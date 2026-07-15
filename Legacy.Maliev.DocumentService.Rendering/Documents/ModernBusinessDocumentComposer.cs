using Legacy.Maliev.DocumentService.Rendering.Components;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class ModernBusinessDocumentComposer
{
    internal static byte[] Render(BusinessDocumentSpec spec) => Document.Create(document => ComposePageSet(document, spec)).GeneratePdf();

    internal static void ComposePageSet(IDocumentContainer document, BusinessDocumentSpec spec)
    {
        document.Page(page =>
        {
            A4Page.Configure(page, spec.BottomMargin);
            page.Header().Column(header =>
            {
                header.Item().ShowOnce().Element(container => DocumentHeader.ComposeMasthead(
                    container,
                    spec.Logo,
                    spec.Title,
                    spec.ThaiTitle,
                    spec.Reference,
                    QuestDocumentRenderer.CompanyIdentity(),
                    spec.Metadata));
                header.Item().SkipOnce().Element(container => DocumentHeader.Compose(container, spec.Logo, spec.Title, spec.ThaiTitle, spec.Reference));
            });
            page.Content().Element(container => ComposeContent(container, spec));
            page.Footer().Element(spec.Footer);
        });
    }

    private static void ComposeContent(IContainer container, BusinessDocumentSpec spec)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            for (var index = 0; index < spec.Parties.Count; index += 3)
            {
                column.Item().Row(row =>
                {
                    row.Spacing(12);
                    for (var offset = 0; offset < 3; offset++)
                    {
                        var partyIndex = index + offset;
                        if (partyIndex < spec.Parties.Count)
                            row.RelativeItem().Element(box => PartyCard(box, spec.Parties[partyIndex]));
                        else if (spec.Parties.Count > 2)
                            row.RelativeItem();
                    }

                    if (spec.Parties.Count == 1)
                        row.RelativeItem();
                });
            }

            if (!string.IsNullOrWhiteSpace(spec.Introduction))
                column.Item().PaddingTop(2).Element(item => BilingualText.ComposeCombined(item, spec.Introduction, 8, 7, bold: true));

            if (spec.OperationalFields.Count > 0)
                column.Item().Element(box => OperationalTable(box, spec.OperationalFields));

            column.Item().Element(box => ItemTable(box, spec.Items));

            column.Item().Row(row =>
            {
                row.Spacing(14);
                row.RelativeItem().Element(box => SupportingNotes(box, spec.SupportingNote, spec.NotesHeading, spec.Notes));
                row.ConstantItem(205).ShowEntire().Element(box => TotalsBlock.Compose(box, spec.Currency, spec.Totals.ToArray()));
            });
        });
    }

    private static void PartyCard(IContainer container, BusinessPartySection party)
    {
        container.Column(column =>
        {
            column.Item()
                .Background(DocumentStyle.HeaderFill)
                .PaddingHorizontal(5)
                .PaddingVertical(3)
                .Element(item => BilingualText.ComposeCombined(item, party.Heading, 8, 7, bold: true));
            column.Item()
                .PaddingHorizontal(5)
                .PaddingTop(5)
                .Text(string.IsNullOrWhiteSpace(party.Body) ? "-" : party.Body)
                .LineHeight(1.25f);
        });
    }

    private static void OperationalTable(IContainer container, IReadOnlyList<(string Key, string? Value)> fields)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in fields)
                    columns.RelativeColumn();
            });

            for (var index = 0; index < fields.Count; index++)
            {
                var (key, _) = fields[index];
                BilingualText.ComposeCombined(
                    ItemTableStyle.HeaderCell(table.Cell(), firstColumn: index == 0),
                    key,
                    7,
                    6,
                    alignment: BilingualTextAlignment.Center);
            }

            for (var index = 0; index < fields.Count; index++)
            {
                var (_, value) = fields[index];
                ItemTableStyle.Cell(table.Cell(), firstColumn: index == 0)
                    .AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                    .FontSize(7);
            }
        });
    }

    private static void ItemTable(IContainer container, IReadOnlyList<BusinessDocumentItem> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(5.1f);
                columns.RelativeColumn(1.9f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(1.9f);
            });

            table.Header(header =>
            {
                var headings = new[]
                {
                    "Item / รหัส",
                    "Description / รายละเอียด",
                    "Unit Price / หน่วยละ",
                    "Quantity / จำนวน",
                    "Amount / จำนวนเงิน",
                };

                for (var index = 0; index < headings.Length; index++)
                    BilingualText.ComposeCombined(
                        ItemTableStyle.HeaderCell(header.Cell(), firstColumn: index == 0),
                        headings[index],
                        7,
                        6,
                        alignment: BilingualTextAlignment.Center);
            });

            var rows = items.Count == 0
                ? [new BusinessDocumentItem("-", "No line items / ไม่มีรายการ", "-", "-", "-")]
                : items;

            foreach (var item in rows)
            {
                ItemTableStyle.Cell(table.Cell(), firstColumn: true).ShowEntire().AlignCenter().Text(item.Code);
                ItemTableStyle.Cell(table.Cell()).ShowEntire().Text(item.Description).LineHeight(1.25f);
                ItemTableStyle.Cell(table.Cell()).ShowEntire().AlignRight().Text(item.UnitPrice);
                ItemTableStyle.Cell(table.Cell()).ShowEntire().AlignCenter().Text(item.Quantity);
                ItemTableStyle.Cell(table.Cell()).ShowEntire().AlignRight().Text(item.Amount);
            }
        });
    }

    private static void SupportingNotes(IContainer container, string? supportingNote, string notesHeading, string? notes)
    {
        if (string.IsNullOrWhiteSpace(supportingNote) && string.IsNullOrWhiteSpace(notes))
            return;

        container.BorderLeft(2).BorderColor(DocumentStyle.Rule).PaddingLeft(8)
            .Column(column =>
            {
                if (!string.IsNullOrWhiteSpace(supportingNote))
                    column.Item().Element(item => BilingualText.ComposeCombined(item, supportingNote, 7, 6));

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    column.Item().PaddingTop(string.IsNullOrWhiteSpace(supportingNote) ? 0 : 12)
                        .Element(item => BilingualText.ComposeCombined(item, notesHeading, 8, 7, bold: true));
                    column.Item().PaddingTop(3).Text(notes).LineHeight(1.3f);
                }
            });
    }
}
