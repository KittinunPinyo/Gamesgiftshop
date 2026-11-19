using ClosedXML.Excel;
using Gamesgiftshop.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gamesgiftshop.Services;

public static class AccountingReportExporter
{
    static AccountingReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
    }

    public static byte[] CreateExcel(AccountingDashboardViewModel model)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("สรุป");
        summary.Cell(1, 1).Value = "สรุปรายรับรายจ่าย";
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 16;
        summary.Cell(2, 1).Value = "ช่วงเวลา";
        summary.Cell(2, 2).Value = model.PeriodLabel;
        summary.Cell(4, 1).Value = "รายรับจากยอดขาย";
        summary.Cell(4, 2).Value = model.Revenue;
        summary.Cell(5, 1).Value = "รายจ่าย";
        summary.Cell(5, 2).Value = model.ExpenseTotal;
        summary.Cell(6, 1).Value = "คืนเงินแล้ว";
        summary.Cell(6, 2).Value = model.RefundTotal;
        summary.Cell(7, 1).Value = "คงเหลือสุทธิ";
        summary.Cell(7, 2).Value = model.NetIncome;
        summary.Cell(8, 1).Value = "ออเดอร์ที่ชำระแล้ว";
        summary.Cell(8, 2).Value = model.PaidOrderCount;
        summary.Range(4, 2, 7, 2).Style.NumberFormat.Format = "#,##0.00;[Red]-#,##0.00";
        summary.Columns().AdjustToContents();

        var ledger = workbook.Worksheets.Add("ธุรกรรม");
        var headers = new[] { "วันที่", "ประเภท", "อ้างอิง", "รายละเอียด", "เงินเข้า/ออก (บาท)" };
        for (var index = 0; index < headers.Length; index++)
        {
            ledger.Cell(1, index + 1).Value = headers[index];
        }
        ledger.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ledger.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;

        for (var index = 0; index < model.LedgerEntries.Count; index++)
        {
            var entry = model.LedgerEntries[index];
            var row = index + 2;
            ledger.Cell(row, 1).Value = entry.Date;
            ledger.Cell(row, 2).Value = entry.Type;
            ledger.Cell(row, 3).Value = entry.Reference;
            ledger.Cell(row, 4).Value = entry.Description;
            ledger.Cell(row, 5).Value = entry.Amount;
        }

        if (model.LedgerEntries.Count > 0)
        {
            ledger.Range(1, 1, model.LedgerEntries.Count + 1, headers.Length).SetAutoFilter();
            ledger.SheetView.FreezeRows(1);
        }
        ledger.Column(1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        ledger.Column(5).Style.NumberFormat.Format = "#,##0.00;[Red]-#,##0.00";
        ledger.Columns().AdjustToContents();
        ledger.Column(4).Width = Math.Min(60, Math.Max(24, ledger.Column(4).Width));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] CreatePdf(AccountingDashboardViewModel model)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontFamily("Tahoma").FontSize(9));
                page.Header().Column(header =>
                {
                    header.Item().Text("Games Gift Shop - Accounting Report").FontSize(18).Bold();
                    header.Item().Text($"สรุป {model.PeriodLabel}");
                });
                page.Content().Column(content =>
                {
                    content.Spacing(8);
                    content.Item().Text($"รายรับจากยอดขาย: ฿{model.Revenue:N2}");
                    content.Item().Text($"รายจ่าย: ฿{model.ExpenseTotal:N2}");
                    content.Item().Text($"คืนเงินแล้ว: ฿{model.RefundTotal:N2}");
                    content.Item().Text($"คงเหลือสุทธิ: ฿{model.NetIncome:N2}").Bold();
                    content.Item().Text($"ออเดอร์ที่ชำระแล้ว: {model.PaidOrderCount}");
                    content.Item().PaddingTop(8).Text("รายการธุรกรรม").FontSize(12).Bold();

                    if (model.LedgerEntries.Count == 0)
                    {
                        content.Item().Text("ไม่มีธุรกรรมในช่วงเวลานี้");
                    }
                    else
                    {
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(62);
                                columns.ConstantColumn(42);
                                columns.ConstantColumn(44);
                                columns.RelativeColumn();
                                columns.ConstantColumn(72);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(TableCellStyle).Text("วันที่").SemiBold();
                                header.Cell().Element(TableCellStyle).Text("ประเภท").SemiBold();
                                header.Cell().Element(TableCellStyle).Text("อ้างอิง").SemiBold();
                                header.Cell().Element(TableCellStyle).Text("รายละเอียด").SemiBold();
                                header.Cell().Element(TableCellStyle).AlignRight().Text("บาท").SemiBold();
                            });

                            foreach (var entry in model.LedgerEntries)
                            {
                                table.Cell().Element(TableCellStyle).Text(entry.Date.ToString("dd/MM/yy"));
                                table.Cell().Element(TableCellStyle).Text(entry.Type);
                                table.Cell().Element(TableCellStyle).Text(entry.Reference);
                                table.Cell().Element(TableCellStyle).Text(entry.Description);
                                table.Cell().Element(TableCellStyle).AlignRight().Text(entry.Amount.ToString("N2"));
                            }
                        });
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public static byte[] CreateRefundReceipt(RefundRequest request)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(style => style.FontFamily("Tahoma").FontSize(10));
                page.Header().Column(header =>
                {
                    header.Item().AlignCenter().Text("Games Gift Shop").FontSize(20).Bold();
                    header.Item().AlignCenter().Text("ใบเสร็จคืนเงิน").FontSize(16).Bold();
                    header.Item().AlignCenter().Text($"เลขที่ RR-{request.RefundRequestId:D6}");
                });
                page.Content().Column(content =>
                {
                    content.Spacing(8);
                    content.Item().Text($"ออเดอร์: #{request.OrderId:D4}").Bold();
                    content.Item().Text($"ลูกค้า: {request.User.Username}");
                    content.Item().Text($"อีเมล: {request.User.Email}");
                    content.Item().Text($"วันที่สั่งซื้อ: {request.Order.OrderDate?.ToString("dd/MM/yyyy HH:mm") ?? "-"}");
                    content.Item().Text($"วันที่ยืนยันคืนเงิน: {request.ReviewedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-"}");
                    content.Item().PaddingTop(8).Text("รายการสินค้า").FontSize(12).Bold();
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(TableCellStyle).Text("สินค้า").SemiBold();
                            header.Cell().Element(TableCellStyle).AlignRight().Text("จำนวน").SemiBold();
                            header.Cell().Element(TableCellStyle).AlignRight().Text("ราคาต่อชิ้น").SemiBold();
                            header.Cell().Element(TableCellStyle).AlignRight().Text("รวม").SemiBold();
                        });

                        foreach (var detail in request.Order.Orderdetails)
                        {
                            var productName = detail.Product?.Name ?? $"สินค้า #{detail.ProductId}";
                            if (!string.IsNullOrWhiteSpace(detail.Size))
                                productName += $" (ไซซ์ {detail.Size})";

                            table.Cell().Element(TableCellStyle).Text(productName);
                            table.Cell().Element(TableCellStyle).AlignRight().Text(detail.Quantity.ToString());
                            table.Cell().Element(TableCellStyle).AlignRight().Text(detail.Price.ToString("N2"));
                            table.Cell().Element(TableCellStyle).AlignRight().Text((detail.Price * detail.Quantity).ToString("N2"));
                        }
                    });
                    content.Item().AlignRight()
                        .Text($"ยอดคืนเงิน: ฿{request.Order.TotalAmount:N2}")
                        .FontSize(14)
                        .Bold();
                    content.Item().PaddingTop(8).Text($"เหตุผลที่ขอคืนเงิน: {request.Reason}");
                    if (!string.IsNullOrWhiteSpace(request.Feedback))
                        content.Item().Text($"ผลการพิจารณา: {request.Feedback}");
                    content.Item().PaddingTop(12).Text("เอกสารนี้ออกตามการบันทึกผลคืนเงินในระบบ ไม่ใช่หลักฐานการโอนจากธนาคารหรือผู้ให้บริการชำระเงิน");
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static IContainer TableCellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(4)
            .PaddingHorizontal(3);
    }
}
