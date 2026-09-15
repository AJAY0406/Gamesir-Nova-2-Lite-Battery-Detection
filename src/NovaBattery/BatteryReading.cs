namespace NovaBattery;

internal sealed record BatteryReading(
    bool IsConnected,
    int? Percent,
    bool IsCharging,
    string Connection,
    string DeviceName,
    string? Error = null,
    byte[]? RawFrame = null)
{
    public static BatteryReading Disconnected(string error = "Controller not found") =>
        new(false, null, false, "Disconnected", "GameSir Nova 2 Lite", error);

    public string ToConsoleString()
    {
        if (!IsConnected) return $"Disconnected: {Error}";
        if (IsCharging) return Percent is int p
            ? $"Charging (last known {p}%) via {Connection}"
            : $"Charging via {Connection}";
        return Percent is int value
            ? $"{value}% via {Connection}"
            : $"Connected via {Connection}; battery unavailable";
    }
}

