using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;

namespace DrinkWater.Android;

[global::Android.Runtime.Register("DrinkWater.Android.DailyBarChartView")]
public class DailyBarChartView : View
{
    private readonly Paint _consumedPaint = new() { AntiAlias = true, Color = Color.ParseColor("#1D7C7A") };
    private readonly Paint _goalPaint = new() { AntiAlias = true, Color = Color.ParseColor("#D9E8E6") };
    private readonly Paint _labelPaint = new() { AntiAlias = true, Color = Color.ParseColor("#5D7472"), TextSize = 28f };
    private readonly Paint _linePaint = new() { AntiAlias = true, Color = Color.ParseColor("#E8DCC9"), StrokeWidth = 2f };

    public List<ChartPoint> Points { get; set; } = [];

    public DailyBarChartView(Context context) : base(context)
    {
    }

    public DailyBarChartView(Context context, IAttributeSet? attrs) : base(context, attrs)
    {
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        if (canvas is null || Points.Count == 0)
        {
            return;
        }

        var width = Width;
        var height = Height;
        var density = Resources!.DisplayMetrics!.Density;
        _labelPaint.TextSize = 10f * Resources.DisplayMetrics.ScaledDensity;
        var paddingLeft = 20f * density;
        var paddingRight = 20f * density;
        var paddingTop = 32f * density;
        var paddingBottom = 28f * density;
        var chartHeight = height - paddingTop - paddingBottom;
        var contentWidth = width - paddingLeft - paddingRight;
        var step = contentWidth / Points.Count;
        var barWidth = step * 0.6f;
        var maxValue = Math.Max(1, Points.Max(point => Math.Max(point.ConsumedMl, point.GoalMl)));
        canvas.DrawText($"{maxValue} ml | tmave: vypite, svetle: ciel", paddingLeft, 16f * density, _labelPaint);

        canvas.DrawLine(paddingLeft, paddingTop + chartHeight, width - paddingRight, paddingTop + chartHeight, _linePaint);

        for (var i = 0; i < Points.Count; i++)
        {
            var point = Points[i];
            var centerX = paddingLeft + step * i + step / 2f;
            var goalHeight = chartHeight * point.GoalMl / maxValue;
            var consumedHeight = chartHeight * point.ConsumedMl / maxValue;

            canvas.DrawRoundRect(
                centerX - barWidth / 2f,
                paddingTop + chartHeight - goalHeight,
                centerX + barWidth / 2f,
                paddingTop + chartHeight,
                10f,
                10f,
                _goalPaint);

            canvas.DrawRoundRect(
                centerX - barWidth / 2f,
                paddingTop + chartHeight - consumedHeight,
                centerX + barWidth / 2f,
                paddingTop + chartHeight,
                10f,
                10f,
                _consumedPaint);

            var labelEvery = Math.Max(1, (int)Math.Ceiling(Points.Count / 5d));
            if (i % labelEvery == 0 || i == Points.Count - 1)
                canvas.DrawText(point.Label, centerX - _labelPaint.MeasureText(point.Label) / 2, height - 8f * density, _labelPaint);
        }
    }
}
