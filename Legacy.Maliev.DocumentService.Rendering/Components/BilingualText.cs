using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal enum BilingualTextAlignment
{
    Left,
    Center,
    Right,
}

internal static class BilingualText
{
    internal static void Compose(
        IContainer container,
        string english,
        string? thai,
        float englishFontSize,
        float thaiFontSize,
        bool bold = false,
        BilingualTextAlignment alignment = BilingualTextAlignment.Left)
    {
        container.Column(column =>
        {
            column.Spacing(0.5f);
            if (!string.IsNullOrWhiteSpace(english))
                column.Item().Element(item => Line(item, english, englishFontSize, bold, false, alignment));
            if (!string.IsNullOrWhiteSpace(thai))
                column.Item().Element(item => Line(item, thai, thaiFontSize, bold, true, alignment));
        });
    }

    internal static void ComposeCombined(
        IContainer container,
        string value,
        float englishFontSize,
        float thaiFontSize,
        bool bold = false,
        BilingualTextAlignment alignment = BilingualTextAlignment.Left)
    {
        var (english, thai) = Split(value);
        Compose(container, english, thai, englishFontSize, thaiFontSize, bold, alignment);
    }

    internal static (string English, string? Thai) Split(string value)
    {
        const string separator = " / ";
        var index = value.IndexOf(separator, StringComparison.Ordinal);
        return index < 0
            ? (value, null)
            : (value[..index].Trim(), value[(index + separator.Length)..].Trim());
    }

    private static void Line(
        IContainer container,
        string value,
        float fontSize,
        bool bold,
        bool thai,
        BilingualTextAlignment alignment)
    {
        var aligned = alignment switch
        {
            BilingualTextAlignment.Center => container.AlignCenter(),
            BilingualTextAlignment.Right => container.AlignRight(),
            _ => container.AlignLeft(),
        };
        var family = (bold, thai) switch
        {
            (true, true) => new[] { DocumentStyle.ThaiBold, DocumentStyle.LatinBold },
            (true, false) => new[] { DocumentStyle.LatinBold, DocumentStyle.ThaiBold },
            (false, true) => new[] { DocumentStyle.Thai, DocumentStyle.Latin },
            _ => new[] { DocumentStyle.Latin, DocumentStyle.Thai },
        };
        var text = aligned.Text(value).FontFamily(family).FontSize(fontSize);
        if (thai)
            text.FontColor(DocumentStyle.MutedInk);
    }
}
