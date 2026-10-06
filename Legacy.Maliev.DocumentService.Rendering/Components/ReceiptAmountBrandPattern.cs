using System.Globalization;
using System.Text;
using System.Xml.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal static class ReceiptAmountBrandPattern
{
    private const double WordWidth = 14.26;
    private const double Pitch = WordWidth + 2;
    private static readonly string WordPaths = LoadWordPaths();

    internal static void Compose(IContainer container, string amountWords) => container.Layers(layers =>
    {
        layers.Layer().Svg(size => CreateSvg(size.Width, size.Height));
        // The existing foreground controls the row size, pagination and inherited text style.
        layers.PrimaryLayer().AlignCenter().Text($"( {amountWords} )");
    });

    private static string CreateSvg(float width, float height)
    {
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        svg.Append("<defs><g id=\"brand-word\">").Append(WordPaths).Append("</g>");
        svg.Append(CultureInfo.InvariantCulture, $"<clipPath id=\"amount-band\"><rect width=\"{width}\" height=\"{height}\"/></clipPath></defs><g fill=\"#BEBEBE\" clip-path=\"url(#amount-band)\">");
        // Source intent: 4-point letters, 2-point gap, 5-point rows and a 12-row stagger.
        // SVG uses downward y coordinates; the 60-point tile repeats within this row only.
        for (var row = 0; row * 5 < height; row++)
        {
            var shift = row % 12 * Pitch / 12;
            for (var x = shift - Pitch; x < width; x += Pitch)
            {
                svg.Append(CultureInfo.InvariantCulture, $"<use xlink:href=\"#brand-word\" transform=\"translate({x} {row * 5})\"/>");
            }
        }

        return svg.Append("</g></svg>").ToString();
    }

    private static string LoadWordPaths()
    {
        using var stream = typeof(ReceiptAmountBrandPattern).Assembly.GetManifestResourceStream(
            "Legacy.Maliev.DocumentService.Rendering.Resources.ReceiptAmountBrandWord.svg")
            ?? throw new InvalidOperationException("The embedded receipt brand artwork is missing.");
        var resource = XDocument.Load(stream);
        XNamespace svg = "http://www.w3.org/2000/svg";
        return resource.Root!.Element(svg + "g")!.ToString(SaveOptions.DisableFormatting);
    }
}
