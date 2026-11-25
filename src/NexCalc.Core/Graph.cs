using System.Numerics;

namespace NexCalc.Core
{
    // Sampling plan for 2D function plots and polar plots. The UI draws the
    // polyline segments; NaN marks breaks (domain gaps / asymptotes) and
    // 'breaks' counts them so the UI can decide whether a vertical asymptote
    // should be drawn as a break rather than a connecting spike.
    public class GraphPlan {
        public double[] xs = new double[0];
        public double[] ys = new double[0];      // cartesian: y values; polar: radius values
        public double[]? polarXs; // polar mode: cartesian x of each point (else null)
        public double[]? polarYs; // polar mode: cartesian y of each point (else null)
        public double yMin;
        public double yMax;
        public int breaks;       // number of NaN transitions (usable segments - 1)
        public bool ok;          // expression parsed successfully
        public string? message;   // parse error text when ok == false
    }

    public class Graph {
        // Uniform sampling of y = f(x) over [xMin, xMax].
        public static GraphPlan Plan(string expr, double xMin, double xMax, int samples, AngleMode mode) {
            GraphPlan plan = new GraphPlan();
            plan.xs = new double[samples];
            plan.ys = new double[samples];
            plan.polarXs = null;
            ANode node;
            try {
                node = Parser.Parse(expr);
            } catch (ErrorType e) {
                plan.ok = false;
                plan.message = e.userMsg;
                plan.yMin = -1.0;
                plan.yMax = 1.0;
                plan.breaks = 0;
                return plan;
            }
            plan.ok = true;
            plan.message = null;
            double step = (xMax - xMin) / (samples - 1);
            double lo = 1e300;
            double hi = -1e300;
            int breaks = 0;
            bool prevFinite = false;
            for (int k = 0; k < samples; k = k + 1) {
                double x = xMin + step * k;
                double y;
                try {
                    Val v = Eval.Evaluate(node, mode, Val.OfDouble(x));
                    y = v.ToDoubleValue();
                } catch (ErrorType) {
                    y = Double.NaN;
                }
                if (Eval.IsNaN(y)) {
                    y = Double.NaN;
                }
                // clamp absurd outliers so the auto-range doesn't explode,
                // but keep them finite-adjacent so asymptotes read correctly
                if (y > 1.0e15) y = 1.0e15;
                if (y < -1.0e15) y = -1.0e15;
                plan.xs[k] = x;
                plan.ys[k] = y;
                bool finite = !Eval.IsNaN(y);
                if (finite) {
                    if (y < lo) lo = y;
                    if (y > hi) hi = y;
                }
                if (finite && !prevFinite && k > 0) breaks = breaks + 1;
                if (!finite && prevFinite) breaks = breaks + 1;
                prevFinite = finite;
            }
            if (lo > hi) { lo = -1.0; hi = 1.0; }   // all-NaN function
            plan.yMin = lo;
            plan.yMax = hi;
            plan.breaks = breaks;
            return plan;
        }

        // Polar plot r = f(theta). Values are evaluated under the given
        // angle convention and converted to cartesian points for drawing.
        public static GraphPlan PlanPolar(string expr, double thetaMin, double thetaMax, int samples, AngleMode mode) {
            GraphPlan plan = new GraphPlan();
            plan.xs = new double[samples];      // theta values
            plan.ys = new double[samples];      // radius values
            plan.polarXs = new double[samples]; // cartesian x
            plan.polarYs = new double[samples]; // cartesian y
            ANode node;
            try {
                node = Parser.Parse(expr);
            } catch (ErrorType e) {
                plan.ok = false;
                plan.message = e.userMsg;
                plan.yMin = -1.0;
                plan.yMax = 1.0;
                plan.breaks = 0;
                return plan;
            }
            plan.ok = true;
            plan.message = null;
            double step = (thetaMax - thetaMin) / (samples - 1);
            double lo = 1e300;
            double hi = -1e300;
            int breaks = 0;
            bool prevFinite = false;
            for (int k = 0; k < samples; k = k + 1) {
                double th = thetaMin + step * k;
                double r;
                try {
                    Val v = Eval.Evaluate(node, mode, Val.OfDouble(th));
                    r = v.ToDoubleValue();
                } catch (ErrorType) {
                    r = Double.NaN;
                }
                if (Eval.IsNaN(r) || r > 1e300 || r < -1e300) {
                    r = Double.NaN;
                }
                plan.xs[k] = th;
                plan.ys[k] = r;
                if (!Eval.IsNaN(r)) {
                    double thetaRad = Graph.ToRadValue(th, mode);
                    plan.polarXs[k] = r * Math.Cos(thetaRad);
                    plan.polarYs[k] = r * Math.Sin(thetaRad);
                    if (r < lo) lo = r;
                    if (r > hi) hi = r;
                } else {
                    plan.polarXs[k] = Double.NaN;
                    plan.polarYs[k] = Double.NaN;
                }
                bool finite = !Eval.IsNaN(r);
                if (finite && !prevFinite && k > 0) breaks = breaks + 1;
                if (!finite && prevFinite) breaks = breaks + 1;
                prevFinite = finite;
            }
            if (lo > hi) { lo = -1.0; hi = 1.0; }
            plan.yMin = lo;
            plan.yMax = hi;
            plan.breaks = breaks;
            return plan;
        }

        // Parametric plot X = f(x), Y = g(x) sweeping the parameter (the engine
        // variable x) over [tMin, tMax]. polarXs/polarYs carry the cartesian
        // points so the UI uses the same uniform-scale path as polar plots.
        public static GraphPlan PlanParam(string xExpr, string yExpr, double tMin, double tMax, int samples, AngleMode mode) {
            GraphPlan plan = new GraphPlan();
            plan.xs = new double[samples];      // parameter values
            plan.ys = new double[samples];      // X values
            plan.polarXs = new double[samples]; // cartesian x
            plan.polarYs = new double[samples]; // cartesian y
            ANode nx;
            ANode ny;
            try {
                nx = Parser.Parse(xExpr);
                ny = Parser.Parse(yExpr);
            } catch (ErrorType e) {
                plan.ok = false;
                plan.message = e.userMsg;
                plan.yMin = -1.0;
                plan.yMax = 1.0;
                plan.breaks = 0;
                return plan;
            }
            plan.ok = true;
            plan.message = null;
            double step = (tMax - tMin) / (samples - 1);
            double lo = 1e300;
            double hi = -1e300;
            int breaks = 0;
            bool prevFinite = false;
            for (int k = 0; k < samples; k = k + 1) {
                double t = tMin + step * k;
                double xv;
                double yv;
                try {
                    Val v1 = Eval.Evaluate(nx, mode, Val.OfDouble(t));
                    xv = v1.ToDoubleValue();
                } catch (ErrorType) {
                    xv = Double.NaN;
                }
                try {
                    Val v2 = Eval.Evaluate(ny, mode, Val.OfDouble(t));
                    yv = v2.ToDoubleValue();
                } catch (ErrorType) {
                    yv = Double.NaN;
                }
                if (Eval.IsNaN(xv)) xv = Double.NaN;
                if (Eval.IsNaN(yv)) yv = Double.NaN;
                if (xv > 1.0e15) xv = 1.0e15;
                if (xv < -1.0e15) xv = -1.0e15;
                if (yv > 1.0e15) yv = 1.0e15;
                if (yv < -1.0e15) yv = -1.0e15;
                plan.xs[k] = t;
                plan.ys[k] = xv;
                plan.polarXs[k] = xv;
                plan.polarYs[k] = yv;
                bool finite = !Eval.IsNaN(xv) && !Eval.IsNaN(yv);
                if (finite) {
                    if (yv < lo) lo = yv;
                    if (yv > hi) hi = yv;
                }
                if (finite && !prevFinite && k > 0) breaks = breaks + 1;
                if (!finite && prevFinite) breaks = breaks + 1;
                prevFinite = finite;
            }
            if (lo > hi) { lo = -1.0; hi = 1.0; }
            plan.yMin = lo;
            plan.yMax = hi;
            plan.breaks = breaks;
            return plan;
        }

        // Angle convention -> radians, mirroring Eval.ToRad for plain doubles.
        public static double ToRadValue(double v, AngleMode mode) {
            if (mode == AngleMode.Deg) return v * (3.141592653589793 / 180.0);
            if (mode == AngleMode.Grad) return v * (3.141592653589793 / 200.0);
            return v;
        }
    }
}
