using Microsoft.Maui.Graphics;

namespace DriverLedger.Helpers
{
    /// <summary>
    /// A lightweight custom IDrawable bar chart.
    /// No external packages — safe for Android Release builds.
    /// </summary>
    public class BarChartDrawable : IDrawable
    {
        public string Title        { get; set; } = string.Empty;
        public List<float> Values  { get; set; } = new();
        public List<string> Labels { get; set; } = new();
        public Color BarColor      { get; set; } = Color.FromArgb("#2979FF");
        public Color TextColor     { get; set; } = Color.FromArgb("#90CAF9");
        public Color AxisColor     { get; set; } = Color.FromArgb("#1E3A5F");
        public Color TitleColor    { get; set; } = Color.FromArgb("#BBDEFB");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Values is null || Values.Count == 0) return;

            float padLeft   = 48f;
            float padRight  = 12f;
            float padTop    = 32f;
            float padBottom = 36f;

            float chartWidth  = dirtyRect.Width  - padLeft - padRight;
            float chartHeight = dirtyRect.Height - padTop  - padBottom;

            float maxVal = Values.Max();
            if (maxVal <= 0) maxVal = 1;

            int count    = Values.Count;
            float barW   = (chartWidth / count) * 0.55f;
            float gapW   = (chartWidth / count) * 0.45f;

            // Title
            canvas.FontColor = TitleColor;
            canvas.FontSize  = 12f;
            canvas.Font      = new Microsoft.Maui.Graphics.Font("Default");
            canvas.DrawString(Title, padLeft, 8f, HorizontalAlignment.Left);

            // Axis lines
            canvas.StrokeColor = AxisColor;
            canvas.StrokeSize  = 1f;
            canvas.DrawLine(padLeft, padTop, padLeft, padTop + chartHeight);
            canvas.DrawLine(padLeft, padTop + chartHeight, dirtyRect.Width - padRight, padTop + chartHeight);

            // Y-axis labels (3 ticks: 0, mid, max)
            canvas.FontColor = TextColor;
            canvas.FontSize  = 9f;
            float[] ticks = { 0f, maxVal / 2f, maxVal };
            foreach (var tick in ticks)
            {
                float y = padTop + chartHeight - (tick / maxVal * chartHeight);
                canvas.DrawString(FormatValue(tick), 2f, y - 6f, padLeft - 4f, 14f, HorizontalAlignment.Right, VerticalAlignment.Center);
                canvas.StrokeColor = AxisColor;
                canvas.StrokeSize  = 0.3f;
                canvas.DrawLine(padLeft, y, padLeft + chartWidth, y);
            }

            // Bars
            for (int i = 0; i < count; i++)
            {
                float x      = padLeft + i * (chartWidth / count) + gapW / 2f;
                float normH  = (Values[i] / maxVal) * chartHeight;
                float barTop = padTop + chartHeight - normH;

                // Bar fill with gradient-like effect
                canvas.FillColor = BarColor;
                canvas.FillRoundedRectangle(x, barTop, barW, normH, 3f);

                // Value label on bar top
                if (Values[i] > 0)
                {
                    canvas.FontColor = Color.FromArgb("#FFFFFF");
                    canvas.FontSize  = 8f;
                    canvas.DrawString(FormatValue(Values[i]),
                        x, barTop - 14f, barW, 14f,
                        HorizontalAlignment.Center, VerticalAlignment.Center);
                }

                // X-axis label
                canvas.FontColor = TextColor;
                canvas.FontSize  = 9f;
                var lbl = i < Labels.Count ? Labels[i] : i.ToString();
                canvas.DrawString(lbl,
                    x - 2f, padTop + chartHeight + 4f, barW + 4f, 14f,
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        private static string FormatValue(float v) =>
            v >= 1000 ? $"₹{v / 1000:F1}k" : $"₹{v:F0}";
    }
}

