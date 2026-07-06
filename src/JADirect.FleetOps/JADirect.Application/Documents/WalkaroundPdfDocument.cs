using JADirect.Domain.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JADirect.Application.Documents;

/// <summary>
/// Documento QuestPDF do walkaround check.
/// Recebe WalkaroundPdfData já completamente preenchido pelo WalkaroundPdfService.
/// Não acessa banco de dados nem S3.
/// Estrutura: Página 1 (resumo) + uma página de anexo por foto em Photos.
/// </summary>
public class WalkaroundPdfDocument : IDocument
{
    private readonly WalkaroundPdfData _dataWalkaround;

    // - Paleta de cores -
    // Cores primárias do sistema JADirect
    private const string ColorBrandDark = "#008080";
    private const string ColorBrandMid = "#0f766e";
    private const string ColorBrandAccent = "#0d9488";
    private const string ColorAccentLight = "#f0fdfa";

    // Cores de status — funcionais, sem conflito com identidade
    private const string ColorOk = "#16A34A";
    private const string ColorAttention = "#D97706";
    private const string ColorDefect = "#DC2626";
    private const string ColorOkBg = "#DCFCE7";
    private const string ColorAttentionBg = "#FEF3C7";
    private const string ColorDefectBg = "#FEE2E2";

    // Cores auxiliares de layout complementam sem substituir identidade
    private const string ColorRowLight = "#F8FAFC";
    private const string ColorRowAlt = "#f0fdfa";
    private const string ColorBorder = "#99d6d0";
    private const string ColorTextMain = "#0F172A";
    private const string ColorTextMuted = "#64748B";

    private WalkaroundPdfDocument(WalkaroundPdfData dataWalkaround)
    {
        _dataWalkaround = dataWalkaround;
    }

    /// <summary>
    /// Ponto de entrada estático para geração do PDF.
    /// Encapsula instanciação e chamada GeneratePdf() do QuestPDF.
    /// </summary>
    public static byte[] GeneratePdf(WalkaroundPdfData dataWalkaround)
    {
        return new WalkaroundPdfDocument(dataWalkaround).GeneratePdf();
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title  = string.Format("Walkaround Check - {0}", _dataWalkaround.VehicleRegistration),
        Author = "DCODE Solutions - JADirect FleetOps"
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(ComposeSummaryPage);

        for (int photoIndex = 0; photoIndex < _dataWalkaround.Photos.Count; photoIndex++)
        {
            WalkaroundPdfPhotoData photo = _dataWalkaround.Photos[photoIndex];

            ChecklistItemResult? item = _dataWalkaround.Items
                .FirstOrDefault(checklist => checklist.ItemId == photo.ChecklistItemId);

            if (item == null)
            {
                continue;
            }

            int oneBasedIndex = photoIndex + 1;
            int totalPhotos = _dataWalkaround.Photos.Count;

            container.Page(page =>
                ComposeAnnexPage(page, photo, item, oneBasedIndex, totalPhotos));
        }
    }

    // - PÁGINA 1: RESUMO -

    private void ComposeSummaryPage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(14, Unit.Millimetre);

        page.Content().Column(column =>
        {
            column.Item().Element(ComposeMainHeader);
            column.Item().Height(5, Unit.Millimetre);
            column.Item().Element(ComposeMetaGrid);
            column.Item().Height(4, Unit.Millimetre);
            column.Item().Element(ComposeCounterRow);
            column.Item().Height(4, Unit.Millimetre);
            column.Item().Text("INSPECTION CHECKLIST")
                .FontSize(8).Bold().FontColor(ColorBrandMid);
            column.Item().BorderBottom(0.5f).BorderColor(ColorBrandAccent).Height(1);
            column.Item().Height(2, Unit.Millimetre);
            column.Item().Element(ComposeChecklistTable);
            column.Item().Height(4, Unit.Millimetre);
            column.Item().Element(ComposeLegalNotice);
        });

        page.Footer().Element(ComposeFooter);
    }

    private void ComposeMainHeader(IContainer container)
    {
        int defectCount    = _dataWalkaround.Items.Count(item => item.State == "Defect");
        int attentionCount = _dataWalkaround.Items.Count(item => item.State == "Attention");

        string statusLabel = defectCount > 0 ? "DEFECT"
            : attentionCount > 0 ? "ATTENTION"
            : "OK";
        string statusColor = defectCount > 0 ? ColorDefect
            : attentionCount > 0 ? ColorAttention
            : ColorOk;

        container.Background(ColorBrandDark)
            .Padding(8, Unit.Millimetre)
            .Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item()
                        .Text("Walkaround Check Report")
                        .FontSize(18).Bold().FontColor(Colors.White);

                    column.Item()
                        .Text("Daily Vehicle Inspection  •  JADirect FleetOps")
                        .FontSize(8.5f).FontColor(ColorAccentLight);

                    column.Item().Height(3, Unit.Millimetre);

                    column.Item()
                        .Background(ColorBrandMid)
                        .Padding(3, Unit.Millimetre)
                        .Row(refRow =>
                        {
                            refRow.AutoItem().Column(refColumn =>
                            {
                                refColumn.Item()
                                    .Text("REF. No.")
                                    .FontSize(6.5f).FontColor(ColorAccentLight);
                                refColumn.Item()
                                    .Text(_dataWalkaround.ReferenceNumber)
                                    .FontSize(8.5f).Bold().FontColor(Colors.White);
                            });
                        });
                });

                row.ConstantItem(55, Unit.Millimetre).Column(column =>
                {
                    column.Item()
                        .Background(statusColor)
                        .Padding(3, Unit.Millimetre)
                        .AlignCenter()
                        .Text(string.Format("● {0}", statusLabel))
                        .FontSize(9).Bold().FontColor(Colors.White);

                    column.Item().Height(2, Unit.Millimetre);

                    column.Item()
                        .AlignCenter()
                        .Text("Overall Status")
                        .FontSize(7).FontColor(ColorAccentLight);

                    column.Item().Height(6, Unit.Millimetre);

                    column.Item()
                        .AlignCenter()
                        .Text("DCODE Solutions")
                        .FontSize(7).Bold().FontColor(Colors.White);

                    column.Item()
                        .AlignCenter()
                        .Text("jadirect.ie")
                        .FontSize(6.5f).FontColor(ColorAccentLight);
                });
            });
    }

    private void ComposeMetaGrid(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(3.8f);
                columns.ConstantColumn(6, Unit.Millimetre);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(3.8f);
            });

            void MetaRow(string label1, string value1, string label2, string value2)
            {
                table.Cell().Background(ColorRowLight)
                    .BorderBottom(0.3f).BorderColor(ColorBorder)
                    .Padding(3).Text(label1)
                    .FontSize(7.5f).FontColor(ColorTextMuted);

                table.Cell().Background(ColorRowLight)
                    .BorderBottom(0.3f).BorderColor(ColorBorder)
                    .Padding(3).Text(value1)
                    .FontSize(8.5f).Bold().FontColor(ColorTextMain);

                table.Cell();

                table.Cell().Background(ColorRowLight)
                    .BorderBottom(0.3f).BorderColor(ColorBorder)
                    .Padding(3).Text(label2)
                    .FontSize(7.5f).FontColor(ColorTextMuted);

                table.Cell().Background(ColorRowLight)
                    .BorderBottom(0.3f).BorderColor(ColorBorder)
                    .Padding(3).Text(value2)
                    .FontSize(8.5f).Bold().FontColor(ColorTextMain);
            }

            MetaRow(
                "Vehicle Registration", _dataWalkaround.VehicleRegistration,
                "Vehicle Make / Model",
                string.Format("{0} {1}", _dataWalkaround.VehicleMake, _dataWalkaround.VehicleModel));

            MetaRow(
                "Vehicle Type",  _dataWalkaround.VehicleType,
                "Driver", _dataWalkaround.DriverName);

            MetaRow(
                "Date", _dataWalkaround.CheckDate.ToString("dd MMMM yyyy"),
                "Time", _dataWalkaround.CheckDate.ToString("HH:mm"));

            MetaRow(
                "Odometer Reading",
                string.Format("{0:N0} km", _dataWalkaround.Odometer),
                "Submitted Via",
                "JADirect FleetOps — Mobile");
        });
    }

    private void ComposeCounterRow(IContainer container)
    {
        int okCount = _dataWalkaround.Items.Count(item => item.State == "Good");
        int attentionCount = _dataWalkaround.Items.Count(item => item.State == "Attention");
        int defectCount = _dataWalkaround.Items.Count(item => item.State == "Defect");
        int totalCount = _dataWalkaround.Items.Count;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            void CounterCell(string bgColor, string fgColor, int count, string label)
            {
                table.Cell()
                    .Background(bgColor)
                    .Border(0.5f).BorderColor(ColorBorder)
                    .PaddingVertical(4, Unit.Millimetre)
                    .Column(column =>
                    {
                        column.Item().AlignCenter()
                            .Text(count.ToString())
                            .FontSize(20).Bold().FontColor(fgColor);

                        column.Item().AlignCenter()
                            .Text(label)
                            .FontSize(7).FontColor(ColorTextMuted);
                    });
            }

            CounterCell(ColorOkBg, ColorOk,  okCount, "OK");
            CounterCell(ColorAttentionBg, ColorAttention, attentionCount, "Attention");
            CounterCell(ColorDefectBg, ColorDefect, defectCount, "Defect");
            CounterCell(ColorAccentLight, ColorBrandMid, totalCount, "Total Items");
        });
    }

    private void ComposeChecklistTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3.0f);
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(3.2f);
                columns.RelativeColumn(0.8f);
            });

            void HeaderCell(string text)
            {
                table.Cell()
                    .Background(ColorBrandMid)
                    .Padding(3)
                    .AlignCenter()
                    .Text(text)
                    .FontSize(7.5f).Bold().FontColor(Colors.White);
            }

            HeaderCell("ITEM");
            HeaderCell("CATEGORY");
            HeaderCell("STATUS");
            HeaderCell("NOTES / OBSERVATIONS");
            HeaderCell("PIC.");

            for (int rowIndex = 0; rowIndex < _dataWalkaround.Items.Count; rowIndex++)
            {
                ChecklistItemResult item = _dataWalkaround.Items[rowIndex];

                string rowBg = item.State == "Attention" ? ColorAttentionBg
                    : item.State == "Defect" ? ColorDefectBg
                    : rowIndex % 2 == 0 ? ColorRowLight
                    : ColorRowAlt;

                string statusBg = item.State == "Attention" ? ColorAttentionBg
                    : item.State == "Defect" ? ColorDefectBg
                    : ColorOkBg;

                string statusFg = item.State == "Attention" ? ColorAttention
                    : item.State == "Defect" ? ColorDefect
                    : ColorOk;

                string statusLabel = item.State == "Attention" ? "ATTENTION"
                    : item.State == "Defect" ? "DEFECT"
                    : "OK";

                table.Cell().Background(rowBg)
                    .BorderBottom(0.3f).BorderColor(ColorBorder)
                    .Padding(3)
                    .Text(item.Label)
                    .FontSize(7.5f).FontColor(ColorTextMain);

                table.Cell().Background(rowBg)
                    .BorderBottom(0.3f).BorderColor(ColorBorder)
                    .Padding(3)
                    .Text(item.Category)
                    .FontSize(6.8f).FontColor(ColorTextMuted);

                table.Cell().Background(statusBg)
                    .BorderBottom(0.3f).BorderColor(ColorBorder)
                    .Padding(3).AlignCenter()
                    .Text(statusLabel)
                    .FontSize(7).Bold().FontColor(statusFg);

                table.Cell().Background(rowBg)
                    .BorderBottom(0.3f).BorderColor(ColorBorder)
                    .Padding(3)
                    .Text(string.IsNullOrEmpty(item.Note) ? "—" : item.Note)
                    .FontSize(7.5f).FontColor(ColorTextMain);

                WalkaroundPdfPhotoData? photo = _dataWalkaround.Photos
                    .FirstOrDefault(p => p.ChecklistItemId == item.ItemId);

                if (photo != null && photo.ImageBytes.Length > 0)
                {
                    table.Cell().Background(rowBg)
                        .BorderBottom(0.3f).BorderColor(ColorBorder)
                        .Padding(2).AlignCenter()
                        .Width(11, Unit.Millimetre)
                        .Image(photo.ImageBytes).FitArea();
                }
                else
                {
                    table.Cell().Background(rowBg)
                        .BorderBottom(0.3f).BorderColor(ColorBorder)
                        .Padding(3);
                }
            }
        });
    }

    private void ComposeLegalNotice(IContainer container)
    {
        container
            .Background(ColorAccentLight)
            .Border(0.8f).BorderColor(ColorBrandAccent)
            .Padding(5, Unit.Millimetre)
            .AlignCenter()
            .Text(
                "This walkaround check was completed and submitted digitally via JADirect FleetOps " +
                "by the driver named above. The inspection record is stored securely and timestamped. " +
                "This document is a true printed representation of the digital record. " +
                "Issued by DCODE Solutions — jadirect.ie")
            .FontSize(7.5f).FontColor(ColorTextMain);
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("JADirect FleetOps  •  DCODE Solutions  •  ")
                .FontSize(6.5f).FontColor(ColorTextMuted);

            text.Span(string.Format("{0} — {1}  •  Ref: {2}",
                    _dataWalkaround.VehicleRegistration,
                    _dataWalkaround.CheckDate.ToString("dd MMM yyyy"),
                    _dataWalkaround.ReferenceNumber))
                .FontSize(6.5f).FontColor(ColorTextMuted);

            text.Span("  Page ").FontSize(6.5f).FontColor(ColorTextMuted);
            text.CurrentPageNumber().FontSize(6.5f).FontColor(ColorTextMuted);
        });
    }

    // - PÁGINAS DE ANEXO -

    private void ComposeAnnexPage(
        PageDescriptor page,
        WalkaroundPdfPhotoData photo,
        ChecklistItemResult item,
        int photoIndex,
        int totalPhotos)
    {
        page.Size(PageSizes.A4);
        page.Margin(14, Unit.Millimetre);

        page.Content().Column(column =>
        {
            column.Item().Element(ComposeCompactHeader);
            column.Item().Height(4, Unit.Millimetre);

            // Rótulo de anexo
            column.Item()
                .Background(ColorAccentLight)
                .Border(0.8f).BorderColor(ColorBrandAccent)
                .Padding(5, Unit.Millimetre)
                .Text(string.Format("ANNEX A{0} of {1}  —  Photo Evidence",
                    photoIndex, totalPhotos))
                .FontSize(9).Bold().FontColor(ColorTextMain);

            column.Item().Height(4, Unit.Millimetre);

            // Bloco de informação do item
            string itemBg = item.State == "Attention" ? ColorAttentionBg
                : item.State == "Defect" ? ColorDefectBg
                : ColorRowLight;
            string itemFg = item.State == "Attention" ? ColorAttention
                : item.State == "Defect" ? ColorDefect
                : ColorOk;
            string itemStatus = item.State == "Attention" ? "ATTENTION"
                : item.State == "Defect" ? "DEFECT"
                : "OK";

            column.Item()
                .Background(itemBg)
                .Border(0.8f).BorderColor(itemFg)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4.2f);
                        columns.RelativeColumn(3.0f);
                        columns.RelativeColumn(2.8f);
                    });

                    table.Cell().Padding(4)
                        .Text("Item").FontSize(7.5f).FontColor(ColorTextMuted);
                    table.Cell().Padding(4)
                        .Text("Category").FontSize(7.5f).FontColor(ColorTextMuted);
                    table.Cell().Padding(4)
                        .Text("Status").FontSize(7.5f).FontColor(ColorTextMuted);

                    table.Cell().Padding(4)
                        .Text(item.Label).FontSize(11).Bold().FontColor(ColorTextMain);
                    table.Cell().Padding(4)
                        .Text(item.Category).FontSize(9).FontColor(ColorTextMain);
                    table.Cell().Padding(4)
                        .Text(itemStatus).FontSize(10).Bold().FontColor(itemFg);
                });

            // Observações (se houver)
            if (!string.IsNullOrEmpty(item.Note))
            {
                column.Item().Height(3, Unit.Millimetre);
                column.Item()
                    .Background(ColorRowLight)
                    .Border(0.5f).BorderColor(ColorBorder)
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.8f);
                            columns.RelativeColumn(8.2f);
                        });

                        table.Cell().Padding(4)
                            .Text("Observations:")
                            .FontSize(7.5f).Bold().FontColor(ColorTextMuted);
                        table.Cell().Padding(4)
                            .Text(item.Note)
                            .FontSize(8.5f).FontColor(ColorTextMain);
                    });
            }

            column.Item().Height(4, Unit.Millimetre);

            // Seção de foto
            column.Item()
                .Text("PHOTO EVIDENCE")
                .FontSize(8).Bold().FontColor(ColorBrandMid);
            column.Item().BorderBottom(0.5f).BorderColor(ColorBrandAccent).Height(1);
            column.Item().Height(3, Unit.Millimetre);

            column.Item()
                .MaxHeight(130, Unit.Millimetre)
                .Image(photo.ImageBytes).FitArea();

            column.Item().Height(3, Unit.Millimetre);

            // Legenda
            column.Item().AlignCenter()
                .Text(string.Format(
                    "Photo {0} of {1}  •  {2}  •  Captured {3} at {4}",
                    photoIndex,
                    totalPhotos,
                    item.Label,
                    _dataWalkaround.CheckDate.ToString("dd MMM yyyy"),
                    _dataWalkaround.CheckDate.ToString("HH:mm")))
                .FontSize(8).FontColor(ColorTextMuted);

            column.Item().Height(4, Unit.Millimetre);

            // Linha de assinatura
            column.Item()
                .Background(ColorRowLight)
                .Border(0.5f).BorderColor(ColorBorder)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.0f);
                        columns.RelativeColumn(4.0f);
                        columns.RelativeColumn(1.7f);
                        columns.RelativeColumn(2.3f);
                    });

                    table.Cell().Padding(5)
                        .Text("Driver Signature:")
                        .FontSize(8).FontColor(ColorTextMuted);
                    table.Cell().Padding(5)
                        .Text("_______________________________")
                        .FontSize(8).FontColor(ColorTextMain);
                    table.Cell().Padding(5)
                        .Text("Date / Time:")
                        .FontSize(8).FontColor(ColorTextMuted);
                    table.Cell().Padding(5)
                        .Text(string.Format("{0}  {1}",
                            _dataWalkaround.CheckDate.ToString("dd MMM yyyy"),
                            _dataWalkaround.CheckDate.ToString("HH:mm")))
                        .FontSize(8).Bold().FontColor(ColorTextMain);
                });
        });

        page.Footer().Element(ComposeFooter);
    }

    private void ComposeCompactHeader(IContainer container)
    {
        container
            .Background(ColorBrandDark)
            .Padding(4, Unit.Millimetre)
            .Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item()
                        .Text("Walkaround Check — Photo Evidence")
                        .FontSize(10).Bold().FontColor(Colors.White);

                    column.Item()
                        .Text(string.Format(
                            "{0}  •  {1} {2}  •  {3}  {4}  •  Ref: {5}",
                            _dataWalkaround.VehicleRegistration,
                            _dataWalkaround.VehicleMake,
                            _dataWalkaround.VehicleModel,
                            _dataWalkaround.CheckDate.ToString("dd MMM yyyy"),
                            _dataWalkaround.CheckDate.ToString("HH:mm"),
                            _dataWalkaround.ReferenceNumber))
                        .FontSize(7.5f).FontColor(ColorAccentLight);
                });

                row.AutoItem().Column(column =>
                {
                    column.Item().AlignRight()
                        .Text("DCODE Solutions")
                        .FontSize(7).Bold().FontColor(Colors.White);
                    column.Item().AlignRight()
                        .Text("jadirect.ie")
                        .FontSize(6.5f).FontColor(ColorAccentLight);
                });
            });
    }
}