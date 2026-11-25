using System.Numerics;

namespace NexCalc.Core
{
    // One-variable statistics data list (SD mode). Values are kept as Val so exact
    // rationals stay exact through sums, means, and variances.
    public static class Stats {
        public static List<Val> xs = new List<Val>();
        public static List<Val> px = new List<Val>();
        public static List<Val> py = new List<Val>();
        public static string regType = "lin";

        public static void Clear() {
            xs.Clear();
            px.Clear();
            py.Clear();
        }

        public static void ClearPairs() {
            px.Clear();
            py.Clear();
        }

        // Exact sums: return null if any stored value is not exact.
        public static Rat? SumX() {
            Rat? s = Rat.OfInt(0);
            foreach (Val v in xs) {
                if (!v.exact || v.r == null) return null;
                s = s!.Add(v.r!);
            }
            return s;
        }

        public static Rat? SumX2() {
            Rat? s = Rat.OfInt(0);
            foreach (Val v in xs) {
                if (!v.exact || v.r == null) return null;
                s = s!.Add(v.r!.Mul(v.r!));
            }
            return s;
        }

        public static double SumXDouble() {
            double s = 0.0;
            foreach (Val v in xs) s += v.ToDoubleValue();
            return s;
        }

        public static double SumX2Double() {
            double s = 0.0;
            foreach (Val v in xs) {
                double d = v.ToDoubleValue();
                s += d * d;
            }
            return s;
        }

        // Compare two rationals by cross-multiplication (BigInteger-safe).
        public static int CmpRat(Rat a, Rat b) {
            BigInteger left = a.num * b.den;
            BigInteger right = b.num * a.den;
            return BigInteger.Compare(left, right);
        }

        // min/max: pick the original Val so exact values survive.
        public static Val MinVal() {
            Val best = xs[0];
            foreach (Val v in xs) {
                if (v.exact && best.exact && v.r != null && best.r != null) {
                    if (CmpRat(v.r!, best.r!) < 0) best = v;
                } else if (v.ToDoubleValue() < best.ToDoubleValue()) {
                    best = v;
                }
            }
            return best;
        }

        public static Val MaxVal() {
            Val best = xs[0];
            foreach (Val v in xs) {
                if (v.exact && best.exact && v.r != null && best.r != null) {
                    if (CmpRat(v.r!, best.r!) > 0) best = v;
                } else if (v.ToDoubleValue() > best.ToDoubleValue()) {
                    best = v;
                }
            }
            return best;
        }

        // ---------- REG mode: (x,y) pairs, model transforms, fits ----------

        public static double[] ToDoubles(List<Val> list) {
            double[] d = new double[list.Count];
            for (int i = 0; i < list.Count; i++) d[i] = list[i].ToDoubleValue();
            return d;
        }

        // Map pairs to the linearizable (u,v) space for the current model.
        public static void Transform(double[] xs, double[] ys, out double[] us, out double[] vs) {
            int n = xs.Length;
            us = new double[n];
            vs = new double[n];
            string t = regType;
            for (int i = 0; i < n; i++) {
                double x = xs[i], y = ys[i];
                if (t == "log") {
                    if (x <= 0) throw ErrorType.Create("BadReg", "Log regression needs all x > 0.");
                    us[i] = Math.Log(x); vs[i] = y;
                } else if (t == "exp") {
                    us[i] = Math.Exp(x); vs[i] = y;
                } else if (t == "inv") {
                    if (x == 0) throw ErrorType.Create("BadReg", "Inverse regression needs x != 0.");
                    us[i] = 1.0 / x; vs[i] = y;
                } else if (t == "pow") {
                    if (x <= 0 || y <= 0) throw ErrorType.Create("BadReg", "Power regression needs all x > 0 and y > 0.");
                    us[i] = Math.Log(x); vs[i] = Math.Log(y);
                } else {
                    us[i] = x; vs[i] = y;
                }
            }
        }

        // Pearson correlation; throws when the data has no spread.
        public static double Pearson(double[] a, double[] b) {
            int n = a.Length;
            double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0;
            for (int i = 0; i < n; i++) {
                sa += a[i]; sb += b[i];
                saa += a[i] * a[i]; sbb += b[i] * b[i];
                sab += a[i] * b[i];
            }
            double den = Math.Sqrt((n * saa - sa * sa) * (n * sbb - sb * sb));
            if (den == 0.0) throw ErrorType.Create("BadReg", "Correlation is not defined for this data.");
            return (n * sab - sa * sb) / den;
        }

        // 2-parameter fit (lin, log, exp, pow, inv): returns {A, B}.
        public static double[] FitAB() {
            int n = px.Count;
            if (n == 0) throw ErrorType.Create("NoData", "No data pairs. Add one with dat() first.");
            if (n < 2) throw ErrorType.Create("BadReg", "Regression needs at least 2 data pairs.");
            double[] xs = ToDoubles(px), ys = ToDoubles(py);
            Transform(xs, ys, out double[] us, out double[] vs);
            double su = 0, sv = 0, suv = 0, suu = 0;
            for (int i = 0; i < n; i++) {
                su += us[i]; sv += vs[i];
                suv += us[i] * vs[i]; suu += us[i] * us[i];
            }
            double denom = n * suu - su * su;
            if (denom == 0.0) throw ErrorType.Create("BadReg", "Regression: all x values are equal, so the model is not defined.");
            double slope = (n * suv - su * sv) / denom;
            double inter = (sv - slope * su) / n;
            string t = regType;
            if (t == "pow") return new double[] { Math.Exp(inter), slope };
            if (t == "log" || t == "exp") return new double[] { inter, slope };
            return new double[] { slope, inter };
        }

        // 3-parameter quadratic fit: returns {A, B, C} for y = A x^2 + B x + C.
        public static double[] FitQuad() {
            int n = px.Count;
            if (n == 0) throw ErrorType.Create("NoData", "No data pairs. Add one with dat() first.");
            if (n < 3) throw ErrorType.Create("BadReg", "Quadratic regression needs at least 3 data pairs.");
            double[] xs = ToDoubles(px), ys = ToDoubles(py);
            double s1 = 0, s2 = 0, s3 = 0, s4 = 0, sy = 0, sxy = 0, sx2y = 0;
            for (int i = 0; i < n; i++) {
                double x = xs[i], y = ys[i];
                s1 += x; s2 += x * x; s3 += x * x * x; s4 += x * x * x * x;
                sy += y; sxy += x * y; sx2y += x * x * y;
            }
            double[,] m = new double[3, 4];
            m[0, 0] = s4; m[0, 1] = s3; m[0, 2] = s2; m[0, 3] = sx2y;
            m[1, 0] = s3; m[1, 1] = s2; m[1, 2] = s1; m[1, 3] = sxy;
            m[2, 0] = s2; m[2, 1] = s1; m[2, 2] = n; m[2, 3] = sy;
            for (int col = 0; col < 3; col++) {
                int piv = col;
                for (int rr = col + 1; rr < 3; rr++) {
                    if (Math.Abs(m[rr, col]) > Math.Abs(m[piv, col])) piv = rr;
                }
                if (Math.Abs(m[piv, col]) < 1e-15) throw ErrorType.Create("BadReg", "Quadratic regression: data is degenerate for a 3-parameter fit.");
                if (piv != col) {
                    for (int k = 0; k < 4; k++) { double tmp = m[col, k]; m[col, k] = m[piv, k]; m[piv, k] = tmp; }
                }
                for (int rr = col + 1; rr < 3; rr++) {
                    double f = m[rr, col] / m[col, col];
                    for (int k = col; k < 4; k++) m[rr, k] -= f * m[col, k];
                }
            }
            double[] sol = new double[3];
            for (int k = 2; k >= 0; k--) {
                double sum = m[k, 3];
                for (int j = k + 1; j < 3; j++) sum -= m[k, j] * sol[j];
                sol[k] = sum / m[k, k];
            }
            return new double[] { sol[0], sol[1], sol[2] };
        }

        public static double CorrAB() {
            double[] xs = ToDoubles(px), ys = ToDoubles(py);
            Transform(xs, ys, out double[] us, out double[] vs);
            return Pearson(us, vs);
        }

        // Correlation for the quadratic fit: agreement between y and the fitted values.
        public static double CorrQuad() {
            double[] abc = FitQuad();
            double[] xs = ToDoubles(px), ys = ToDoubles(py);
            double[] fit = new double[xs.Length];
            for (int i = 0; i < xs.Length; i++) fit[i] = abc[0] * xs[i] * xs[i] + abc[1] * xs[i] + abc[2];
            return Pearson(ys, fit);
        }

        // Model value at x for the current fit.
        public static double ModelY(double x) {
            string t = regType;
            if (t == "quad") {
                double[] abc = FitQuad();
                return abc[0] * x * x + abc[1] * x + abc[2];
            }
            double[] ab = FitAB();
            double A = ab[0], B = ab[1];
            if (t == "lin") return A * x + B;
            if (t == "log") {
                if (x <= 0) throw ErrorType.Create("BadReg", "yhat needs x > 0 for log regression.");
                return A + B * Math.Log(x);
            }
            if (t == "exp") return A + B * Math.Exp(x);
            if (t == "pow") {
                if (x <= 0) throw ErrorType.Create("BadReg", "yhat needs x > 0 for power regression.");
                return A * Math.Pow(x, B);
            }
            if (x == 0) throw ErrorType.Create("BadReg", "yhat needs x != 0 for inverse regression.");
            return A / x + B;
        }

        // Solve the model for x given y.
        public static double ModelX(double y) {
            string t = regType;
            if (t == "quad") {
                double[] abc = FitQuad();
                if (abc[0] == 0) {
                    if (abc[1] == 0) throw ErrorType.Create("BadReg", "xhat is not defined for this fit.");
                    return (y - abc[2]) / abc[1];
                }
                double disc = abc[1] * abc[1] - 4 * abc[0] * (abc[2] - y);
                if (disc < 0) throw ErrorType.Create("BadReg", "xhat: no real x gives that y.");
                return (-abc[1] + Math.Sqrt(disc)) / (2 * abc[0]);
            }
            double[] ab = FitAB();
            double A = ab[0], B = ab[1];
            if (t == "lin") {
                if (A == 0) throw ErrorType.Create("BadReg", "xhat is not defined for this fit.");
                return (y - B) / A;
            }
            if (t == "log") {
                if (B == 0) throw ErrorType.Create("BadReg", "xhat is not defined for this fit.");
                return Math.Exp((y - A) / B);
            }
            if (t == "exp") {
                if (B == 0) throw ErrorType.Create("BadReg", "xhat is not defined for this fit.");
                double q = (y - A) / B;
                if (q <= 0) throw ErrorType.Create("BadReg", "xhat: no real x gives that y for exp regression.");
                return Math.Log(q);
            }
            if (t == "pow") {
                if (A == 0 || B == 0) throw ErrorType.Create("BadReg", "xhat is not defined for this fit.");
                double q = y / A;
                if (q <= 0) throw ErrorType.Create("BadReg", "xhat: no real x gives that y for power regression.");
                return Math.Pow(q, 1.0 / B);
            }
            if (y == B) throw ErrorType.Create("BadReg", "xhat: no real x gives that y for inverse regression.");
            return A / (y - B);
        }
    }
}
