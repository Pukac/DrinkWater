using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;

namespace DrinkWater.Android;

[Activity(Label = "Insights", Exported = false)]
public class InsightsActivity : Activity
{
    private const string PreferencesName = "drinkwater.preferences";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_insights);

        var preferences = GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        var state = AppStateStorage.Load(preferences);
        state.ResetForToday();
        var insights = state.GenerateInsights();

        FindViewById<TextView>(Resource.Id.insightHeadlineText)!.Text = "Insights";
        FindViewById<TextView>(Resource.Id.insightSummaryText)!.Text = insights.Recommendation;
        FindViewById<TextView>(Resource.Id.card7Text)!.Text = BuildCardText("7 dni", insights.Stats7);
        FindViewById<TextView>(Resource.Id.card30Text)!.Text = BuildCardText("30 dni", insights.Stats30);
        FindViewById<TextView>(Resource.Id.trendText)!.Text = BuildTrendText(insights);
        FindViewById<TextView>(Resource.Id.dayPartInsightText)!.Text = BuildDayPartInsight(insights.Stats30.DayParts);

        var chart7 = FindViewById<DailyBarChartView>(Resource.Id.chart7View)!;
        chart7.Points = insights.Stats7.ChartPoints;
        chart7.Invalidate();

        var chart30 = FindViewById<DailyBarChartView>(Resource.Id.chart30View)!;
        chart30.Points = insights.Stats30.ChartPoints;
        chart30.Invalidate();

        FindViewById<Button>(Resource.Id.closeInsightsButton)!.Click += (_, _) => Finish();
    }

    private static string BuildCardText(string title, HydrationStats stats)
    {
        return string.Join(
            System.Environment.NewLine,
            [
                title,
                $"{stats.AveragePerDayMl} ml priemer / den",
                $"{stats.GoalHitDays}/{stats.WindowDays} splnenych cielov",
                $"{stats.ConsistencyPercent} % konzistentnost",
                "Priemer za cele obdobie vratane dneska. Chybajuce dni maju v sucte 0 ml."
            ]);
    }

    private static string BuildTrendText(InsightSummary insights)
    {
        var trend7 = DescribeTrend(insights.Stats7.TrendMl);
        var trend30 = DescribeTrend(insights.Stats30.TrendMl);

        return string.Join(
            System.Environment.NewLine,
            [
                "Streak a trend",
                $"Aktualny streak: {insights.Stats30.CurrentStreak} dni",
                $"Trend 7 dni: {trend7}",
                $"Trend 30 dni: {trend30}"
            ]);
    }

    private static string BuildDayPartInsight(DayPartStats stats)
    {
        if (stats.MorningMl + stats.NoonMl + stats.EveningMl == 0)
            return "Zatial bez casovych zaznamov. Starsie denne sucty zostavaju v historii, ale nevieme ich priradit k casti dna.";
        var weakest = "rano do 11:00";
        var weakestValue = stats.MorningMl;

        if (stats.NoonMl < weakestValue)
        {
            weakest = "na obed medzi 11:00-17:00";
            weakestValue = stats.NoonMl;
        }

        if (stats.EveningMl < weakestValue)
        {
            weakest = "vecer od 17:00";
        }

        return string.Join(
            System.Environment.NewLine,
            [
                "Rozdelenie dna",
                $"Rano: {stats.MorningMl} ml",
                $"Obed: {stats.NoonMl} ml",
                $"Vecer: {stats.EveningMl} ml",
                $"Najmenej zaznamenaneho objemu: {weakest}. Nejde o meranie skutocnej hydratacie."
            ]);
    }

    private static string DescribeTrend(int trendMl)
    {
        if (trendMl > 120) return $"rastie o cca {trendMl} ml";
        if (trendMl < -120) return $"klesa o cca {Math.Abs(trendMl)} ml";
        return "stabilny";
    }
}
