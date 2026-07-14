namespace OrderService.Diagnostics;

public static class DiagnosticsInfo
{
    /// <summary>
    /// Logical service name derived from the entry assembly.
    /// This is a convention — no configuration key is introduced for it.
    /// </summary>
    public static string ServiceName =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Name ?? "OrderService";

    /// <summary>Current UTC date/time as an ISO 8601 string.</summary>
    public static string GetCurrentUtc() => DateTimeOffset.UtcNow.ToString("O");
}
