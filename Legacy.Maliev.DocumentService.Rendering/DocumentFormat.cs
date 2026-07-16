using System.Globalization;

namespace Legacy.Maliev.DocumentService.Rendering;

internal static class DocumentFormat
{
    internal static string Date(DateTime? value) => value is null || value.Value == default
        ? "-"
        : value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    internal static string Identifier(long? value) => value is null || value <= 0
        ? "-"
        : value.Value.ToString(CultureInfo.InvariantCulture);

    internal static string DurationDays(int value) => value <= 0
        ? "-"
        : $"{value.ToString(CultureInfo.InvariantCulture)} days / {value.ToString(CultureInfo.InvariantCulture)} วัน";

    internal static string Safe(string? value) => value?.Trim() ?? string.Empty;

    internal static string Lines(params string?[] values) => string.Join('\n', values.Where(value => !string.IsNullOrWhiteSpace(value)));

    internal static string? Prefix(string prefix, string? value) => string.IsNullOrWhiteSpace(value) ? null : $"{prefix}{value}";
}
