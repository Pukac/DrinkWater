using System.Globalization;

namespace DrinkWater.Android;

public static class EveningReminderPlan
{
    public static DateTime? Next(DateTime now, string time, bool goalReached, string lastShownDate)
    {
        if (!TimeSpan.TryParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture, out var eveningTime))
            return null;
        var evening = now.Date.Add(eveningTime);
        return evening <= now || goalReached || lastShownDate == now.ToString("yyyy-MM-dd")
            ? evening.AddDays(1) : evening;
    }

    public static bool IsDue(DateTime now, DateTime scheduled, string lastShownDate) =>
        scheduled <= now && scheduled.Date == now.Date && lastShownDate != now.ToString("yyyy-MM-dd");
}
