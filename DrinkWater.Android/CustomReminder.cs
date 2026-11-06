using System.Globalization;

namespace DrinkWater.Android;

public sealed class CustomReminder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<string> Times { get; set; } = [];
    public List<DayOfWeek> Days { get; set; } = [];

    public void Validate()
    {
        if (!Guid.TryParseExact(Id, "N", out _) || string.IsNullOrWhiteSpace(Title) || Title.Length > 100 ||
            Note is null || Note.Length > 500 || Times is null || Times.Count is < 1 or > 16 ||
            Times.Distinct().Count() != Times.Count || Times.Any(t => !TryTime(t, out _)) ||
            Days is null || Days.Count is < 1 or > 7 || Days.Distinct().Count() != Days.Count ||
            Days.Any(d => !Enum.IsDefined(d)))
            throw new InvalidDataException("Vypln nazov, platne casy HH:mm a aspon jeden den. Casy sa nesmu opakovat.");
    }

    public static bool TryTime(string? value, out TimeSpan time) =>
        TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time);
}

public static class CustomReminderPlan
{
    // Calendar days preserve the selected local time across daylight-saving changes.
    public static DateTime? Next(CustomReminder reminder, DateTime now, TimeZoneInfo? timeZone = null)
    {
        if (!reminder.Enabled) return null;
        timeZone ??= TimeZoneInfo.Local;
        for (var offset = 0; offset <= 7; offset++)
        {
            var day = now.Date.AddDays(offset);
            if (!reminder.Days.Contains(day.DayOfWeek)) continue;
            var candidates = new List<DateTime>();
            foreach (var time in reminder.Times)
            {
                if (!CustomReminder.TryTime(time, out var parsed)) continue;
                var candidate = day.Add(parsed);
                // A time skipped by the spring clock change moves to the first valid minute.
                while (timeZone.IsInvalidTime(candidate)) candidate = candidate.AddMinutes(1);
                if (candidate > now) candidates.Add(candidate);
            }
            if (candidates.Count > 0) return candidates.Min();
        }
        return null;
    }
}
