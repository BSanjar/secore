using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using WebApplication1.Dtos;

namespace WebApplication1.Services;

public class ExcelExportService
{
    public byte[] ExportTransactions(IEnumerable<TransactionExcelRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Транзакции");

        ws.Cell(1, 1).Value = "Дата";
        ws.Cell(1, 2).Value = "ФИО клиента";
        ws.Cell(1, 3).Value = "Лицевой счёт";
        ws.Cell(1, 4).Value = "Агент";
        ws.Cell(1, 5).Value = "Сумма (сом)";
        ws.Cell(1, 6).Value = "Итого (сом)";
        ws.Cell(1, 7).Value = "Статус";

        var header = ws.Range(1, 1, 1, 7);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;

        var statusText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = "Успешно",
            ["error"] = "Ошибка"
        };

        int r = 2;
        foreach (var row in rows ?? Enumerable.Empty<TransactionExcelRow>())
        {
            ws.Cell(r, 1).Value = row.Date?.ToString("dd.MM.yyyy HH:mm") ?? "";
            ws.Cell(r, 2).Value = row.ClientName ?? "";
            ws.Cell(r, 3).Value = row.PayCode ?? "";
            ws.Cell(r, 4).Value = row.AgentName ?? "";
            ws.Cell(r, 5).Value = row.AmountTyiyn.HasValue ? (double)(row.AmountTyiyn.Value / 100m) : 0;
            ws.Cell(r, 6).Value = row.TotalTyiyn.HasValue ? (double)(row.TotalTyiyn.Value / 100m) : 0;
            ws.Cell(r, 7).Value = statusText.TryGetValue(row.Status ?? "", out var st) ? st : (row.Status ?? "");
            r++;
        }

        ws.Columns().AdjustToContents();
        return Save(workbook);
    }

    public byte[] ExportInvoicePaymentsAll(IEnumerable<InvoicePaymentExcelRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Платежи по счетам");

        ws.Cell(1, 1).Value = "Номер счета";
        ws.Cell(1, 2).Value = "Лицевой счет (PayCode)";
        ws.Cell(1, 3).Value = "Клиент";
        ws.Cell(1, 4).Value = "Период с";
        ws.Cell(1, 5).Value = "Период по";
        ws.Cell(1, 6).Value = "Значение периода";
        ws.Cell(1, 7).Value = "Сумма (сом)";
        ws.Cell(1, 8).Value = "Статус платежа";

        var header = ws.Range(1, 1, 1, 8);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;

        var statusText = PaymentStatusText();

        int r = 2;
        foreach (var row in rows ?? Enumerable.Empty<InvoicePaymentExcelRow>())
        {
            ws.Cell(r, 1).Value = row.InvoiceId ?? "";
            ws.Cell(r, 2).Value = row.PayCode ?? "";
            ws.Cell(r, 3).Value = row.ClientName ?? "";
            ws.Cell(r, 4).Value = row.PeriodFrom.HasValue ? row.PeriodFrom.Value.ToString("dd.MM.yyyy") : "";
            ws.Cell(r, 5).Value = row.PeriodTo.HasValue ? row.PeriodTo.Value.ToString("dd.MM.yyyy") : "";
            ws.Cell(r, 6).Value = row.PeriodValue ?? "";
            ws.Cell(r, 7).Value = row.AmountTyiyn.HasValue ? (double)(row.AmountTyiyn.Value / 100m) : 0;
            ws.Cell(r, 8).Value = statusText.TryGetValue(row.PaymentStatus ?? "", out var ps) ? ps : (row.PaymentStatus ?? "");
            r++;
        }

        ws.Columns().AdjustToContents();
        return Save(workbook);
    }

    public byte[] ExportInvoicePaymentsSingle(IEnumerable<InvoicePaymentExcelRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Платежи по счёту");

        ws.Cell(1, 1).Value = "Период с";
        ws.Cell(1, 2).Value = "Период по";
        ws.Cell(1, 3).Value = "Значение периода";
        ws.Cell(1, 4).Value = "Сумма (сом)";
        ws.Cell(1, 5).Value = "Статус платежа";

        var header = ws.Range(1, 1, 1, 5);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;

        var statusText = PaymentStatusText();

        int r = 2;
        var any = false;
        foreach (var row in rows ?? Enumerable.Empty<InvoicePaymentExcelRow>())
        {
            any = true;
            ws.Cell(r, 1).Value = row.PeriodFrom.HasValue ? row.PeriodFrom.Value.ToString("dd.MM.yyyy") : "";
            ws.Cell(r, 2).Value = row.PeriodTo.HasValue ? row.PeriodTo.Value.ToString("dd.MM.yyyy") : "";
            ws.Cell(r, 3).Value = row.PeriodValue ?? "";
            ws.Cell(r, 4).Value = row.AmountTyiyn.HasValue ? (double)(row.AmountTyiyn.Value / 100m) : 0;
            ws.Cell(r, 5).Value = statusText.TryGetValue(row.PaymentStatus ?? "", out var ps) ? ps : (row.PaymentStatus ?? "");
            r++;
        }

        if (!any)
        {
            ws.Cell(r, 1).Value = "";
            ws.Cell(r, 2).Value = "";
            ws.Cell(r, 3).Value = "";
            ws.Cell(r, 4).Value = "";
            ws.Cell(r, 5).Value = "Нет записей";
        }

        ws.Columns().AdjustToContents();
        return Save(workbook);
    }

    private static Dictionary<string, string> PaymentStatusText()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["paid"] = "Оплачено",
            ["non_paid"] = "Не оплачено",
            ["anulated"] = "Аннулировано"
        };

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream, false);
        return stream.ToArray();
    }
}

