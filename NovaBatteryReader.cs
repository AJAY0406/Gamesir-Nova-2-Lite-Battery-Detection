using System.Text;

namespace NovaBattery;

internal static class NovaBatteryReader
{
    private const ushort GameSirVendorId = 0x3537;
    private const ushort VendorUsagePage = 0xFF7A;
    private static readonly ushort[] KnownPids = [0x1098, 0x100F];

    internal static async Task<BatteryReading> ReadAsync(CancellationToken cancellationToken = default)
    {
        var all = HidNative.Enumerate();
        var candidates = all
            .Where(d => d.VendorId == GameSirVendorId &&
                        (d.UsagePage == VendorUsagePage ||
                         (KnownPids.Contains(d.ProductId) &&
                          d.Path.Contains("mi_02", StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(d => d.UsagePage == VendorUsagePage)
            .ThenByDescending(d => d.OutputLength >= 65)
            .ToList();

        if (candidates.Count == 0)
            return BatteryReading.Disconnected("No compatible GameSir HID interface was found.");

        var errors = new List<string>();
        BatteryReading? chargingReading = null;
        BatteryReading? unavailableReading = null;
        foreach (var device in candidates)
        {
            try
            {
                BatteryReading? reading = await QueryAsync(device, cancellationToken);
                if (reading is null) continue;

                // Some wired identities return only 0xFF (charging), while a
                // second vendor collection may still expose the numeric level.
                // Keep looking and merge both facts if a percentage is found.
                if (reading.Percent is int)
                {
                    return chargingReading is null
                        ? reading
                        : reading with
                        {
                            IsCharging = true,
                            Connection = chargingReading.Connection
                        };
                }

                if (reading.IsCharging) chargingReading ??= reading;
                else unavailableReading ??= reading;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{device.ProductId:X4}: {ex.Message}");
            }
        }

        if (chargingReading is not null) return chargingReading;
        if (unavailableReading is not null) return unavailableReading;

        string detail = errors.Count > 0
            ? string.Join("; ", errors)
            : "GameSir interfaces were found, but none returned a battery frame.";
        return BatteryReading.Disconnected(detail);
    }

    private static async Task<BatteryReading?> QueryAsync(HidNative.HidDevice device, CancellationToken outerToken)
    {
        using FileStream stream = HidNative.OpenReadWrite(device);
        int outputLength = Math.Max(65, (int)device.OutputLength);
        byte[] request = new byte[outputLength];
        request[0] = 0x00;
        request[1] = 0x01;
        request[2] = 0x01;
        await stream.WriteAsync(request, outerToken);
        await stream.FlushAsync(outerToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(1800));
        int inputLength = Math.Max(65, (int)device.InputLength);

        while (!timeout.IsCancellationRequested)
        {
            byte[] frame = new byte[inputLength];
            int count;
            try { count = await stream.ReadAsync(frame, timeout.Token); }
            catch (OperationCanceledException) when (!outerToken.IsCancellationRequested)
            { throw new TimeoutException("Battery query timed out."); }
            if (count < 20) continue;
            if (frame[1] == 0xF4 && frame[2] == 0x00) continue;
            if (frame[1] != 0x01) continue;

            byte value = frame[19];
            string name = string.IsNullOrWhiteSpace(device.ProductName)
                ? "GameSir Nova 2 Lite" : device.ProductName;
            if (value == 0xFF)
                return new(true, null, true, device.Connection, name, RawFrame: frame[..count]);
            if (value <= 100)
                return new(true, value, false, device.Connection, name, RawFrame: frame[..count]);
            return new(true, null, false, device.Connection, name,
                $"The controller returned an unknown battery value 0x{value:X2}.", frame[..count]);
        }
        return null;
    }

    internal static async Task<string> ProbeAsync()
    {
        var sb = new StringBuilder();
        var devices = HidNative.Enumerate().Where(d => d.VendorId == GameSirVendorId).ToList();
        if (devices.Count == 0) return "No GameSir HID devices (VID 3537) were found.";

        foreach (var d in devices)
        {
            sb.AppendLine($"VID {d.VendorId:X4} PID {d.ProductId:X4}  usage {d.UsagePage:X4}:{d.Usage:X4}");
            sb.AppendLine($"  {d.ProductName}  input={d.InputLength} output={d.OutputLength}");
            sb.AppendLine($"  path={d.Path}");
        }

        sb.AppendLine();
        foreach (var device in devices.Where(d => d.UsagePage == VendorUsagePage && d.OutputLength >= 65))
        {
            try
            {
                BatteryReading? reading = await QueryAsync(device, CancellationToken.None);
                sb.AppendLine($"Query PID {device.ProductId:X4}: {reading?.ToConsoleString() ?? "No battery reply"}");
                if (reading?.RawFrame is { Length: > 0 })
                    sb.AppendLine($"Frame PID {device.ProductId:X4}: {Convert.ToHexString(reading.RawFrame)}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Query PID {device.ProductId:X4}: {ex.Message}");
            }
        }
        return sb.ToString();
    }
}
