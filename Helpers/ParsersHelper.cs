using System.Globalization;

namespace WebApplication1.Helpers
{
    public static class ParsersHelper
    {
        public static DateTime NowForTimestamp()
          => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        public static string ToMoneyStringFromCents(decimal? value)
        {
            if (value == 0)
                return "0";

            return (value / 100m)?.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static decimal? FromMoneyStringToCents(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
            {                                
                    return decimalValue * 100m;                
            }

            return null;
        }

        public static decimal? ParsePaymentSumm(string? paymentSumm)
        {
            if (string.IsNullOrWhiteSpace(paymentSumm))
                return null;

            if (decimal.TryParse(paymentSumm, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return value;

            return null;
        }
    }
}
