using Android.Content;
using System.Text.Json;

namespace DrinkWater.Android;

public sealed class AppState
{
    public string LastTrackedDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public int ConsumedMl { get; set; }
    public bool RemindersEnabled { get; set; }
    public bool EveningReminder { get; set; }
    public string EveningReminderTime { get; set; } = "20:00";
    public string LastEveningReminderDate { get; set; } = string.Empty;
    public long NextReminderUtc { get; set; }
    public int GoalMl { get; set; } = 2200;
    public int RemindersPerDay { get; set; } = 8;
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "20:00";
    public string Name { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string Sex { get; set; } = "female";
    public double WeightKg { get; set; } = 70;
    public double HeightCm { get; set; } = 170;
    public string Activity { get; set; } = "medium";
    public string Climate { get; set; } = "temperate";
    public int WorkoutMinutes { get; set; } = 30;
    public string SweatLevel { get; set; } = "medium";
    public int CoffeeCups { get; set; }
    public int AlcoholServings { get; set; }
    public bool IsPregnantOrBreastfeeding { get; set; }
    public bool HasKidneyCondition { get; set; }
    public bool HasHeartCondition { get; set; }
    public bool HasFluidRestriction { get; set; }
    public string BodySignal { get; set; } = "balanced";
    public string Dosha { get; set; } = "mixed";
    public int RecommendedGoalMl { get; set; } = 2200;
    public int RecommendedRemindersPerDay { get; set; } = 8;
    public string ElementProfile { get; set; } = string.Empty;
    public string RecommendationNotes { get; set; } = string.Empty;
    public string RecommendationBreakdown { get; set; } = string.Empty;
    public string SafetyMessage { get; set; } = string.Empty;
    public List<string> Schedule { get; set; } = [];
    public List<string> ActiveReminderSchedule { get; set; } = [];
    public string ActiveReminderSummary { get; set; } = string.Empty;
    public List<DailyHydrationRecord> History { get; set; } = [];

    public static AppState CreateDefault()
    {
        var state = new AppState();
        state.Schedule = HydrationCalculator.BuildSchedule(state.StartTime, state.EndTime, state.RemindersPerDay);
        state.SyncTodayRecord(resetConsumed: true);
        return state;
    }

    public void ResetForToday(bool forceReset = false)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (forceReset || !string.Equals(LastTrackedDate, today, StringComparison.Ordinal))
        {
            LastTrackedDate = today;
            ConsumedMl = 0;
            ActiveReminderSchedule = [];
            ActiveReminderSummary = string.Empty;
        }

        SyncTodayRecord(resetConsumed: forceReset);
    }

    public void AddConsumed(int amountMl)
    {
        ResetForToday();
        ConsumedMl += amountMl;
        SyncTodayRecord();

        var today = LastTrackedDate;
        var record = History.FirstOrDefault(item => item.Date == today);
        record?.Events.Add(new HydrationEvent
        {
            Time = DateTime.Now.ToString("HH:mm"),
            AmountMl = amountMl
        });
    }

    public void SyncTodayRecord(bool resetConsumed = false)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        LastTrackedDate = today;

        var record = History.FirstOrDefault(item => item.Date == today);
        if (record is null)
        {
            record = new DailyHydrationRecord
            {
                Date = today,
                GoalMl = GoalMl,
                ConsumedMl = resetConsumed ? 0 : ConsumedMl,
                Events = []
            };
            History.Add(record);
        }
        else
        {
            if (resetConsumed)
            {
                record.ConsumedMl = 0;
                ConsumedMl = 0;
                record.Events = [];
            }

            record.GoalMl = GoalMl;
            record.ConsumedMl = ConsumedMl;
        }

        History = History
            .OrderByDescending(item => item.Date)
            .Take(120)
            .OrderBy(item => item.Date)
            .ToList();
    }

    public HydrationStats CalculateStats(int days)
    {
        var today = DateTime.Today;
        var startDate = today.AddDays(-(days - 1));

        var relevant = History
            .Where(item => DateTime.TryParse(item.Date, out var parsed) && parsed.Date >= startDate && parsed.Date <= today)
            .OrderBy(item => item.Date)
            .ToList();

        var totalConsumed = relevant.Sum(item => item.ConsumedMl);
        var averagePerDay = days > 0 ? (int)Math.Round(totalConsumed / (double)days) : 0;
        var activeDays = relevant.Count(item => item.ConsumedMl > 0);
        var goalHitDays = relevant.Count(item => item.GoalMl > 0 && item.ConsumedMl >= item.GoalMl);
        var consistencyPercent = days > 0 ? (int)Math.Round(goalHitDays * 100d / days) : 0;
        var streak = CalculateCurrentStreak();
        var trend = CalculateTrend(relevant);
        var segments = CalculateSegments(relevant);
        var chartPoints = Enumerable.Range(0, days).Select(offset =>
        {
            var date = startDate.AddDays(offset);
            var record = relevant.FirstOrDefault(item => item.Date == date.ToString("yyyy-MM-dd"));
            return new ChartPoint(date.ToString("dd.MM"), record?.ConsumedMl ?? 0, record?.GoalMl ?? 0);
        }).ToList();

        return new HydrationStats(days, totalConsumed, averagePerDay, activeDays, goalHitDays, consistencyPercent, streak, trend, segments, chartPoints, relevant);
    }

    public InsightSummary GenerateInsights()
    {
        var stats7 = CalculateStats(7);
        var stats30 = CalculateStats(30);
        var recommendation = BuildSmartRecommendation(stats30);
        return new InsightSummary(stats7, stats30, recommendation);
    }

    public AdaptiveReminderPlan BuildAdaptiveReminderPlan(DateTime now)
    {
        var remainingMl = Math.Max(0, GoalMl - ConsumedMl);
        if (remainingMl <= 0)
        {
            return new AdaptiveReminderPlan([], "Dnesny ciel mas splneny. Dalsie upozornenia nie su potrebne.");
        }

        var endTime = TimeSpan.TryParse(EndTime, out var parsedEnd) ? parsedEnd : new TimeSpan(20, 0, 0);
        var dayEnd = DateTime.Today.Add(parsedEnd);
        if (dayEnd <= now)
        {
            dayEnd = now.AddHours(3);
        }

        var start = now.AddMinutes(15);
        var windowMinutes = Math.Max(45, (int)(dayEnd - start).TotalMinutes);
        var desiredCount = Math.Clamp((int)Math.Ceiling(remainingMl / 280d), 1, 6);

        var stats30 = CalculateStats(30);
        var weakSegment = GetWeakestSegment(stats30.DayParts);
        var preferredTimes = BuildPreferredTimes(start, dayEnd, desiredCount, weakSegment);

        var doseMl = Math.Max(120, (int)Math.Round(remainingMl / (double)desiredCount));
        var items = preferredTimes
            .Select((time, index) => new ReminderPlanItem(
                time.ToString("HH:mm"),
                doseMl,
                BuildReminderMessage(index, desiredCount, weakSegment, remainingMl, doseMl)))
            .ToList();

        var summary = $"Adaptivny plan: ostava {remainingMl} ml, preto appka rozlozila {desiredCount} pripomienok a posilnila usek {DescribeSegment(weakSegment)}.";
        return new AdaptiveReminderPlan(items, summary);
    }

    private int CalculateCurrentStreak()
    {
        var ordered = History
            .Where(item => item.GoalMl > 0)
            .OrderByDescending(item => item.Date)
            .ToList();

        var streak = 0;
        var currentDate = DateTime.Today;
        var todayRecord = ordered.FirstOrDefault(item => item.Date == currentDate.ToString("yyyy-MM-dd"));
        if (todayRecord is null || todayRecord.ConsumedMl < todayRecord.GoalMl)
        {
            currentDate = currentDate.AddDays(-1);
        }

        foreach (var item in ordered)
        {
            if (!DateTime.TryParse(item.Date, out var parsed) || parsed.Date != currentDate)
            {
                if (parsed.Date < currentDate)
                {
                    break;
                }

                continue;
            }

            if (item.ConsumedMl >= item.GoalMl)
            {
                streak++;
                currentDate = currentDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    private static int CalculateTrend(List<DailyHydrationRecord> relevant)
    {
        if (relevant.Count < 4)
        {
            return 0;
        }

        var split = relevant.Count / 2;
        var firstHalf = relevant.Take(split).Average(item => item.ConsumedMl);
        var secondHalf = relevant.Skip(split).Average(item => item.ConsumedMl);
        return (int)Math.Round(secondHalf - firstHalf);
    }

    private static DayPartStats CalculateSegments(List<DailyHydrationRecord> relevant)
    {
        var morning = 0;
        var noon = 0;
        var evening = 0;

        foreach (var record in relevant)
        {
            foreach (var hydrationEvent in record.Events)
            {
                if (!TimeSpan.TryParse(hydrationEvent.Time, out var time))
                {
                    continue;
                }

                if (time < new TimeSpan(11, 0, 0))
                {
                    morning += hydrationEvent.AmountMl;
                }
                else if (time < new TimeSpan(17, 0, 0))
                {
                    noon += hydrationEvent.AmountMl;
                }
                else
                {
                    evening += hydrationEvent.AmountMl;
                }
            }
        }

        return new DayPartStats(morning, noon, evening);
    }

    private static List<ChartPoint> BuildChartPoints(List<DailyHydrationRecord> relevant)
    {
        return relevant
            .Select(item => new ChartPoint(
                item.Date.Length >= 5 ? item.Date[^5..] : item.Date,
                item.ConsumedMl,
                item.GoalMl))
            .ToList();
    }

    private static string BuildSmartRecommendation(HydrationStats stats)
    {
        var timedDays = stats.Records.Count(record => record.Events.Any(e => e.AmountMl > 0 && TimeSpan.TryParse(e.Time, out _)));
        if (timedDays < 3)
        {
            return "Zbierame tvoje zaznamy. Po aspon troch dnoch s casom pitia zobrazime porovnanie casti dna. Chybajuci zaznam neznamena, ze si nepil.";
        }
        var weakestWindow = "rano medzi 8:00-11:00";
        var weakestValue = stats.DayParts.MorningMl;

        if (stats.DayParts.NoonMl < weakestValue)
        {
            weakestWindow = "na obed medzi 11:00-17:00";
            weakestValue = stats.DayParts.NoonMl;
        }

        if (stats.DayParts.EveningMl < weakestValue)
        {
            weakestWindow = "vecer medzi 17:00-21:00";
        }

        if (stats.CurrentStreak == 0 && stats.ConsistencyPercent < 30)
        {
            return $"Najcastejsie nepijes {weakestWindow}. Skus presunut prvu alebo dalsiu pripomienku prave do tohto okna a ciel znizit na mensie davky.";
        }

        if (stats.TrendMl < -120)
        {
            return $"Tvoj trend pitia klesa. Najviac priestoru na zlepsenie je {weakestWindow}, kde sa oplati pridat jednu malu davku navyse.";
        }

        if (stats.DayParts.MorningMl < stats.DayParts.NoonMl / 2)
        {
            return "Rano pijes vyrazne menej ako cez den. Skus vypit prvy pohar vody do 30 minut po zobudeni.";
        }

        return $"Tvoje pitie je relativne stabilne. Najvacsi priestor na dalsie zlepsenie je {weakestWindow}.";
    }

    private static string GetWeakestSegment(DayPartStats stats)
    {
        var weakest = "morning";
        var value = stats.MorningMl;

        if (stats.NoonMl < value)
        {
            weakest = "noon";
            value = stats.NoonMl;
        }

        if (stats.EveningMl < value)
        {
            weakest = "evening";
        }

        return weakest;
    }

    private static List<DateTime> BuildPreferredTimes(DateTime start, DateTime end, int count, string weakSegment)
    {
        var preferredWindow = weakSegment switch
        {
            "morning" => (Start: DateTime.Today.AddHours(8), End: DateTime.Today.AddHours(11)),
            "noon" => (Start: DateTime.Today.AddHours(11), End: DateTime.Today.AddHours(17)),
            _ => (Start: DateTime.Today.AddHours(17), End: DateTime.Today.AddHours(21))
        };

        var times = new List<DateTime>();
        var effectiveStart = start;
        var effectiveEnd = end;

        if (preferredWindow.End > effectiveStart && preferredWindow.Start < effectiveEnd)
        {
            var segmentStart = preferredWindow.Start > effectiveStart ? preferredWindow.Start : effectiveStart;
            var segmentEnd = preferredWindow.End < effectiveEnd ? preferredWindow.End : effectiveEnd;
            var segmentCount = Math.Max(1, (int)Math.Ceiling(count / 2d));
            times.AddRange(SpreadTimes(segmentStart, segmentEnd, segmentCount));
        }

        while (times.Count < count)
        {
            var fallback = SpreadTimes(effectiveStart, effectiveEnd, count)
                .Where(time => times.All(existing => Math.Abs((existing - time).TotalMinutes) >= 25))
                .ToList();

            foreach (var time in fallback)
            {
                if (times.Count >= count)
                {
                    break;
                }

                times.Add(time);
            }

            if (fallback.Count == 0)
            {
                break;
            }
        }

        return times
            .OrderBy(time => time)
            .Take(count)
            .ToList();
    }

    private static List<DateTime> SpreadTimes(DateTime start, DateTime end, int count)
    {
        if (count <= 1 || end <= start)
        {
            return [start];
        }

        var totalMinutes = (end - start).TotalMinutes;
        var interval = totalMinutes / (count + 1);
        return Enumerable.Range(1, count)
            .Select(index => start.AddMinutes(Math.Round(interval * index)))
            .ToList();
    }

    private static string BuildReminderMessage(int index, int total, string weakSegment, int remainingMl, int doseMl)
    {
        var segmentText = DescribeSegment(weakSegment);
        if (index == 0)
        {
            return $"Dnes este ostava dopit asi {remainingMl} ml. Zacni teraz mensou davkou okolo {doseMl} ml.";
        }

        if (index == total - 1)
        {
            return $"Zaver dna: doplnenie vody v useku {segmentText}. Daj si aspon {doseMl} ml.";
        }

        return $"Historicky slabsie miesto je {segmentText}. Skus teraz vypit okolo {doseMl} ml.";
    }

    private static string DescribeSegment(string weakSegment) => weakSegment switch
    {
        "morning" => "rano medzi 8:00-11:00",
        "noon" => "na obed medzi 11:00-17:00",
        _ => "vecer medzi 17:00-21:00"
    };
}

public sealed class DailyHydrationRecord
{
    public string Date { get; set; } = string.Empty;
    public int ConsumedMl { get; set; }
    public int GoalMl { get; set; }
    public List<HydrationEvent> Events { get; set; } = [];
}

public sealed class HydrationEvent
{
    public string Time { get; set; } = string.Empty;
    public int AmountMl { get; set; }
}

public sealed record DayPartStats(int MorningMl, int NoonMl, int EveningMl);

public sealed record ChartPoint(string Label, int ConsumedMl, int GoalMl);

public sealed record HydrationStats(
    int WindowDays,
    int TotalConsumedMl,
    int AveragePerDayMl,
    int ActiveDays,
    int GoalHitDays,
    int ConsistencyPercent,
    int CurrentStreak,
    int TrendMl,
    DayPartStats DayParts,
    List<ChartPoint> ChartPoints,
    List<DailyHydrationRecord> Records);

public sealed record InsightSummary(
    HydrationStats Stats7,
    HydrationStats Stats30,
    string Recommendation);

public sealed record ReminderPlanItem(string Time, int DoseMl, string Message);

public sealed record AdaptiveReminderPlan(List<ReminderPlanItem> Items, string Summary);

public static class AppStateStorage
{
    private const string StateKey = "app_state_json";

    public static AppState Load(ISharedPreferences preferences)
    {
        try
        {
            var json = preferences.GetString(StateKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return AppState.CreateDefault();
            }

            var state = JsonSerializer.Deserialize<AppState>(json);
            return state ?? AppState.CreateDefault();
        }
        catch
        {
            return AppState.CreateDefault();
        }
    }

    public static void Save(ISharedPreferences preferences, AppState state)
    {
        var json = JsonSerializer.Serialize(state);
        preferences.Edit()?.PutString(StateKey, json)?.Apply();
    }
}
