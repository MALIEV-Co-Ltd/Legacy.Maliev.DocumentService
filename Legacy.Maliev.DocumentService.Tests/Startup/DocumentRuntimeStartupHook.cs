using System.Text.Json;

/// <summary>Test-only startup hook verifies the isolated child actually loaded both frameworks.</summary>
public static class StartupHook
{
    /// <summary>Writes only to the caller-owned test directory and never changes startup output.</summary>
    public static void Initialize()
    {
        var destination = Environment.GetEnvironmentVariable("DOCUMENT_RUNTIME_RECEIPT");
        if (string.IsNullOrEmpty(destination) || !Path.IsPathFullyQualified(destination))
            throw new InvalidOperationException("Runtime receipt destination is required.");
        var run = Environment.GetEnvironmentVariable("DOCUMENT_RUNTIME_RUN");
        if (string.IsNullOrEmpty(run)) throw new InvalidOperationException("Run identity is required.");
        var hookPath = typeof(StartupHook).Assembly.Location;
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        var core = typeof(object).Assembly;
        var aspNet = typeof(Microsoft.AspNetCore.Builder.WebApplication).Assembly;
        using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, new
        {
            pid = Environment.ProcessId,
            birthUtcTicks = current.StartTime.ToUniversalTime().Ticks,
            run,
            hookPath,
            hookSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(hookPath))),
            coreMajor = Environment.Version.Major,
            aspNetMajor = aspNet.GetName().Version?.Major,
            corePath = core.Location,
            aspNetPath = aspNet.Location,
        });
    }
}
