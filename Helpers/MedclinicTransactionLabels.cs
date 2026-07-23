using WebApplication1.Models.DBModels;

namespace WebApplication1.Helpers;

public static class MedclinicTransactionLabels
{
    public static string ResolveEffectiveType(Transaction transaction)
    {
        if (string.Equals(transaction.TransactionType, "credit", StringComparison.OrdinalIgnoreCase) &&
            transaction.ParentTransactionNavigation?.TransactionType != null)
        {
            return transaction.ParentTransactionNavigation.TransactionType;
        }

        return transaction.TransactionType ?? string.Empty;
    }

    public static string ChannelLabel(string? transactionType)
    {
        var type = (transactionType ?? string.Empty).Trim();
        return type switch
        {
            "payFromAPI" or "payPaymentInvoice" => "QR SECORE",
            "payFromExternalQR" => "QR внешний",
            "payFromCash" => "Наличные",
            "payFromExternalCard" => "Карта",
            _ => string.IsNullOrEmpty(type) ? "—" : type
        };
    }

    public static string ChannelLabel(Transaction transaction) =>
        ChannelLabel(ResolveEffectiveType(transaction));

    public static string KindLabel(Transaction transaction) =>
        string.Equals(transaction.TransactionType, "credit", StringComparison.OrdinalIgnoreCase)
            ? "Возврат"
            : "Оплата";

    public static IReadOnlyList<(string Key, string Label)> ChannelFilterOptions(DashboardSumPermissions.Visibility visibility)
    {
        var list = new List<(string, string)>();
        if (visibility.CanViewQrSecore)
            list.Add(("qr_secore", "QR SECORE"));
        if (visibility.CanViewQrExternal)
            list.Add(("qr_external", "QR внешний"));
        if (visibility.CanViewCash)
            list.Add(("cash", "Наличные"));
        if (visibility.CanViewCard)
            list.Add(("card", "Карта"));
        return list;
    }

    public static bool MatchesChannelFilter(Transaction transaction, string? channelFilter)
    {
        if (string.IsNullOrWhiteSpace(channelFilter))
            return true;

        var effective = ResolveEffectiveType(transaction);
        return channelFilter.Trim().ToLowerInvariant() switch
        {
            "qr_secore" => effective is "payFromAPI" or "payPaymentInvoice",
            "qr_external" => string.Equals(effective, "payFromExternalQR", StringComparison.OrdinalIgnoreCase),
            "cash" => string.Equals(effective, "payFromCash", StringComparison.OrdinalIgnoreCase),
            "card" => string.Equals(effective, "payFromExternalCard", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}
