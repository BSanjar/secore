using System.Globalization;

namespace WebApplication1.Helpers
{
    public static class ParsersHelper
    {
        public static DateTime NowForTimestamp()
          => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        /// <summary>Сумма в БД (тыйын) → сом для UI и печати.</summary>
        public static decimal TyiynToSom(decimal tyiyn) => tyiyn / 100m;

        public static decimal? TyiynToSom(decimal? tyiyn) =>
            tyiyn.HasValue ? TyiynToSom(tyiyn.Value) : null;

        /// <summary>Сом из формы → тыйын для сохранения в БД.</summary>
        public static decimal SomToTyiyn(decimal som) => decimal.Round(som * 100m, 0, MidpointRounding.AwayFromZero);

        public static decimal? SomToTyiyn(decimal? som) =>
            som.HasValue ? SomToTyiyn(som.Value) : null;

        public static string ToMoneyStringFromCents(decimal? value)
        {
            if (value == 0)
                return "0";

            return TyiynToSom(value)?.ToString("0.00", CultureInfo.InvariantCulture);
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
