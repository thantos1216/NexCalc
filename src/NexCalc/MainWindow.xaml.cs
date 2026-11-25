using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Threading.Tasks;
using NexCalc.Core;

namespace NexCalc;

// Main window: Calculate tab (expression box + keypad + history),
// Graph tab (2D f(x) and polar r(θ) rendering on a Canvas),
// Python tab (source box + Run + output, driven by PythonRunner).
public partial class MainWindow : Window
{
    Val? lastAnsVal = null;
    bool pyBusy = false;

    // Trace state: the curve last rendered, its canvas mapping, and the cursor index
    double[]? trXs;
    double[]? trYs;
    double trSx;
    double trSy;
    double trMinX;
    double trMinY;
    int trIdx;
    bool trHas;
    Ellipse? trDot;
    List<Shape> sketchLines = new List<Shape>();

    public MainWindow()
    {
        InitializeComponent();
    }

    AngleMode CurrentMode()
    {
        string sel = (angleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "DEG";
        if (sel == "RAD") return AngleMode.Rad;
        if (sel == "GRAD") return AngleMode.Grad;
        return AngleMode.Deg;
    }

    DisplayFormat CurrentFmt()
    {
        string sel = (displayCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Norm";
        if (sel == "Fix") return DisplayFormat.Fix;
        if (sel == "Sci") return DisplayFormat.Sci;
        if (sel == "DMS") return DisplayFormat.Dms;
        if (sel == "Frac") return DisplayFormat.Frac;
        if (sel == "Mixed") return DisplayFormat.Mixed;
        return DisplayFormat.Norm;
    }

    int CurrentDigits()
    {
        string sel = (displayDigits.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "0";
        return int.Parse(sel);
    }

    void Evaluate()
    {
        string text = exprBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        Result r = NexCalc.Core.Expression.Calculate(text, CurrentMode(), lastAnsVal);
        if (r.ok && r.value != null)
        {
            string disp = Format.Display(r.value!, CurrentFmt(), CurrentDigits());
            displayText.Text = disp;
            displayText.Foreground = new SolidColorBrush(Color.FromRgb(238, 240, 248));
            historyBox.Items.Insert(0, text + " = " + disp);
            lastAnsVal = r.value;
        }
        else
        {
            displayText.Text = r.message ?? "Error";
            displayText.Foreground = new SolidColorBrush(Color.FromRgb(255, 110, 110));
        }
    }

    void Expr_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Evaluate();
            e.Handled = true;
        }
    }

    void MathPrint_Toggle(object sender, RoutedEventArgs e)
    {
        UpdateMathPrint();
    }

    void Expr_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (mathPrintBorder.Visibility == Visibility.Visible)
        {
            UpdateMathPrint();
        }
    }

    void UpdateMathPrint()
    {
        if (mathPrintToggle.IsChecked == true)
        {
            mathPrintBorder.Visibility = Visibility.Visible;
            mathPrint.Text = Format.MathPrint(exprBox.Text);
        }
        else
        {
            mathPrintBorder.Visibility = Visibility.Collapsed;
        }
    }

    void Key_Click(object sender, RoutedEventArgs e)
    {
        string t = ((Button)sender).Content?.ToString() ?? "";
        if (t == "C" || t == "AC")
        {
            exprBox.Text = "";
            displayText.Text = "0";
        }
        else if (t == "⌫" || t == "DEL")
        {
            if (exprBox.Text.Length > 0)
            {
                exprBox.Text = exprBox.Text.Substring(0, exprBox.Text.Length - 1);
            }
        }
        else if (t == "=")
        {
            Evaluate();
        }
        else if (t == "ANS")
        {
            exprBox.Text += "ANS";
        }
        else if (t == "sin" || t == "cos" || t == "tan" || t == "ln" || t == "log" || t == "exp" || t == "sqrt" || t == "dms" || t == "logb" || t == "pol" || t == "rec" || t == "conj" || t == "arg" || t == "cmplx" || t == "sinh" || t == "cosh" || t == "tanh" || t == "poly2" || t == "poly3" || t == "poly4" || t == "solve")
        {
            exprBox.Text += t + "(";
        }
        else if (t == "X")
        {
            exprBox.Text += "X()";
        }
        else if (t == "Y")
        {
            exprBox.Text += "Y()";
        }
        else if (t == "M+")
        {
            exprBox.Text += "madd(ANS)";
        }
        else if (t == "MR")
        {
            exprBox.Text += "m(";
        }
        else if (t == "MC")
        {
            exprBox.Text += "mc(";
        }
        else if (t == "STO")
        {
            exprBox.Text += "sto(";
        }
        else if (t == "RCL")
        {
            exprBox.Text += "rcl(";
        }
        else if (t == "A")
        {
            exprBox.Text += "a";
        }
        else if (t == "B")
        {
            exprBox.Text += "b";
        }
        else if (t == "D")
        {
            exprBox.Text += "d";
        }
        else if (t == "d/c")
        {
            // Fraction key: inserts a slash; from an empty box, a full 1/2 fraction.
            exprBox.Text += exprBox.Text.Length == 0 ? "1/2" : "/";
        }
        else if (t == "SPC")
        {
            exprBox.Text += " ";
        }
        else if (t == "x²")
        {
            exprBox.Text += "square(";
        }
        else if (t == "x³")
        {
            exprBox.Text += "cube(";
        }
        else if (t == "1/x")
        {
            exprBox.Text += "recip(";
        }
        else if (t == "10^x" || t == "EXP")
        {
            exprBox.Text += "tenpow(";
        }
        else if (t == "(-)")
        {
            exprBox.Text += "-";
        }
        else if (t == "Rand#")
        {
            exprBox.Text += "rand()";
        }
        else if (t == "∫")
        {
            exprBox.Text += "integrate(";
        }
        else if (t == "d/dx")
        {
            exprBox.Text += "deriv(";
        }
        else if (t == "nCr")
        {
            exprBox.Text += "combin(";
        }
        else if (t == "nPr")
        {
            exprBox.Text += "perm(";
        }
        else
        {
            exprBox.Text += t;
        }
        exprBox.CaretIndex = exprBox.Text.Length;
    }

    void GraphExpr_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Plot();
            e.Handled = true;
        }
    }

    void Plot_Click(object sender, RoutedEventArgs e)
    {
        Plot();
    }

    void Zoom_Click(object sender, RoutedEventArgs e)
    {
        string tag = ((Button)sender).Tag?.ToString() ?? "";
        double a;
        double b;
        if (!double.TryParse(graphMinText.Text, out a) || !double.TryParse(graphMaxText.Text, out b))
        {
            graphMsg.Text = "min and max must be numbers.";
            return;
        }
        double mid = (a + b) / 2.0;
        double half = (b - a) / 2.0;
        if (tag == "in") half = half / 2.0;
        if (tag == "out") half = half * 2.0;
        if (half <= 0.0 || double.IsNaN(half) || double.IsInfinity(half)) half = 10.0;
        graphMinText.Text = (mid - half).ToString("0.###");
        graphMaxText.Text = (mid + half).ToString("0.###");
        Plot();
    }

    void Trace_Click(object sender, RoutedEventArgs e)
    {
        string tag = ((Button)sender).Tag?.ToString() ?? "";
        if (!trHas || trXs == null || trYs == null)
        {
            graphMsg.Text = "Plot a curve first to use Trace.";
            return;
        }
        int n = trXs.Length;
        int idx = trIdx;
        int want = tag == "prev" ? -1 : 1;
        int steps = 0;
        while (steps < n)
        {
            idx = idx + want;
            if (idx < 0) idx = n - 1;
            if (idx >= n) idx = 0;
            steps = steps + 1;
            if (!double.IsNaN(trXs[idx]) && !double.IsNaN(trYs[idx])) break;
        }
        if (steps >= n)
        {
            graphMsg.Text = "No valid points to trace.";
            return;
        }
        trIdx = idx;
        double pad = 28;
        double W = graphCanvas.ActualWidth;
        double H = graphCanvas.ActualHeight;
        double px = pad + (trXs[idx] - trMinX) * trSx;
        double py = H - pad - (trYs[idx] - trMinY) * trSy;
        if (trDot != null)
        {
            graphCanvas.Children.Remove(trDot);
            trDot = null;
        }
        if (px < pad - 2 || px > W - pad + 2 || py < pad - 2 || py > H - pad + 2)
        {
            graphMsg.Text = "Trace point is outside the visible window: x=" + trXs[idx].ToString("0.###") + ", y=" + trYs[idx].ToString("0.###");
            return;
        }
        Ellipse dot = new Ellipse();
        dot.Width = 7;
        dot.Height = 7;
        dot.Fill = new SolidColorBrush(Color.FromRgb(255, 192, 105));
        Canvas.SetLeft(dot, px - 3.5);
        Canvas.SetTop(dot, py - 3.5);
        graphCanvas.Children.Add(dot);
        trDot = dot;
        graphMsg.Text = "trace: x=" + trXs[idx].ToString("0.###") + ", y=" + trYs[idx].ToString("0.###")
            + " (point " + (idx + 1) + " of " + n + ")";
    }

    // Root: solve Y1 = 0 near the current trace point (or the center of the
    // x-range when nothing is traced). Inter: solve Y1 - Y2 = 0 from the
    // same starting x. Both reuse the engine's Newton solve through
    // Expression.Calculate, so all parsing and error messages stay shared.
    void GraphSolve_Click(object sender, RoutedEventArgs e)
    {
        string kind = (string)((Button)sender).Tag;
        string y1 = graphExpr.Text;
        string y2 = graphExpr2.Text;
        if (polarCheck.IsChecked == true || paramCheck.IsChecked == true)
        {
            graphMsg.Text = "Root and Inter work on cartesian plots (Y1 = f(x), Y2 = g(x)).";
            return;
        }
        if (string.IsNullOrWhiteSpace(y1))
        {
            graphMsg.Text = "Root needs Y1 = f(x) in the first box.";
            return;
        }
        if (kind == "inter" && string.IsNullOrWhiteSpace(y2))
        {
            graphMsg.Text = "Inter needs Y1 and Y2 (Y2 box).";
            return;
        }
        double start = trHas ? trXs![trIdx] : 0.0;
        if (!trHas)
        {
            double a = 0;
            double b = 0;
            if (double.TryParse(graphMinText.Text, out a) && double.TryParse(graphMaxText.Text, out b)) start = (a + b) / 2.0;
        }
        string startText = start.ToString("R");
        string expr = kind == "root"
            ? "solve(" + y1 + ",x," + startText + ")"
            : "solve((" + y1 + ")-(" + y2 + "),x," + startText + ")";
        Result r = NexCalc.Core.Expression.Calculate(expr, CurrentMode(), lastAnsVal);
        string verb = kind == "root" ? "root: " : "intersect: ";
        if (!r.ok)
        {
            graphMsg.Text = verb + r.message + " (starting from x=" + start.ToString("0.###") + ")";
            return;
        }
        graphMsg.Text = verb + "x=" + Format.Display(r.value!) + " (starting from x=" + start.ToString("0.###") + ")";
    }

    // Sketch lines at the traced point: tangent of Y1 at the trace x,
    // dashed horizontal/vertical guide lines through the trace point.
    void Sketch_Click(object sender, RoutedEventArgs e)
    {
        string kind = (string)((Button)sender).Tag;
        if (!trHas)
        {
            graphMsg.Text = "Trace a point first (Trace / ◀ / ▶).";
            return;
        }
        double W = graphCanvas.ActualWidth;
        double H = graphCanvas.ActualHeight;
        double pad = 28;
        foreach (Shape sh in sketchLines) graphCanvas.Children.Remove(sh);
        sketchLines.Clear();
        double x0 = trXs![trIdx];
        double y0 = trYs![trIdx];
        if (kind == "tan")
        {
            if (polarCheck.IsChecked == true || paramCheck.IsChecked == true)
            {
                graphMsg.Text = "Tangent works on cartesian plots (Y1 = f(x)).";
                return;
            }
            Result r = NexCalc.Core.Expression.Calculate("deriv(" + graphExpr.Text + ",x," + x0.ToString("R") + ")", CurrentMode(), lastAnsVal);
            if (!r.ok)
            {
                graphMsg.Text = "tangent: " + r.message;
                return;
            }
            double s = r.value!.ToDoubleValue();
            double xL = trMinX;
            double xR = trMinX + (W - 2 * pad) / trSx;
            PointCollection pts = new PointCollection();
            for (int k = 0; k <= 60; k = k + 1)
            {
                double xt = xL + (xR - xL) * k / 60.0;
                double yt = y0 + s * (xt - x0);
                double px = pad + (xt - trMinX) * trSx;
                double py = H - pad - (yt - trMinY) * trSy;
                if (double.IsNaN(px) || double.IsNaN(py)) continue;
                if (py < pad - 2 || py > H - pad + 2) continue;
                pts.Add(new Point(px, py));
            }
            if (pts.Count > 1)
            {
                Polyline pl = new Polyline();
                pl.Points = pts;
                pl.Stroke = new SolidColorBrush(Color.FromRgb(197, 143, 255));
                pl.StrokeThickness = 1.5;
                graphCanvas.Children.Add(pl);
                sketchLines.Add(pl);
            }
            graphMsg.Text = "tangent: slope=" + s.ToString("0.###") + " at (" + x0.ToString("0.###") + ", " + y0.ToString("0.###") + ")";
            return;
        }
        SolidColorBrush c = new SolidColorBrush(Color.FromRgb(255, 158, 158));
        Line l = new Line();
        if (kind == "h")
        {
            double py0 = H - pad - (y0 - trMinY) * trSy;
            l.X1 = pad; l.X2 = W - pad; l.Y1 = py0; l.Y2 = py0;
            graphMsg.Text = "horiz: y=" + y0.ToString("0.###");
        }
        else
        {
            double px0 = pad + (x0 - trMinX) * trSx;
            l.X1 = px0; l.X2 = px0; l.Y1 = pad; l.Y2 = H - pad;
            graphMsg.Text = "vert: x=" + x0.ToString("0.###");
        }
        l.Stroke = c;
        l.StrokeThickness = 1.5;
        DoubleCollection dash = new DoubleCollection();
        dash.Add(4);
        dash.Add(2);
        l.StrokeDashArray = dash;
        graphCanvas.Children.Add(l);
        sketchLines.Add(l);
    }

    void Plot()
    {
        foreach (Shape sh in sketchLines) graphCanvas.Children.Remove(sh);
        sketchLines.Clear();
        double a;
        double b;
        if (!double.TryParse(graphMinText.Text, out a) || !double.TryParse(graphMaxText.Text, out b))
        {
            graphMsg.Text = "min and max must be numbers.";
            return;
        }
        int samples;
        if (!int.TryParse(graphSamplesText.Text, out samples)) samples = 100;
        if (samples < 10) samples = 10;
        if (samples > 500) samples = 500;
        bool param = paramCheck.IsChecked == true;
        bool polar = polarCheck.IsChecked == true;
        if (param)
        {
            if (string.IsNullOrWhiteSpace(graphExpr.Text) || string.IsNullOrWhiteSpace(graphExpr2.Text))
            {
                graphMsg.Text = "Parametric mode needs X(x) in the first box and Y(x) in the Y2 box.";
                return;
            }
            RenderGraph(Graph.PlanParam(graphExpr.Text, graphExpr2.Text, a, b, samples, CurrentMode()), true, null, null, null, true);
            return;
        }
        if (polar)
        {
            // polar sweeps a full circle, using the current angle convention
            double thetaMax = CurrentMode() == AngleMode.Deg ? 360.0
                : CurrentMode() == AngleMode.Grad ? 400.0 : 2 * Math.PI;
            RenderGraph(Graph.PlanPolar(graphExpr.Text, 0, thetaMax, samples, CurrentMode()), true, null, null, null);
            return;
        }
        GraphPlan plan = Graph.Plan(graphExpr.Text, a, b, samples, CurrentMode());
        GraphPlan? plan2 = null;
        if (!string.IsNullOrWhiteSpace(graphExpr2.Text))
        {
            plan2 = Graph.Plan(graphExpr2.Text, a, b, samples, CurrentMode());
        }
        double yLo = 0;
        double yHi = 0;
        bool userY = double.TryParse(graphYminText.Text, out yLo) && double.TryParse(graphYmaxText.Text, out yHi) && yLo < yHi;
        RenderGraph(plan, false, plan2, userY ? (double?)yLo : null, userY ? (double?)yHi : null);
    }

    // Draw one curve as segments on the canvas; points outside the visible
    // Y window end a segment (the curve is clipped at the plot edges).
    void DrawCurve(double[] x, double[] y, Color color, double pad, double W, double H, double sx, double sy, double minX, double minY)
    {
        int n = x.Length;
        int k = 0;
        while (k < n)
        {
            PointCollection pts = new PointCollection();
            while (k < n)
            {
                double xk = x[k];
                double yk = y[k];
                if (double.IsNaN(xk) || double.IsNaN(yk)) break;
                double py = H - pad - (yk - minY) * sy;
                double px = pad + (xk - minX) * sx;
                if (py < pad - 2 || py > H - pad + 2) break;
                pts.Add(new Point(px, py));
                k = k + 1;
            }
            if (pts.Count > 1)
            {
                Polyline pl = new Polyline();
                pl.Points = pts;
                pl.Stroke = new SolidColorBrush(color);
                pl.StrokeThickness = 2;
                graphCanvas.Children.Add(pl);
            }
            k = k + 1;
        }
    }

    void RenderGraph(GraphPlan plan, bool polar, GraphPlan? plan2, double? yLo, double? yHi, bool param = false)
    {
        graphCanvas.Children.Clear();
        if (!plan.ok)
        {
            graphMsg.Text = plan.message ?? "Parse error.";
            return;
        }
        double W = graphCanvas.ActualWidth;
        double H = graphCanvas.ActualHeight;
        if (W < 20 || H < 20)
        {
            graphMsg.Text = "Canvas is not ready yet - press Plot again.";
            return;
        }

        int n = plan.xs.Length;
        double[] x = polar ? (plan.polarXs ?? new double[0]) : plan.xs;
        double[] y = polar ? (plan.polarYs ?? new double[0]) : plan.ys;
        double minX = double.MaxValue, maxX = -double.MaxValue;
        double minY = double.MaxValue, maxY = -double.MaxValue;
        int valid = 0;
        for (int i = 0; i < n; i = i + 1)
        {
            if (!double.IsNaN(x[i]) && !double.IsNaN(y[i]))
            {
                valid = valid + 1;
                if (x[i] < minX) minX = x[i];
                if (x[i] > maxX) maxX = x[i];
                if (y[i] < minY) minY = y[i];
                if (y[i] > maxY) maxY = y[i];
            }
        }
        string y2note = "";
        bool plan2ok = false;
        if (!polar && plan2 != null)
        {
            if (!plan2.ok)
            {
                y2note = "Y2: " + (plan2.message ?? "Parse error.") + "  ";
            }
            else
            {
                plan2ok = true;
                for (int i = 0; i < plan2.ys.Length; i = i + 1)
                {
                    double v = plan2.ys[i];
                    if (!double.IsNaN(v))
                    {
                        if (v < minY) minY = v;
                        if (v > maxY) maxY = v;
                    }
                }
            }
        }
        if (valid == 0)
        {
            graphMsg.Text = "Nothing to plot: every sample point is undefined.";
            return;
        }

        bool userY = yLo != null && yHi != null;
        if (userY && !polar)
        {
            minY = yLo!.Value;
            maxY = yHi!.Value;
        }

        double pad = 28;
        double dx = maxX - minX;
        if (dx <= 0) { minX = minX - 0.5; maxX = maxX + 0.5; dx = maxX - minX; }
        double dy = maxY - minY;
        if (dy <= 0) { minY = minY - 0.5; maxY = maxY + 0.5; dy = maxY - minY; }
        double sx = (W - 2 * pad) / dx;
        double sy = (H - 2 * pad) / dy;
        if (polar)
        {
            double s = Math.Min(sx, sy);
            sx = s;
            sy = s;
        }

        // grid lines with tick labels at nice steps, drawn under axes and curve
        SolidColorBrush gridBrush = new SolidColorBrush(Color.FromRgb(42, 42, 64));
        SolidColorBrush labelBrush = new SolidColorBrush(Color.FromRgb(150, 160, 184));
        double xStep = ParseScale(graphXsclText.Text, NiceStep(maxX - minX), maxX - minX);
        int i0 = (int)Math.Ceiling(minX / xStep);
        int i1 = (int)Math.Floor(maxX / xStep);
        for (int gi = i0; gi <= i1; gi = gi + 1)
        {
            double xt = gi * xStep;
            double px = pad + (xt - minX) * sx;
            Line g = new Line();
            g.X1 = px; g.X2 = px; g.Y1 = pad; g.Y2 = H - pad;
            g.Stroke = gridBrush;
            g.StrokeThickness = 1;
            graphCanvas.Children.Add(g);
            TextBlock tb = new TextBlock();
            tb.Text = xt.ToString("0.###");
            tb.FontSize = 10;
            tb.Foreground = labelBrush;
            Canvas.SetLeft(tb, px - 14);
            Canvas.SetTop(tb, H - pad + 2);
            graphCanvas.Children.Add(tb);
        }
        double yStep = ParseScale(graphYsclText.Text, NiceStep(maxY - minY), maxY - minY);
        int j0 = (int)Math.Ceiling(minY / yStep);
        int j1 = (int)Math.Floor(maxY / yStep);
        for (int gj = j0; gj <= j1; gj = gj + 1)
        {
            double yt = gj * yStep;
            double py = H - pad - (yt - minY) * sy;
            Line g = new Line();
            g.X1 = pad; g.X2 = W - pad; g.Y1 = py; g.Y2 = py;
            g.Stroke = gridBrush;
            g.StrokeThickness = 1;
            graphCanvas.Children.Add(g);
            TextBlock tb = new TextBlock();
            tb.Text = yt.ToString("0.###");
            tb.FontSize = 10;
            tb.Foreground = labelBrush;
            Canvas.SetLeft(tb, pad - 24);
            Canvas.SetTop(tb, py - 6);
            graphCanvas.Children.Add(tb);
        }

        // axes through 0 when 0 is inside the visible window
        if (minX <= 0 && 0 <= maxX)
        {
            double axx = pad + (0 - minX) * sx;
            Line v = new Line();
            v.X1 = axx; v.X2 = axx; v.Y1 = pad; v.Y2 = H - pad;
            v.Stroke = new SolidColorBrush(Color.FromRgb(70, 78, 100));
            v.StrokeThickness = 1;
            graphCanvas.Children.Add(v);
        }
        if (minY <= 0 && 0 <= maxY)
        {
            double ayy = H - pad - (0 - minY) * sy;
            Line hh = new Line();
            hh.X1 = pad; hh.X2 = W - pad; hh.Y1 = ayy; hh.Y2 = ayy;
            hh.Stroke = new SolidColorBrush(Color.FromRgb(70, 78, 100));
            hh.StrokeThickness = 1;
            graphCanvas.Children.Add(hh);
        }

        // curve segments, split at undefined points and clipped at the Y window
        DrawCurve(x, y, Color.FromRgb(102, 170, 255), pad, W, H, sx, sy, minX, minY);
        if (plan2ok)
        {
            DrawCurve(plan2!.xs, plan2.ys, Color.FromRgb(143, 216, 160), pad, W, H, sx, sy, minX, minY);
        }

        // record trace state for Trace/◀/▶ on the curve we just rendered
        trXs = x;
        trYs = y;
        trSx = sx;
        trSy = sy;
        trMinX = minX;
        trMinY = minY;
        trIdx = n / 2;
        trHas = true;
        trDot = null;

        if (param)
        {
            graphMsg.Text = "X(x), Y(x) sampled at " + n + " points over [" + plan.xs[0].ToString("0.###") + ", " + plan.xs[n - 1].ToString("0.###")
                + "]; x in [" + minX.ToString("0.###") + ", " + maxX.ToString("0.###")
                + "], y in [" + minY.ToString("0.###") + ", " + maxY.ToString("0.###") + "]; segments: " + (plan.breaks + 1);
        }
        else if (polar)
        {
            graphMsg.Text = "r(θ) sampled at " + n + " angles; x in [" + minX.ToString("0.###") + ", " + maxX.ToString("0.###")
                + "], y in [" + minY.ToString("0.###") + ", " + maxY.ToString("0.###") + "]; segments: " + (plan.breaks + 1);
        }
        else
        {
            graphMsg.Text = y2note + "x in [" + plan.xs[0].ToString("0.###") + ", " + plan.xs[n - 1].ToString("0.###")
                + "], y in [" + minY.ToString("0.###") + ", " + maxY.ToString("0.###") + "]"
                + (userY ? " (user Y)" : " (auto Y)") + "; Y1 segments: " + (plan.breaks + 1)
                + (plan2ok ? "; Y2 segments: " + (plan2!.breaks + 1) : "");
        }
    }

    void History_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        object sel = historyBox.SelectedItem;
        if (sel == null) return;
        string s = sel.ToString() ?? "";
        int idx = s.IndexOf(" = ");
        if (idx >= 0)
        {
            exprBox.Text = s.Substring(0, idx);
            exprBox.CaretIndex = exprBox.Text.Length;
            exprBox.Focus();
        }
    }

    void PySource_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            RunPython();
            e.Handled = true;
        }
    }

    // User Xscl/Yscl grid step; falls back to the automatic nice step when
    // the field is not a positive number or the grid would be too dense.
    double ParseScale(string text, double fallback, double span)
    {
        double v;
        if (double.TryParse(text, out v) && v > 0 && v < 1e15 && span / v <= 250) return v;
        return fallback;
    }

    double NiceStep(double span)
    {
        double raw = span / 8.0;
        double p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double m = raw / p;
        if (m >= 5.0) return 5.0 * p;
        if (m >= 2.0) return 2.0 * p;
        return p;
    }

    void RunPython_Click(object sender, RoutedEventArgs e)
    {
        RunPython();
    }

    void RunPython()
    {
        if (pyBusy) return;
        string src = pySource.Text;
        pyBusy = true;
        pyOutput.Text = "running...";
        Task.Run(() => PythonRunner.Run(src, 15000)).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                pyBusy = false;
                PythonRunner.RunResult r = t.Result;
                string outText;
                if (r.note != null)
                {
                    outText = r.note;
                }
                else
                {
                    outText = r.output;
                    if (r.errorText.Length > 0) outText = outText + "\n[stderr]\n" + r.errorText;
                    if (r.timedOut) outText = outText + "\n[timed out - process killed]";
                    outText = outText + "\n[exit code " + r.exitCode.ToString() + "]";
                }
                pyOutput.Text = outText;
            });
        });
    }

    // ---------- hamburger menu mode switching ----------

    void Menu_Click(object sender, RoutedEventArgs e)
    {
        modePopup.IsOpen = !modePopup.IsOpen;
    }

    void Mode_Click(object sender, RoutedEventArgs e)
    {
        string m = ((Button)sender).Content?.ToString() ?? "Scientific";
        panelScientific.Visibility = m == "Scientific" ? Visibility.Visible : Visibility.Collapsed;
        panelProg.Visibility = m == "Programmer" ? Visibility.Visible : Visibility.Collapsed;
        panelGraph.Visibility = m == "Graph" ? Visibility.Visible : Visibility.Collapsed;
        panelPython.Visibility = m == "Python" ? Visibility.Visible : Visibility.Collapsed;
        panelStat.Visibility = m == "Stat" ? Visibility.Visible : Visibility.Collapsed;
        modeTitle.Text = m;
        modePopup.IsOpen = false;
    }

    // ---------- Statistics (SD) tab ----------

    void StatKey_Click(object sender, RoutedEventArgs e)
    {
        string t = ((Button)sender).Content?.ToString() ?? "";
        string insert;
        if (t == "DATA+") insert = "data(";
        else if (t == "n") insert = "n(";
        else if (t == "\u03A3x") insert = "sumx(";
        else if (t == "\u03A3x\u00B2") insert = "sumx2(";
        else if (t == "Avg") insert = "mean(";
        else if (t == "\u03C3n") insert = "sd(";
        else if (t == "\u03C3n-1") insert = "sds(";
        else if (t == "Var") insert = "var(";
        else if (t == "Min") insert = "minx(";
        else if (t == "Max") insert = "maxx(";
        else if (t == "CLR") insert = "clrd(";
        else if (t == "DAT2") insert = "dat(";
        else if (t == "LIN") insert = "lin(";
        else if (t == "LOG") insert = "logreg(";
        else if (t == "EXP") insert = "expreg(";
        else if (t == "POW") insert = "powreg(";
        else if (t == "INV") insert = "invreg(";
        else if (t == "QUAD") insert = "quadreg(";
        else if (t == "r") insert = "r(";
        else if (t == "A") insert = "a(";
        else if (t == "B") insert = "b(";
        else if (t == "C") insert = "c(";
        else if (t == "\u03A3xy") insert = "rsumxy(";
        else if (t == "x\u0304") insert = "rmeanx(";
        else if (t == "y\u0304") insert = "rmeany(";
        else if (t == "\u0177") insert = "yhat(";
        else if (t == "x\u0302") insert = "xhat(";
        else insert = t;
        statExpr.Text += insert;
        statExpr.CaretIndex = statExpr.Text.Length;
    }

    void StatEval_Click(object sender, RoutedEventArgs e)
    {
        statMsg.Text = "";
        Result r = NexCalc.Core.Expression.Calculate(statExpr.Text, CurrentMode());
        if (r.ok && r.value != null) {
            statDisplay.Text = Format.Display(r.value!, DisplayFormat.Norm, 0);
        } else {
            statMsg.Text = r.message;
        }
    }

    void StatClear_Click(object sender, RoutedEventArgs e)
    {
        statExpr.Clear();
        statMsg.Text = "";
    }

    void StatExpr_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) StatEval_Click(sender, e);
    }

    // ---------- Programmer (Base-N) tab ----------

    int ProgRadix()
    {
        string sel = (progBase.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "HEX";
        if (sel == "DEC") return 10;
        if (sel == "OCT") return 8;
        if (sel == "BIN") return 2;
        return 16;
    }

    int ProgBits()
    {
        string sel = (progWidth.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "32";
        return int.Parse(sel);
    }

    void ProgEval_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(progExpr.Text)) return;
        int radix = ProgRadix();
        int bits = ProgBits();
        try
        {
            string[] r = Programmer.Run(progExpr.Text, radix, bits);
            progDec.Text = r[0];
            progHex.Text = r[1];
            progOct.Text = r[2];
            progBin.Text = r[3];
            progMsg.Text = "";
        }
        catch (ErrorType ex)
        {
            progMsg.Text = ex.userMsg;
        }
        catch (Exception ex)
        {
            progMsg.Text = ex.Message;
        }
    }

    void ProgClear_Click(object sender, RoutedEventArgs e)
    {
        progExpr.Text = "";
        progDec.Text = "0";
        progHex.Text = "0";
        progOct.Text = "0";
        progBin.Text = "0";
        progMsg.Text = "";
        lastProgRadix = ProgRadix();
        progExpr.Focus();
    }

    int lastProgRadix = 16;

    void ProgBase_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Fires during InitializeComponent before fields are wired; guard.
        if (progDec == null)
        {
            lastProgRadix = ProgRadix();
            return;
        }
        int newRadix = ProgRadix();
        if (newRadix == lastProgRadix) return;
        if (progExpr.Text.Length > 0)
        {
            try
            {
                string conv = Programmer.ConvertBase(progExpr.Text, lastProgRadix, newRadix, ProgBits());
                progExpr.Text = conv;
                progExpr.CaretIndex = progExpr.Text.Length;
            }
            catch (ErrorType ex)
            {
                progMsg.Text = ex.userMsg;
                lastProgRadix = newRadix;
                return;
            }
        }
        lastProgRadix = newRadix;
        if (progExpr.Text.Length > 0) ProgEval_Click(sender, e);
    }

    void ProgWidth_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (progDec == null) return;
        if (progExpr.Text.Length > 0) ProgEval_Click(sender, e);
    }

    void ProgExpr_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ProgEval_Click(sender, e);
            e.Handled = true;
        }
    }

    void ProgKey_Click(object sender, RoutedEventArgs e)
    {
        string ins = ((Button)sender).Content?.ToString() ?? "";
        if (ins == "\u232B")
        {
            string t = progExpr.Text;
            if (t.Length > 0) progExpr.Text = t.Substring(0, t.Length - 1);
            progExpr.CaretIndex = progExpr.Text.Length;
            progExpr.Focus();
            return;
        }
        if (ins == "and" || ins == "or" || ins == "xor" || ins == "shl" || ins == "shr" || ins == "mod")
        {
            ins = " " + ins + " ";
        }
        progExpr.Text = progExpr.Text + ins;
        progExpr.CaretIndex = progExpr.Text.Length;
        progExpr.Focus();
    }
}
