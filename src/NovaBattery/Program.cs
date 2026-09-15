using System.Runtime.InteropServices;

namespace NovaBattery;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains("--once", StringComparer.OrdinalIgnoreCase))
        {
            AttachToParentConsole();
            var result = await NovaBatteryReader.ReadAsync();
            Console.WriteLine(result.ToConsoleString());
            Environment.ExitCode = result.IsConnected ? 0 : 1;
            return;
        }

        if (args.Contains("--probe", StringComparer.OrdinalIgnoreCase))
        {
            if (!AttachToParentConsole()) AllocConsole();
            Console.WriteLine("Nova Battery diagnostic probe\n");
            Console.WriteLine(await NovaBatteryReader.ProbeAsync());
            Console.WriteLine("\nPress Enter to close.");
            Console.ReadLine();
            return;
        }

        using var mutex = new Mutex(true, "Local\\NovaBattery.App", out bool first);
        if (!first) return;
        Application.Run(new MainForm());
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint processId);

    private static bool AttachToParentConsole()
    {
        if (!AttachConsole(0xFFFFFFFF)) return false;
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        return true;
    }
}
