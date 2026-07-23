using Microsoft.AspNetCore.Http;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Helpers;

/// <summary>
/// Права на отображение сумм на главном экране по каналу оплаты.
/// </summary>
public static class DashboardSumPermissions
{
    public const string QrSecore = "dashboard.sums.qr_secore";
    public const string QrExternal = "dashboard.sums.qr_external";
    public const string Cash = "dashboard.sums.cash";
    public const string Card = "dashboard.sums.card";

    public const string Category = "Дашборд";

    public static readonly IReadOnlyList<(string Code, string Name, string Description)> Definitions =
        new (string, string, string)[]
        {
            (QrSecore, "Суммы: QR SECORE", "Отображать суммы, принятые через QR SECORE"),
            (QrExternal, "Суммы: QR внешний", "Отображать суммы, принятые через внешний QR"),
            (Cash, "Суммы: наличные", "Отображать суммы, принятые через наличные"),
            (Card, "Суммы: карта", "Отображать суммы, принятые через карту")
        };

    public sealed class Visibility
    {
        public bool CanViewQrSecore { get; init; }
        public bool CanViewQrExternal { get; init; }
        public bool CanViewCash { get; init; }
        public bool CanViewCard { get; init; }

        public bool CanViewAny =>
            CanViewQrSecore || CanViewQrExternal || CanViewCash || CanViewCard;

        public IReadOnlyList<string> AllowedTransactionTypes { get; init; } = Array.Empty<string>();

        public bool IsTransactionTypeAllowed(string? transactionType)
        {
            if (string.IsNullOrWhiteSpace(transactionType))
                return false;

            return AllowedTransactionTypes.Contains(transactionType, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static Visibility GetVisibility(HttpContext httpContext)
    {
        var canSecore = PermissionHelper.HasPermission(httpContext, QrSecore);
        var canExternal = PermissionHelper.HasPermission(httpContext, QrExternal);
        var canCash = PermissionHelper.HasPermission(httpContext, Cash);
        var canCard = PermissionHelper.HasPermission(httpContext, Card);

        var types = new List<string>();
        if (canSecore)
            types.Add("payFromAPI");

        if (canExternal)
            types.Add("payFromExternalQR");
        if (canCash)
            types.Add("payFromCash");
        if (canCard)
            types.Add("payFromExternalCard");

        return new Visibility
        {
            CanViewQrSecore = canSecore,
            CanViewQrExternal = canExternal,
            CanViewCash = canCash,
            CanViewCard = canCard,
            AllowedTransactionTypes = types
        };
    }

    public static IQueryable<Transaction> ApplyDashboardSumFilter(
        IQueryable<Transaction> query,
        Visibility visibility)
    {
        if (!visibility.CanViewAny)
            return query.Where(_ => false);

        var allowed = visibility.AllowedTransactionTypes;
        return query.Where(t =>
            t.TransactionType != null &&
            (
                (t.TransactionType != "payPaymentInvoice" &&
                 t.TransactionType != "credit" &&
                 allowed.Contains(t.TransactionType)) ||
                (t.TransactionType == "credit" &&
                 t.ParentTransactionNavigation != null &&
                 (
                     t.ParentTransactionNavigation.TransactionType == "payPaymentInvoice" ||
                     (allowed.Contains(t.ParentTransactionNavigation.TransactionType!) &&
                      t.ParentTransactionNavigation.TransactionType != "payPaymentInvoice")
                 ))
            ));
    }

    /// <summary>
    /// Внутреннее разнесение по счёту (не отдельный платёж для пользователя).
    /// </summary>
    public static bool IsInternalPaymentLedgerType(string? transactionType) =>
        string.Equals(transactionType, "payPaymentInvoice", StringComparison.OrdinalIgnoreCase);
}
