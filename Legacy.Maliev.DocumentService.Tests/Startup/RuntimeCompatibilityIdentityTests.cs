using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;

namespace Legacy.Maliev.DocumentService.Tests.Startup;

/// <summary>Verifies the shared frameworks loaded by each qualification test host.</summary>
public sealed class RuntimeCompatibilityIdentityTests
{
    /// <summary>Rejects a misconfigured qualification host before filtered tests execute.</summary>
    [ModuleInitializer]
    internal static void RequireQualifiedRuntimeWhenRequested()
    {
        var required = Environment.GetEnvironmentVariable("DOCUMENT_REQUIRED_RUNTIME_MAJOR");
        if (required is null) return;
        var assembly = typeof(WebApplication).Assembly;
        if (required != "11" || Environment.Version.Major != 11
            || !RuntimeInformation.FrameworkDescription.StartsWith(".NET 11.", StringComparison.Ordinal)
            || assembly.GetName().Version?.Major != 11
            || !assembly.Location.Replace('\\', '/').Contains("/shared/Microsoft.AspNetCore.App/11.", StringComparison.Ordinal))
            throw new InvalidOperationException("The Document runtime qualification test host did not load the required shared frameworks.");
    }

    /// <summary>Exercises the identity check in the ordinary full test suite.</summary>
    [Fact]
    public async Task Loaded_shared_frameworks_match_the_qualification_lane()
    {
        RequireQualifiedRuntimeWhenRequested();
        var fields = string.Join(" ", Enumerable.Repeat("0", 18));
        Assert.Equal(456UL, DocumentRuntimeProcessIdentity.ParseStartTicks($"123 (name with ) bracket) S {fields} 456"));
        Assert.Throws<InvalidOperationException>(() => DocumentRuntimeProcessIdentity.ParseStartTicks("malformed"));
        Assert.Throws<InvalidOperationException>(() => DocumentRuntimeProcessIdentity.ParseStartTicks($"123 (name) S {fields} nope"));
        Assert.Throws<InvalidOperationException>(() => DocumentRuntimeProcessIdentity.ParseStartTicks($"123 (name) S {fields} 0"));
        DocumentRuntimeProcessIdentity.RequireAcknowledgement(() => "run|123|456", "run|123|456", TimeSpan.FromSeconds(1));
        Assert.Throws<InvalidOperationException>(() => DocumentRuntimeProcessIdentity.RequireAcknowledgement(
            () => "wrong", "run|123|456", TimeSpan.FromSeconds(1)));
        var readCalled = false;
        Assert.Throws<TimeoutException>(() => DocumentRuntimeProcessIdentity.RequireAcknowledgement(
            () => { readCalled = true; return "run|123|456"; }, "run|123|456", TimeSpan.Zero));
        Assert.False(readCalled);
        var times = new Queue<TimeSpan>([TimeSpan.Zero, TimeSpan.FromTicks(1)]);
        Assert.Throws<TimeoutException>(() => DocumentRuntimeProcessIdentity.RequireAcknowledgement(
            () => "run|123|456", "run|123|456", TimeSpan.FromTicks(1), () => times.Dequeue()));
        Assert.Throws<TimeoutException>(() => DocumentRuntimeProcessIdentity.RequireAcknowledgement(
            () => null, "run|123|456", TimeSpan.Zero));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DocumentRuntimeProcessIdentity.AwaitReceiptAsync(
            () => false, () => true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DocumentRuntimeProcessIdentity.AwaitReceiptAsync(
            () => true, () => true, CancellationToken.None));
        await DocumentRuntimeProcessIdentity.AwaitReceiptAsync(() => true, () => false, CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DocumentRuntimeProcessIdentity.AwaitReceiptAsync(
            () => false, () => false, cancelled.Token));
    }
}
