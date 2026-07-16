using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal sealed record BusinessDocumentSpec(
    string Title,
    string ThaiTitle,
    string? Reference,
    byte[] Logo,
    float BottomMargin,
    IReadOnlyList<(string Key, string? Value)> Metadata,
    IReadOnlyList<BusinessPartySection> Parties,
    string? Introduction,
    IReadOnlyList<(string Key, string? Value)> OperationalFields,
    IReadOnlyList<BusinessDocumentItem> Items,
    string? SupportingNote,
    IReadOnlyList<(string Label, decimal? Value)> Totals,
    string? Currency,
    string NotesHeading,
    string? Notes,
    Action<IContainer> Footer);

internal sealed record BusinessPartySection(string Heading, string Body);

internal sealed record BusinessDocumentItem(
    string Code,
    string Description,
    string UnitPrice,
    string Quantity,
    string Amount);
