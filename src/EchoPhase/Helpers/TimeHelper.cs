using Microsoft.Extensions.Localization;

namespace EchoPhase.Helpers
{
    public static class TimeHelper
    {
        public static string GetTimeSince(DateTime date, IStringLocalizer localizer)
        {
            var timeSpan = DateTime.UtcNow - date;
            string result = "";

            if (timeSpan.TotalMinutes < 1)
            {
                int seconds = (int)timeSpan.TotalSeconds;
                result = seconds == 1
                    ? string.Format(localizer["SecondsAgo_Singular"], seconds)
                    : string.Format(localizer["SecondsAgo_Plural"], seconds);
            }
            else if (timeSpan.TotalHours < 1)
            {
                int minutes = (int)timeSpan.TotalMinutes;
                result = minutes == 1
                    ? string.Format(localizer["MinutesAgo_Singular"], minutes)
                    : string.Format(localizer["MinutesAgo_Plural"], minutes);
            }
            else if (timeSpan.TotalDays < 1)
            {
                int hours = (int)timeSpan.TotalHours;
                result = hours == 1
                    ? string.Format(localizer["HoursAgo_Singular"], hours)
                    : string.Format(localizer["HoursAgo_Plural"], hours);
            }
            else if (timeSpan.TotalDays < 30)
            {
                int days = (int)timeSpan.TotalDays;
                result = days == 1
                    ? string.Format(localizer["DaysAgo_Singular"], days)
                    : string.Format(localizer["DaysAgo_Plural"], days);
            }
            else if (timeSpan.TotalDays < 365)
            {
                int months = (int)(timeSpan.TotalDays / 30);
                result = months == 1
                    ? string.Format(localizer["MonthsAgo_Singular"], months)
                    : string.Format(localizer["MonthsAgo_Plural"], months);
            }
            else
            {
                int years = (int)(timeSpan.TotalDays / 365);
                result = years == 1
                    ? string.Format(localizer["YearsAgo_Singular"], years)
                    : string.Format(localizer["YearsAgo_Plural"], years);
            }

            return result;
        }
    }
}
