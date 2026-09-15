using Microsoft.Win32;

namespace NovaBattery;

internal static class SettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaBattery");
    private static readonly string LevelFile = Path.Combine(Folder, "last-level.txt");

    internal static int? LoadLastPercent()
    {
        try
        {
            return int.TryParse(File.ReadAllText(LevelFile), out int value) && value is >= 0 and <= 100
                ? value : null;
        }
        catch { return null; }
    }

    internal static void SaveLastPercent(int value)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(LevelFile, value.ToString());
        }
        catch { }
    }

    internal static bool StartsWithWindows
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("NovaBattery") is string;
        }
        set
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (value) key?.SetValue("NovaBattery", $"\"{Application.ExecutablePath}\"");
            else key?.DeleteValue("NovaBattery", false);
        }
    }
}

