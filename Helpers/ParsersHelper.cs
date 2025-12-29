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
    }
}
