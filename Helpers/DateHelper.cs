namespace WebApplication1.Helpers
{
    public static class DateHelper
    {
        public static DateTime NowForTimestamp()
          => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }
}
