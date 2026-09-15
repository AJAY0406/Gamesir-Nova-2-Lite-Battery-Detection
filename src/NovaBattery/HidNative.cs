using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace NovaBattery;

internal static class HidNative
{
    private const uint DigcfPresent = 0x2;
    private const uint DigcfDeviceInterface = 0x10;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;

    internal sealed record HidDevice(
        string Path, ushort VendorId, ushort ProductId, ushort UsagePage,
        ushort Usage, ushort InputLength, ushort OutputLength, string ProductName)
    {
        public string Connection =>
            ProductName.Contains("dongle", StringComparison.OrdinalIgnoreCase) ||
            ProductName.Contains("receiver", StringComparison.OrdinalIgnoreCase) || ProductId == 0x1098
                ? "2.4 GHz receiver"
                : ProductId == 0x100F ? "USB cable" : "USB HID";
    }

    internal static IReadOnlyList<HidDevice> Enumerate()
    {
        HidD_GetHidGuid(out Guid hidGuid);
        IntPtr infoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
            DigcfPresent | DigcfDeviceInterface);
        if (infoSet == new IntPtr(-1)) return [];

        var devices = new List<HidDevice>();
        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = new SpDeviceInterfaceData { Size = Marshal.SizeOf<SpDeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(infoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                    break;

                SetupDiGetDeviceInterfaceDetail(infoSet, ref interfaceData, IntPtr.Zero, 0,
                    out uint required, IntPtr.Zero);
                if (required == 0) continue;

                IntPtr detail = Marshal.AllocHGlobal((int)required);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(infoSet, ref interfaceData, detail, required,
                            out _, IntPtr.Zero)) continue;
                    string? path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    if (string.IsNullOrWhiteSpace(path)) continue;

                    using SafeFileHandle handle = CreateFile(path, 0, FileShareRead | FileShareWrite,
                        IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                    if (handle.IsInvalid) continue;
                    var attributes = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
                    if (!HidD_GetAttributes(handle, ref attributes)) continue;
                    if (!HidD_GetPreparsedData(handle, out IntPtr preparsed)) continue;
                    try
                    {
                        if (HidP_GetCaps(preparsed, out HidpCaps caps) < 0) continue;
                        var name = new StringBuilder(256);
                        HidD_GetProductString(handle, name, name.Capacity * 2);
                        devices.Add(new(path, attributes.VendorId, attributes.ProductId,
                            caps.UsagePage, caps.Usage, caps.InputReportByteLength,
                            caps.OutputReportByteLength, name.ToString()));
                    }
                    finally { HidD_FreePreparsedData(preparsed); }
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(infoSet); }
        return devices;
    }

    internal static FileStream OpenReadWrite(HidDevice device)
    {
        SafeFileHandle handle = CreateFile(device.Path, GenericRead | GenericWrite,
            FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new FileStream(handle, FileAccess.ReadWrite, Math.Max(65, device.InputLength), true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid hidGuid);
    [DllImport("hid.dll")] private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref HiddAttributes attributes);
    [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out HidpCaps caps);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] private static extern bool HidD_GetProductString(SafeFileHandle handle, StringBuilder value, int length);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr data, ref Guid guid, uint index, ref SpDeviceInterfaceData interfaceData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SpDeviceInterfaceData interfaceData, IntPtr detail, uint size, out uint required, IntPtr data);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
}

