using System.Globalization;

namespace WebApplication1.Helpers
{
    public static class ParsersHelper
    {
        public static DateTime NowForTimestamp()
          => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        /// <summary>
        /// Timestamps in DB are UTC stored as Unspecified — convert for UI display in local time.
        /// </summary>
        public static DateTime? ToLocalDisplayTime(DateTime? value)
        {
            if (value == null)
                return null;

            var dt = value.Value;
            return dt.Kind switch
            {
                DateTimeKind.Local => dt,
                DateTimeKind.Utc => dt.ToLocalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime()
            };
        }

        public static string FormatTimestampForDisplay(DateTime? value, string format = "dd.MM.yyyy HH:mm")
        {
            var local = ToLocalDisplayTime(value);
            return local?.ToString(format, CultureInfo.GetCultureInfo("ru-RU")) ?? "—";
        }

        public static (string DateLabel, string TimeLabel) FormatTimestampParts(DateTime? value, bool compactDate = false)
        {
            var local = ToLocalDisplayTime(value);
            if (local == null)
                return ("—", "—");

            var culture = CultureInfo.GetCultureInfo("ru-RU");
            var dateFormat = compactDate ? "d MMM yyyy" : "d MMMM yyyy";
            return (
                local.Value.ToString(dateFormat, culture),
                local.Value.ToString("HH:mm", culture));
        }

        public static (string DateLabel, string TimeLabel) FormatNowParts(bool compactDate = false)
            => FormatTimestampParts(NowForTimestamp(), compactDate);

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
