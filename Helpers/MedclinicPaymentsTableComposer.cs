using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.ViewModels.Payments;

namespace WebApplication1.Helpers;

public static class MedclinicPaymentsTableComposer
{
    public sealed class ComposeResult
    {
        public IReadOnlyList<MedclinicPaymentRowViewModel> Rows { get; init; } =
            Array.Empty<MedclinicPaymentRowViewModel>();

        public int GroupCount { get; init; }
    }

    public static IEnumerable<string?> CollectPaymentInvoiceIds(IReadOnlyList<Transaction> transactions)
    {
        var paymentInvoiceByTxnId = transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.TxnId) && !string.IsNullOrWhiteSpace(t.PaymentInvoice))
            .GroupBy(t => t.TxnId!)
            .ToDictionary(g => g.Key, g => g.First().PaymentInvoice!);

        return transactions.Select(t => ResolvePaymentInvoiceId(t, paymentInvoiceByTxnId));
    }

    public static ComposeResult Compose(
        IReadOnlyList<Transaction> transactions,
        AppointmentLinksResult appointmentLinks,
        string registryOpenUrlTemplate)
    {
        if (transactions.Count == 0)
            return new ComposeResult();

        var visibleById = transactions.ToDictionary(t => t.Id);
        var paymentInvoiceByTxnId = transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.TxnId) && !string.IsNullOrWhiteSpace(t.PaymentInvoice))
            .GroupBy(t => t.TxnId!)
            .ToDictionary(g => g.Key, g => g.First().PaymentInvoice!);

        var rowById = new Dictionary<string, MedclinicPaymentRowViewModel>(StringComparer.Ordinal);
        foreach (var t in transactions)
        {
            rowById[t.Id] = MapRow(t, appointmentLinks, paymentInvoiceByTxnId, registryOpenUrlTemplate);
        }

        var payments = transactions
            .Where(t => !IsCredit(t))
            .OrderByDescending(t => t.TransactionDate ?? DateTime.MinValue)
            .ThenByDescending(t => t.Id, StringComparer.Ordinal)
            .ToList();

        var credits = transactions.Where(IsCredit).ToList();
        var creditsByAnchor = credits
            .GroupBy(c => ResolveRefundAnchorId(c, visibleById), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.TransactionDate ?? DateTime.MinValue).ToList(),
                StringComparer.Ordinal);

        var attachedCreditIds = new HashSet<string>(StringComparer.Ordinal);
        var flat = new List<MedclinicPaymentRowViewModel>();
        var groupCount = 0;

        foreach (var payment in payments)
        {
            groupCount++;
            var paymentRow = rowById[payment.Id];
            flat.Add(paymentRow);

            if (!creditsByAnchor.TryGetValue(payment.Id, out var refunds))
                continue;

            paymentRow.IsHighlightedGroup = true;
            foreach (var refundTx in refunds)
            {
                attachedCreditIds.Add(refundTx.Id);
                var child = rowById[refundTx.Id];
                child.IsGroupedChild = true;
                child.IsHighlightedGroup = true;
                flat.Add(child);
            }
        }

        foreach (var credit in credits.Where(c => !attachedCreditIds.Contains(c.Id)))
        {
            groupCount++;
            var row = rowById[credit.Id];
            var anchorId = ResolveRefundAnchorId(credit, visibleById);
            row.IsGroupedChild = visibleById.TryGetValue(anchorId, out var anchor) && !IsCredit(anchor);
            flat.Add(row);
        }

        var sequence = 0;
        foreach (var row in flat)
        {
            if (row.IsGroupedChild)
                continue;
            sequence++;
            row.PaymentSequence = sequence;
        }

        return new ComposeResult
        {
            Rows = flat,
            GroupCount = groupCount
        };
    }

    public sealed class AppointmentLinksResult
    {
        public IReadOnlyDictionary<string, AppointmentLinkInfo> ByPaymentInvoiceId { get; init; } =
            new Dictionary<string, AppointmentLinkInfo>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, AppointmentLinkInfo> ByInvoiceId { get; init; } =
            new Dictionary<string, AppointmentLinkInfo>(StringComparer.Ordinal);
    }

    public sealed class AppointmentLinkInfo
    {
        public string AppointmentId { get; init; } = string.Empty;
        public string Label { get; init; } = "—";
    }

    public static async Task<AppointmentLinksResult> LoadAppointmentLinksAsync(
        AppDbContext db,
        string organizationId,
        IEnumerable<string?> paymentInvoiceIds,
        CancellationToken cancellationToken = default)
    {
        var empty = new AppointmentLinksResult();
        var ids = paymentInvoiceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
            return empty;

        var payments = await db.InvoicePayments
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Invoice,
                p.DateFrom,
                p.DateTo,
                ClientId = p.InvoiceNavigation != null ? p.InvoiceNavigation.Client : null
            })
            .ToListAsync(cancellationToken);

        var clientIds = payments
            .Select(p => p.ClientId)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (clientIds.Count == 0)
            return empty;

        var appointments = await db.Appointments
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && a.PatientId != null && clientIds.Contains(a.PatientId))
            .Select(a => new
            {
                a.Id,
                a.PatientId,
                a.StartsAt,
                a.EndsAt,
                a.Title,
                DoctorName = a.Doctor != null ? a.Doctor.Name : null
            })
            .ToListAsync(cancellationToken);

        var ru = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        var byPayment = new Dictionary<string, AppointmentLinkInfo>(StringComparer.Ordinal);
        var byInvoice = new Dictionary<string, AppointmentLinkInfo>(StringComparer.Ordinal);

        static bool SameScheduleSlot(DateTime? appointmentTime, DateTime? paymentTime)
        {
            if (!appointmentTime.HasValue || !paymentTime.HasValue)
                return false;

            var a = appointmentTime.Value;
            var p = paymentTime.Value;
            if (a.Date != p.Date)
                return false;

            return a.Hour == p.Hour && a.Minute == p.Minute;
        }

        static bool SameEndSlot(DateTime? appointmentEnd, DateTime? paymentEnd)
        {
            if (!appointmentEnd.HasValue || !paymentEnd.HasValue)
                return true;

            return SameScheduleSlot(appointmentEnd, paymentEnd);
        }

        foreach (var payment in payments)
        {
            if (string.IsNullOrWhiteSpace(payment.ClientId) || payment.DateFrom == null)
                continue;

            var match = appointments.FirstOrDefault(a =>
                a.PatientId == payment.ClientId &&
                SameScheduleSlot(a.StartsAt, payment.DateFrom) &&
                SameEndSlot(a.EndsAt, payment.DateTo));

            if (match == null)
            {
                match = appointments
                    .Where(a => a.PatientId == payment.ClientId && payment.DateFrom.HasValue &&
                                a.StartsAt.Date == payment.DateFrom.Value.Date)
                    .OrderBy(a => Math.Abs((a.StartsAt - payment.DateFrom.Value).TotalMinutes))
                    .FirstOrDefault(a => Math.Abs((a.StartsAt - payment.DateFrom!.Value).TotalMinutes) <= 5);
            }

            if (match == null)
                continue;

            var datePart = payment.DateFrom.Value.ToString("dd.MM.yyyy", ru);
            var timePart = $"{payment.DateFrom.Value:HH:mm}–{payment.DateTo?.ToString("HH:mm") ?? "—"}";
            var doctorPart = string.IsNullOrWhiteSpace(match.DoctorName) ? null : match.DoctorName;
            var title = string.IsNullOrWhiteSpace(match.Title) ? "Приём" : match.Title.Trim();
            var label = doctorPart != null
                ? $"{datePart} · {timePart} · {doctorPart}"
                : $"{title} · {datePart} · {timePart}";

            var link = new AppointmentLinkInfo
            {
                AppointmentId = match.Id,
                Label = label
            };

            byPayment[payment.Id] = link;
            if (!string.IsNullOrWhiteSpace(payment.Invoice))
                byInvoice[payment.Invoice] = link;
        }

        return new AppointmentLinksResult
        {
            ByPaymentInvoiceId = byPayment,
            ByInvoiceId = byInvoice
        };
    }

    static MedclinicPaymentRowViewModel MapRow(
        Transaction t,
        AppointmentLinksResult appointmentLinks,
        IReadOnlyDictionary<string, string> paymentInvoiceByTxnId,
        string registryOpenUrlTemplate)
    {
        var isRefund = IsCredit(t);
        var isSuccess = string.Equals(t.TransactionStatus, "success", StringComparison.OrdinalIgnoreCase);
        var paymentInvoiceId = ResolvePaymentInvoiceId(t, paymentInvoiceByTxnId);
        AppointmentLinkInfo? appt = null;
        if (!string.IsNullOrWhiteSpace(paymentInvoiceId))
            appointmentLinks.ByPaymentInvoiceId.TryGetValue(paymentInvoiceId, out appt);
        if (appt == null && !string.IsNullOrWhiteSpace(t.Invoice))
            appointmentLinks.ByInvoiceId.TryGetValue(t.Invoice, out appt);

        var appointmentUrl = appt != null && !isRefund
            ? registryOpenUrlTemplate.Replace("{0}", Uri.EscapeDataString(appt.AppointmentId), StringComparison.Ordinal)
            : null;

        return new MedclinicPaymentRowViewModel
        {
            Id = t.Id,
            TransactionDate = t.TransactionDate,
            PatientName = t.InvoiceNavigation?.ClientNavigation?.ClientName ?? "—",
            PayCode = t.InvoiceNavigation?.PayCode ?? "—",
            ChannelLabel = MedclinicTransactionLabels.ChannelLabel(t),
            PaymentTypeLabel = MedclinicTransactionLabels.PaymentTypeLabel(t),
            KindLabel = MedclinicTransactionLabels.KindLabel(t),
            AmountSom = (t.Summ ?? 0m) / 100m,
            StatusLabel = t.TransactionStatus == "success"
                ? (isRefund ? "Возврат" : "Успешно")
                : t.TransactionStatus == "error"
                    ? "Ошибка"
                    : t.TransactionStatus ?? "—",
            IsRefund = isRefund,
            IsSuccess = isSuccess,
            IsGroupedChild = false,
            AppointmentId = appt?.AppointmentId,
            AppointmentLabel = appt?.Label ?? "—",
            AppointmentUrl = appointmentUrl
        };
    }

    static string? ResolvePaymentInvoiceId(Transaction t, IReadOnlyDictionary<string, string> paymentInvoiceByTxnId)
    {
        if (!string.IsNullOrWhiteSpace(t.PaymentInvoice))
            return t.PaymentInvoice;

        if (!string.IsNullOrWhiteSpace(t.TxnId) && paymentInvoiceByTxnId.TryGetValue(t.TxnId, out var fromTxn))
            return fromTxn;

        var source = t.ParentTransactionNavigation;
        if (source != null)
        {
            if (!string.IsNullOrWhiteSpace(source.PaymentInvoice))
                return source.PaymentInvoice;

            if (!string.IsNullOrWhiteSpace(source.TxnId) && paymentInvoiceByTxnId.TryGetValue(source.TxnId, out var fromParentTxn))
                return fromParentTxn;
        }

        return null;
    }

    public static async Task<IReadOnlyList<string?>> ExpandPaymentInvoiceIdsAsync(
        AppDbContext db,
        IReadOnlyList<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        var ids = CollectPaymentInvoiceIds(transactions)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        var txnIds = transactions
            .Select(t => t.TxnId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (txnIds.Count > 0)
        {
            var fromTxn = await db.Transactions
                .AsNoTracking()
                .Where(t => t.TxnId != null && txnIds.Contains(t.TxnId) && t.PaymentInvoice != null)
                .Select(t => t.PaymentInvoice!)
                .ToListAsync(cancellationToken);
            foreach (var id in fromTxn)
                ids.Add(id);
        }

        var invoiceIds = transactions
            .Select(t => t.Invoice)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (invoiceIds.Count > 0)
        {
            var fromInvoices = await db.InvoicePayments
                .AsNoTracking()
                .Where(p =>
                    p.Invoice != null &&
                    invoiceIds.Contains(p.Invoice) &&
                    p.InvoiceNavigation != null &&
                    p.InvoiceNavigation.FromAppointments)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            foreach (var id in fromInvoices)
                ids.Add(id);
        }

        return ids.ToList();
    }

    static string ResolveRefundAnchorId(Transaction credit, IReadOnlyDictionary<string, Transaction> visibleById)
    {
        var node = credit.ParentTransactionNavigation;
        var safety = 0;
        while (node != null && safety++ < 8)
        {
            if (visibleById.TryGetValue(node.Id, out var visible) && !IsCredit(visible))
                return visible.Id;

            if (DashboardSumPermissions.IsInternalPaymentLedgerType(node.TransactionType))
            {
                node = node.ParentTransactionNavigation;
                continue;
            }

            if (visibleById.ContainsKey(node.Id))
                return node.Id;

            break;
        }

        if (credit.ParentTransaction != null && visibleById.ContainsKey(credit.ParentTransaction))
            return credit.ParentTransaction;

        return credit.Id;
    }

    static bool IsCredit(Transaction t) =>
        string.Equals(t.TransactionType, "credit", StringComparison.OrdinalIgnoreCase);
}
