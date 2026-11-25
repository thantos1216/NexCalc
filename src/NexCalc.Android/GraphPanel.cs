using System.Globalization;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using NexCalc.Core;
using Format = NexCalc.Core.Format;

namespace NexCalc.Mobile;

public sealed partial class MainActivity
{
    private View GraphPanel()
    {
        var panel = Column();
        var first = Field("Y1 = f(x)", "sin(x)");
        var second = Field("Y2 = g(x) (optional)");
        var min = Field("X min", "-10");
        var max = Field("X max", "10");
        var bottom = Field("Y min", "auto");
        var top = Field("Y max", "auto");
        var samples = Field("Samples", "100");
        var xStep = Field("X step", "1");
        var yStep = Field("Y step", "1");
        var message = Label("");
        var chart = new GraphView(this);
        int plotType = 0;

        void Plot()
        {
            if (!Number(min, out var left) || !Number(max, out var right) || left >= right)
            {
                message.Text = "X min and max must be finite numbers in increasing order.";
                return;
            }

            int count = int.TryParse(samples.Text, out var parsed) ? Math.Clamp(parsed, 10, 500) : 100;
            string y1 = first.Text ?? "";
            string y2 = second.Text ?? "";
            GraphPlan plan = plotType switch
            {
                1 => Graph.PlanPolar(y1, 0, angleMode switch
                {
                    AngleMode.Deg => 360,
                    AngleMode.Grad => 400,
                    _ => 2 * Math.PI
                }, count, angleMode),
                2 => Graph.PlanParam(y1, y2, left, right, count, angleMode),
                _ => Graph.Plan(y1, left, right, count, angleMode)
            };
            GraphPlan? other = plotType == 0 && !string.IsNullOrWhiteSpace(y2)
                ? Graph.Plan(y2, left, right, count, angleMode) : null;
            if (!plan.ok || other is { ok: false })
            {
                chart.SetCurves([], [], null, null, null, null, 1, 1);
                message.Text = !plan.ok ? plan.message : other?.message;
                return;
            }

            double? yLow = null, yHigh = null;
            if (plotType == 0 && Number(bottom, out var lo) && Number(top, out var hi) && lo < hi)
                (yLow, yHigh) = (lo, hi);

            chart.SetCurves(plan.polarXs ?? plan.xs, plan.polarYs ?? plan.ys, other,
                plotType == 0 ? (left, right) : null, yLow, yHigh,
                Number(xStep, out var dx) && dx > 0 ? dx : 1, Number(yStep, out var dy) && dy > 0 ? dy : 1);
            message.Text = $"Plotted {count} samples · {angleMode.ToString().ToUpperInvariant()}";
        }

        void Zoom(double factor)
        {
            if (!Number(min, out var left) || !Number(max, out var right)) return;
            var center = (left + right) / 2;
            var half = (right - left) / 2 * factor;
            if (plotType == 1)
            {
                chart.Zoom(factor);
                return;
            }
            min.Text = Invariant(center - half);
            max.Text = Invariant(center + half);
            if (Number(bottom, out var lo) && Number(top, out var hi) && lo < hi)
            {
                var mid = (lo + hi) / 2;
                var span = (hi - lo) / 2 * factor;
                bottom.Text = Invariant(mid - span);
                top.Text = Invariant(mid + span);
            }
            Plot();
        }

        void Trace(int direction)
        {
            var point = chart.Trace(direction);
            message.Text = point is { } value
                ? $"Trace: x={value.X:G6}, y={value.Y:G6}" : "Plot a curve first.";
        }

        void Solve(bool intersection)
        {
            if (plotType != 0) { message.Text = "Root and intersection require a Cartesian plot."; return; }
            if (string.IsNullOrWhiteSpace(first.Text) || (intersection && string.IsNullOrWhiteSpace(second.Text)))
            { message.Text = "Enter the function expressions first."; return; }

            double start = chart.TracePoint?.X ?? (Number(min, out var left) && Number(max, out var right) ? (left + right) / 2 : 0);
            var function = intersection ? $"({first.Text})-({second.Text})" : first.Text;
            var solved = Expression.Calculate($"solve({function},x,{Invariant(start)})", angleMode, answer);
            message.Text = solved.ok && solved.value is { } value
                ? $"{(intersection ? "Intersection" : "Root")}: x={Format.Display(value)}" : solved.message;
        }

        void Sketch(string type)
        {
            if (chart.TracePoint is not { } point) { message.Text = "Trace a point first."; return; }
            double slope = 0;
            if (type == "Tangent")
            {
                if (plotType != 0) { message.Text = "Tangent requires a Cartesian plot."; return; }
                var derivative = Expression.Calculate($"deriv({first.Text},x,{Invariant(point.X)})", angleMode, answer);
                if (!derivative.ok || derivative.value is not { } value)
                { message.Text = derivative.message; return; }
                slope = value.ToDoubleValue();
            }
            chart.Sketch(type, point, slope);
            message.Text = type == "Tangent" ? $"Tangent: slope={slope:G6}" : $"{type}: {(type == "Horiz" ? $"y={point.Y:G6}" : $"x={point.X:G6}")}";
        }

        panel.AddView(first);
        panel.AddView(second);
        Row(panel, Label("X min"), Label("X max"), Label("Y min"), Label("Y max"));
        Row(panel, min, max, bottom, top);
        Row(panel, Label("Samples"), Label("X step"), Label("Y step"));
        Row(panel, samples, xStep, yStep);
        Row(panel, Choice("Plot type", ["Cartesian", "Polar", "Parametric"], 0, index =>
        {
            plotType = index;
            first.Hint = index switch { 1 => "r(θ)", 2 => "X(t), use x for t", _ => "Y1 = f(x)" };
            second.Hint = index == 2 ? "Y(t), use x for t" : "Y2 = g(x) (optional)";
            chart.SetCurves([], [], null, null, null, null, 1, 1);
            message.Text = "";
        }), Key("Plot", Plot));
        panel.AddView(chart, new LinearLayout.LayoutParams(-1, Dp(280)));
        panel.AddView(message);
        Row(panel, Key("Zoom In", () => Zoom(0.5)), Key("Zoom Out", () => Zoom(2)), Key("Trace", () => Trace(1)),
            Key("◀", () => Trace(-1)), Key("▶", () => Trace(1)));
        Row(panel, Key("Root", () => Solve(false)), Key("Inter", () => Solve(true)), Key("Tangent", () => Sketch("Tangent")),
            Key("Horiz", () => Sketch("Horiz")), Key("Vert", () => Sketch("Vert")));
        OnDone(first, Plot);
        OnDone(second, Plot);
        return panel;
    }

    private static bool Number(EditText field, out double value) =>
        double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static string Invariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private sealed class GraphView(MainActivity activity) : View(activity)
    {
        private readonly Paint paint = new(PaintFlags.AntiAlias);
        private double[] xs = [], ys = [];
        private GraphPlan? other;
        private double left, right, bottom, top, xStep, yStep;
        private int traced = -1;
        private (string Type, double X, double Y, double Slope)? sketch;

        public (double X, double Y)? TracePoint => traced < 0 ? null : (xs[traced], ys[traced]);

        public void SetCurves(double[] x, double[] y, GraphPlan? second, (double Left, double Right)? xRange,
            double? yLow, double? yHigh, double dx, double dy)
        {
            xs = x;
            ys = y;
            other = second;
            traced = -1;
            sketch = null;
            xStep = dx;
            yStep = dy;
            (left, right) = xRange ?? Range(x, second?.xs);
            (bottom, top) = yLow.HasValue && yHigh.HasValue ? (yLow.Value, yHigh.Value) : Range(y, second?.ys);
            Invalidate();
        }

        private static (double Min, double Max) Range(double[] values, double[]? other)
        {
            var usable = values.Concat(other ?? []).Where(double.IsFinite).ToArray();
            if (usable.Length == 0) return (-1, 1);
            var min = usable.Min();
            var max = usable.Max();
            double margin = min == max ? Math.Max(1, Math.Abs(min) * 0.05) : (max - min) * 0.05;
            return (min - margin, max + margin);
        }

        public (double X, double Y)? Trace(int direction)
        {
            for (int step = 0; step < xs.Length; step++)
            {
                traced = traced < 0 ? (direction < 0 ? xs.Length - 1 : 0) : (traced + direction + xs.Length) % xs.Length;
                if (double.IsFinite(xs[traced]) && double.IsFinite(ys[traced]))
                {
                    Invalidate();
                    return TracePoint;
                }
            }
            traced = -1;
            Invalidate();
            return null;
        }

        public void Sketch(string type, (double X, double Y) point, double slope)
        {
            sketch = (type, point.X, point.Y, slope);
            Invalidate();
        }

        public void Zoom(double factor)
        {
            double horizontal = (right - left) * (1 - factor) / 2;
            double vertical = (top - bottom) * (1 - factor) / 2;
            left += horizontal;
            right -= horizontal;
            bottom += vertical;
            top -= vertical;
            Invalidate();
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            canvas.DrawColor(Color.Rgb(18, 18, 30));
            if (xs.Length == 0) return;
            float pad = activity.Dp(14);
            float X(double x) => (float)(pad + (x - left) / (right - left) * (Width - pad * 2));
            float Y(double y) => (float)(Height - pad - (y - bottom) / (top - bottom) * (Height - pad * 2));

            void Line(double x1, double y1, double x2, double y2)
            {
                float a = X(x1), b = Y(y1), c = X(x2), d = Y(y2);
                if (float.IsFinite(a) && float.IsFinite(b) && float.IsFinite(c) && float.IsFinite(d))
                    canvas.DrawLine(a, b, c, d, paint);
            }

            void Curve(double[] x, double[] y, Color color)
            {
                paint.Color = color;
                paint.StrokeWidth = activity.Dp(2);
                for (int index = 1; index < x.Length; index++)
                    Line(x[index - 1], y[index - 1], x[index], y[index]);
            }

            canvas.Save();
            canvas.ClipRect(pad, pad, Width - pad, Height - pad);
            paint.Color = Color.Rgb(52, 52, 73);
            paint.StrokeWidth = activity.Dp(1);
            if ((right - left) / xStep <= 100)
                for (int i = 0; i < 101; i++)
                {
                    double x = (Math.Ceiling(left / xStep) + i) * xStep;
                    if (x > right) break;
                    Line(x, bottom, x, top);
                }
            if ((top - bottom) / yStep <= 100)
                for (int i = 0; i < 101; i++)
                {
                    double y = (Math.Ceiling(bottom / yStep) + i) * yStep;
                    if (y > top) break;
                    Line(left, y, right, y);
                }
            paint.Color = Color.Rgb(126, 134, 157);
            Line(0, bottom, 0, top);
            Line(left, 0, right, 0);
            Curve(xs, ys, Accent);
            if (other is { } second) Curve(second.xs, second.ys, Color.Rgb(143, 216, 160));
            if (sketch is { } guide)
            {
                paint.Color = ErrorColor;
                if (guide.Type == "Horiz") Line(left, guide.Y, right, guide.Y);
                else if (guide.Type == "Vert") Line(guide.X, bottom, guide.X, top);
                else Line(left, guide.Y + guide.Slope * (left - guide.X), right, guide.Y + guide.Slope * (right - guide.X));
            }
            if (TracePoint is { } point)
            {
                paint.Color = Color.Rgb(255, 192, 105);
                canvas.DrawCircle(X(point.X), Y(point.Y), activity.Dp(4), paint);
            }
            canvas.Restore();
        }
    }
}
