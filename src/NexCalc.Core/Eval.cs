using System.Numerics;

namespace NexCalc.Core
{
    // Angle convention for trig functions, like the Windows DEG/RAD/GRAD modes.
    public enum AngleMode { Deg, Rad, Grad }

    public class Eval {
        public static double piValue = 3.141592653589793;

        public static bool IsNaN(double d) {
            return double.IsNaN(d);
        }

        // Last Pol/Rec pair: pol stores (x,y), rec stores (r,theta).
        public static Val? lastX = null;
        public static Val? lastY = null;

        // Memory (M key) and named variables A-Z (lowercased by the parser).
        public static Val? memVal = null;
        public static Dictionary<string, Val> vars = new Dictionary<string, Val>();

        // Matrix memory MatA..MatF: mat() fills the next free slot in order.
        public static Val?[] matSlots = new Val?[6];

        static Val AddVal(Val a, Val b) {
            if (a.exact && b.exact && a.r != null && b.r != null) return Val.OfRat(a.r!.Add(b.r!));
            return Val.OfDouble(a.ToDoubleValue() + b.ToDoubleValue());
        }

        static Val MulVal(Val a, Val b) {
            if (a.exact && b.exact && a.r != null && b.r != null) return Val.OfRat(a.r!.Mul(b.r!));
            return Val.OfDouble(a.ToDoubleValue() * b.ToDoubleValue());
        }

        // ---- Matrix helpers ----
        // Matrices are rows of scalar Val entries (never nested). Matrices
        // whose entries are all exact rationals keep exact arithmetic
        // through determinant and Gauss-Jordan inversion.

        static string MName(int i) { return "Mat" + (char)('A' + i); }

        static bool AllExact(Val[][] m) {
            for (int i = 0; i < m.Length; i = i + 1) {
                for (int j = 0; j < m[i].Length; j = j + 1) {
                    if (!m[i][j].exact || m[i][j].r == null) return false;
                }
            }
            return true;
        }

        static Val[][] MatrixAdd(Val[][] a, Val[][] b, bool negate) {
            if (a.Length != b.Length || a[0].Length != b[0].Length) {
                throw ErrorType.Create("BadMatrixDims", "Matrix dimensions do not match.");
            }
            Val[][] r = new Val[a.Length][];
            for (int i = 0; i < a.Length; i = i + 1) {
                r[i] = new Val[a[i].Length];
                for (int j = 0; j < a[i].Length; j = j + 1) {
                    r[i][j] = negate ? SubValues(a[i][j], b[i][j]) : AddValues(a[i][j], b[i][j]);
                }
            }
            return r;
        }

        static Val[][] MatrixScl(Val s, Val[][] m) {
            Val[][] r = new Val[m.Length][];
            for (int i = 0; i < m.Length; i = i + 1) {
                r[i] = new Val[m[i].Length];
                for (int j = 0; j < m[i].Length; j = j + 1) r[i][j] = MulValues(s, m[i][j]);
            }
            return r;
        }

        static Val[][] MatrixDiv(Val[][] m, Val s) {
            Val[][] r = new Val[m.Length][];
            for (int i = 0; i < m.Length; i = i + 1) {
                r[i] = new Val[m[i].Length];
                for (int j = 0; j < m[i].Length; j = j + 1) r[i][j] = DivValues(m[i][j], s);
            }
            return r;
        }

        static Val[][] MatrixMul(Val[][] a, Val[][] b) {
            if (a[0].Length != b.Length) {
                throw ErrorType.Create("BadMatrixDims", "Cannot multiply: columns of the left matrix must equal rows of the right.");
            }
            Val[][] r = new Val[a.Length][];
            for (int i = 0; i < a.Length; i = i + 1) {
                r[i] = new Val[b[0].Length];
                for (int j = 0; j < b[0].Length; j = j + 1) {
                    Val sum = MulValues(a[i][0], b[0][j]);
                    for (int k = 1; k < a[0].Length; k = k + 1) {
                        sum = AddValues(sum, MulValues(a[i][k], b[k][j]));
                    }
                    r[i][j] = sum;
                }
            }
            return r;
        }

        static Val[][] TransposeOf(Val[][] m) {
            int rows = m.Length, cols = m[0].Length;
            Val[][] r = new Val[cols][];
            for (int i = 0; i < cols; i = i + 1) {
                r[i] = new Val[rows];
                for (int j = 0; j < rows; j = j + 1) r[i][j] = m[j][i];
            }
            return r;
        }

        static ErrorType MatrixSingular() {
            return ErrorType.Create("NoSolution", "Matrix is singular - no inverse.");
        }

        static Val DetOf(Val[][] m) {
            int n = m.Length;
            if (n == 1) return m[0][0];
            if (AllExact(m)) {
                Val? sum = null;
                for (int j = 0; j < n; j = j + 1) {
                    Val[][] minor = new Val[n - 1][];
                    for (int i = 1; i < n; i = i + 1) {
                        Val[] row = new Val[n - 1];
                        int p = 0;
                        for (int c = 0; c < n; c = c + 1) {
                            if (c == j) continue;
                            row[p] = m[i][c];
                            p = p + 1;
                        }
                        minor[i - 1] = row;
                    }
                    Val term = MulValues(m[0][j], DetOf(minor));
                    if ((j % 2) == 1) term = SubValues(Val.OfInt(0), term);
                    sum = sum == null ? term : AddValues(sum, term);
                }
                return sum!;
            }
            double[][] d = new double[n][];
            for (int i = 0; i < n; i = i + 1) {
                d[i] = new double[n];
                for (int j = 0; j < n; j = j + 1) d[i][j] = m[i][j].ToDoubleValue();
            }
            double det = DetD(d);
            if (IsNaN(det)) throw ErrorType.NotANumber();
            return Val.OfDouble(det);
        }

        static double DetD(double[][] d) {
            int n = d.Length;
            if (n == 1) return d[0][0];
            double sum = 0.0;
            for (int j = 0; j < n; j = j + 1) {
                double[][] minor = new double[n - 1][];
                for (int i = 1; i < n; i = i + 1) {
                    double[] row = new double[n - 1];
                    int p = 0;
                    for (int c = 0; c < n; c = c + 1) {
                        if (c == j) continue;
                        row[p] = d[i][c];
                        p = p + 1;
                    }
                    minor[i - 1] = row;
                }
                double term = d[0][j] * DetD(minor);
                if ((j % 2) == 1) term = -term;
                sum = sum + term;
            }
            return sum;
        }

        static Val[][] InvertOf(Val[][] m) {
            int n = m.Length;
            if (AllExact(m)) {
                Val[][] a = new Val[n][];
                for (int i = 0; i < n; i = i + 1) {
                    a[i] = new Val[2 * n];
                    for (int j = 0; j < n; j = j + 1) a[i][j] = m[i][j];
                    for (int j = 0; j < n; j = j + 1) a[i][n + j] = (j == i) ? Val.OfInt(1) : Val.OfInt(0);
                }
                for (int col = 0; col < n; col = col + 1) {
                    int piv = -1;
                    for (int i = col; i < n; i = i + 1) {
                        if (a[i][col].r!.Signum() != 0) { piv = i; break; }
                    }
                    if (piv < 0) throw MatrixSingular();
                    Val[] tmp = a[col]; a[col] = a[piv]; a[piv] = tmp;
                    Val pv = a[col][col];
                    for (int j = 0; j < 2 * n; j = j + 1) a[col][j] = Val.OfRat(a[col][j].r!.Div(pv.r!));
                    for (int i = 0; i < n; i = i + 1) {
                        if (i == col) continue;
                        if (a[i][col].r!.Signum() == 0) continue;
                        Val f = a[i][col];
                        for (int j = 0; j < 2 * n; j = j + 1) {
                            a[i][j] = Val.OfRat(a[i][j].r!.Sub(f.r!.Mul(a[col][j].r!)));
                        }
                    }
                }
                Val[][] r = new Val[n][];
                for (int i = 0; i < n; i = i + 1) {
                    r[i] = new Val[n];
                    for (int j = 0; j < n; j = j + 1) r[i][j] = a[i][n + j];
                }
                return r;
            }
            double[][] a2 = new double[n][];
            for (int i = 0; i < n; i = i + 1) {
                a2[i] = new double[2 * n];
                for (int j = 0; j < n; j = j + 1) a2[i][j] = m[i][j].ToDoubleValue();
                for (int j = 0; j < n; j = j + 1) a2[i][n + j] = (j == i) ? 1.0 : 0.0;
            }
            for (int col = 0; col < n; col = col + 1) {
                int piv = -1;
                double best = 1e-12;
                for (int i = col; i < n; i = i + 1) {
                    if (Math.Abs(a2[i][col]) > best) { best = Math.Abs(a2[i][col]); piv = i; }
                }
                if (piv < 0) throw MatrixSingular();
                double[] tmp = a2[col]; a2[col] = a2[piv]; a2[piv] = tmp;
                double pv = a2[col][col];
                for (int j = 0; j < 2 * n; j = j + 1) a2[col][j] = a2[col][j] / pv;
                for (int i = 0; i < n; i = i + 1) {
                    if (i == col) continue;
                    double f = a2[i][col];
                    if (f == 0.0) continue;
                    for (int j = 0; j < 2 * n; j = j + 1) a2[i][j] = a2[i][j] - f * a2[col][j];
                }
            }
            Val[][] r2 = new Val[n][];
            for (int i = 0; i < n; i = i + 1) {
                r2[i] = new Val[n];
                for (int j = 0; j < n; j = j + 1) {
                    double v = a2[i][n + j];
                    if (IsNaN(v)) throw ErrorType.NotANumber();
                    r2[i][j] = Val.OfDouble(v);
                }
            }
            return r2;
        }

        // Vector view: one-row or one-column matrices flatten to a length.
        static Val[] FlatOf(Val v) {
            Val[][] m = v.mat!;
            if (m.Length != 1 && m[0].Length != 1) {
                throw ErrorType.Create("BadMatrixDims", "dot(), cross() and vmag() need vectors: one-row or one-column matrices.");
            }
            List<Val> f = new List<Val>();
            for (int i = 0; i < m.Length; i = i + 1) {
                for (int j = 0; j < m[i].Length; j = j + 1) f.Add(m[i][j]);
            }
            return f.ToArray();
        }

        public static bool IsFinite(double d) {
            return !IsNaN(d) && d >= -1.7976931348623157e308 && d <= 1.7976931348623157e308;
        }

        public static double ToRad(Val v, AngleMode mode) {
            double d = v.ToDoubleValue();
            if (mode == AngleMode.Deg) return d * (piValue / 180.0);
            if (mode == AngleMode.Grad) return d * (piValue / 200.0);
            return d;
        }

        // Inverse-angle conversion: a radian result back into the current
        // angle mode (Windows Calc: asin(1) = 90 in DEG).
        public static Val RadToAngle(double rad, AngleMode mode) {
            if (mode == AngleMode.Deg) return Val.OfDouble(rad * 180.0 / piValue);
            if (mode == AngleMode.Grad) return Val.OfDouble(rad * 200.0 / piValue);
            return Val.OfDouble(rad);
        }

        // Known exact inverse-trig values, so common inputs land on clean
        // angles (acos(0.5) => 60 in DEG) instead of float noise.
        public static Val? SpecialInvTrig(string fn, double d, AngleMode mode) {
            double deg = double.NaN;
            double s2 = Math.Sqrt(2.0) / 2.0;
            double s3 = Math.Sqrt(3.0) / 2.0;
            if (fn == "asin") {
                if (d == 1.0) deg = 90.0;
                else if (d == 0.5) deg = 30.0;
                else if (d == s2) deg = 45.0;
                else if (d == s3) deg = 60.0;
                else if (d == -0.5) deg = -30.0;
                else if (d == -1.0) deg = -90.0;
                else if (d == -s2) deg = -45.0;
                else if (d == -s3) deg = -60.0;
                else if (d == 0.0) deg = 0.0;
            }
            if (fn == "acos") {
                if (d == 1.0) deg = 0.0;
                else if (d == 0.5) deg = 60.0;
                else if (d == s2) deg = 45.0;
                else if (d == s3) deg = 30.0;
                else if (d == 0.0) deg = 90.0;
                else if (d == -s3) deg = 120.0;
                else if (d == -s2) deg = 135.0;
                else if (d == -0.5) deg = 120.0;
                else if (d == -1.0) deg = 180.0;
            }
            if (double.IsNaN(deg)) return null;
            if (mode == AngleMode.Deg) return Val.OfDouble(deg);
            if (mode == AngleMode.Grad) return Val.OfDouble(deg * 200.0 / 180.0);
            return Val.OfDouble(deg * piValue / 180.0);
        }

        // Convert an exact angle to integer degrees mod 360 when it is an
        // exact whole-degree value; null otherwise (Niven-theorem lookups).
        public static long? ToIntegerDegrees(Rat r, AngleMode mode) {
            BigInteger m = BigInteger.Zero;
            BigInteger threeSixty = new BigInteger(360);
            if (mode == AngleMode.Deg) {
                if (!r.IsInteger()) return null;
                m = r.num % threeSixty;
            } else if (mode == AngleMode.Rad) {
                BigInteger n2 = r.num * new BigInteger(180);
                if ((n2 % r.den) != BigInteger.Zero) return null;
                m = (n2 / r.den) % threeSixty;
            } else {
                BigInteger n2 = r.num * new BigInteger(9);
                BigInteger d2 = r.den * new BigInteger(10);
                if ((n2 % d2) != BigInteger.Zero) return null;
                m = (n2 / d2) % threeSixty;
            }
            if (m < BigInteger.Zero) m = m + threeSixty;
            return long.Parse(m.ToString());
        }

        // Exact trig values for whole-degree angles: rationals only, per
        // Niven's theorem, plus the half-angle classics 30/150.
        public static Rat? SinTableAt(long m) {
            if (m == 0 || m == 180) return Rat.OfInt(0);
            if (m == 90) return Rat.OfInt(1);
            if (m == 270) return Rat.OfInt(-1);
            if (m == 30 || m == 150) return Rat.Make(new BigInteger(1), new BigInteger(2));
            if (m == 210 || m == 330) return Rat.Make(new BigInteger(-1), new BigInteger(2));
            return null;
        }

        public static Rat? CosTableAt(long m) {
            return SinTableAt((m + 90) % 360);
        }

        // null when not in the exact table; throws where tangent is undefined.
        public static Rat? TanTableAt(long m) {
            if (m == 90 || m == 270) {
                throw ErrorType.Undefined("tangent is undefined at this angle: the value grows without bound");
            }
            if (m == 0 || m == 180) return Rat.OfInt(0);
            if (m == 45 || m == 225) return Rat.OfInt(1);
            if (m == 135 || m == 315) return Rat.OfInt(-1);
            return null;
        }

        public static Val TrigWrap(string fn, Val v, AngleMode mode) {
            RequireReal(v);
            if (v.exact) {
                long? m = ToIntegerDegrees(v.r!, mode);
                if (m != null) {
                    Rat? t = null;
                    if (fn == "sin") t = SinTableAt(m.Value);
                    else if (fn == "cos") t = CosTableAt(m.Value);
                    else if (fn == "tan") t = TanTableAt(m.Value);
                    else if (fn == "cot") {
                        Rat? s = SinTableAt(m.Value);
                        Rat? c = CosTableAt(m.Value);
                        if (s != null && c != null) {
                            if (s.IsZero()) {
                                throw ErrorType.ZeroDivision();
                            }
                            t = c.Div(s);
                        }
                    }
                    if (t != null) return Val.OfRat(t);
                }
            }
            double d = ToRad(v, mode);
            double res;
            if (fn == "sin") res = Math.Sin(d);
            else if (fn == "cos") res = Math.Cos(d);
            else if (fn == "tan") res = Math.Tan(d);
            else res = 1.0 / Math.Tan(d);
            return Val.OfDouble(res);
        }

        public static long PowTenCount(BigInteger v) {
            BigInteger t = v;
            if (t < BigInteger.Zero) t = -t;
            if (t == BigInteger.Zero) return -1;
            long count = 0;
            BigInteger tenB = new BigInteger(10);
            while ((t % tenB) != BigInteger.Zero) {
                t = t / tenB;
                count = count + 1;
            }
            if (!t.Equals(BigInteger.One)) return -1;
            return count;
        }

        public static BigInteger ToBigInt(Val v) {
            if (v.complex) {
                throw ErrorType.NotAnInteger();
            }
            if (v.exact) {
                if (!v.r!.IsInteger()) {
                    throw ErrorType.NotAnInteger();
                }
                return v.r!.num;
            }
            double d = v.approx;
            if (!IsFinite(d)) {
                throw ErrorType.ValueTooLarge();
            }
            if (d != Math.Round(d) || d > 1.0e15 || d < -1.0e15) {
                throw ErrorType.NotAnInteger();
            }
            return new BigInteger(Math.Round(d));
        }

        public static Val OfBigInt(BigInteger v) {
            return Val.OfRat(Rat.Make(v, BigInteger.One));
        }

        public static long? BigToLongOrNull(BigInteger v) {
            try {
                return long.Parse(v.ToString());
            } catch (FormatException) {
                return null;
            }
        }

        // Number theory helpers (iteration 16). IntArgOf rejects matrices and
        // digit-string results before reusing the engine's integer converter.
        private static BigInteger IntArgOf(Val v) {
            if (v.mat != null || v.text != null) throw ErrorType.NotAnInteger();
            return ToBigInt(v);
        }

        private static string ToRadix(BigInteger n, int radix) {
            if (n == BigInteger.Zero) return "0";
            bool neg = n < BigInteger.Zero;
            BigInteger abs = neg ? -n : n;
            string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string res = "";
            while (abs > BigInteger.Zero) {
                BigInteger rem = abs % radix;
                abs = abs / radix;
                res = digits[(int)rem] + res;
            }
            return neg ? "-" + res : res;
        }

        private static bool IsPrimeLong(long n) {
            if (n < 2) return false;
            if (n % 2 == 0) return n == 2;
            long i = 3;
            while (i * i <= n) {
                if (n % i == 0) return false;
                i = i + 2;
            }
            return true;
        }

        private static List<long> FactorList(long n) {
            List<long> fs = new List<long>();
            while (n % 2 == 0) {
                fs.Add(2);
                n = n / 2;
            }
            long i = 3;
            while (i * i <= n) {
                while (n % i == 0) {
                    fs.Add(i);
                    n = n / i;
                }
                i = i + 2;
            }
            if (n > 1) fs.Add(n);
            return fs;
        }

        private static List<long> DivisorList(long n) {
            List<long> ds = new List<long>();
            long i = 1;
            while (i * i <= n) {
                if (n % i == 0) {
                    ds.Add(i);
                    if (i * i != n) ds.Add(n / i);
                }
                i = i + 1;
            }
            ds.Sort();
            return ds;
        }

        public static Val Power(Val a, Val b) {
            if (a.mat != null || b.mat != null) {
                throw ErrorType.Create("InvalidInput", "This operation does not support matrices.");
            }
            if (a.complex || b.complex) {
                // integer exponent on a complex base: repeated multiplication
                if (a.complex && b.exact && b.r!.IsInteger()) {
                    long? e = BigToLongOrNull(b.r!.num);
                    if (e == null || Math.Abs(e.Value) > 100000L) throw ErrorType.ValueTooLarge();
                    double zr = 1.0, zi = 0.0;
                    double br = a.re, bi = a.im;
                    long n = e.Value < 0L ? -e.Value : e.Value;
                    for (long k = 0L; k < n; k = k + 1L) {
                        double nr = zr * br - zi * bi;
                        double ni = zr * bi + zi * br;
                        if (IsNaN(nr) || IsNaN(ni)) throw ErrorType.NotANumber();
                        if (Math.Abs(nr) > 1.7976931348623157e308 || Math.Abs(ni) > 1.7976931348623157e308) throw ErrorType.ValueTooLarge();
                        zr = nr;
                        zi = ni;
                    }
                    if (e.Value < 0L) {
                        double d2 = zr * zr + zi * zi;
                        if (d2 == 0.0) throw ErrorType.ZeroDivision();
                        return Val.OfComplex(zr / d2, -zi / d2);
                    }
                    return Val.OfComplex(zr, zi);
                }
                // principal value: a^b = exp(b * ln(a))
                double ar = a.complex ? a.re : a.ToDoubleValue();
                double ai = a.complex ? a.im : 0.0;
                if (ar == 0.0 && ai == 0.0) {
                    if (b.complex) throw ErrorType.ZeroDivision();
                    double bd0 = b.ToDoubleValue();
                    if (bd0 < 0.0) throw ErrorType.ZeroDivision();
                    return Val.OfComplex(0.0, 0.0);
                }
                double lnr = Math.Log(ar * ar + ai * ai) / 2.0;
                double lni = Math.Atan2(ai, ar);
                double br2 = b.complex ? b.re : b.ToDoubleValue();
                double bi2 = b.complex ? b.im : 0.0;
                double xr = br2 * lnr - bi2 * lni;
                double xi = br2 * lni + bi2 * lnr;
                double m2 = Math.Exp(xr);
                if (Math.Abs(m2) > 1.7976931348623157e308) throw ErrorType.ValueTooLarge();
                double rr = m2 * Math.Cos(xi);
                double ri = m2 * Math.Sin(xi);
                if (IsNaN(rr) || IsNaN(ri)) throw ErrorType.NotANumber();
                if (Math.Abs(rr) > 1.7976931348623157e308 || Math.Abs(ri) > 1.7976931348623157e308) throw ErrorType.ValueTooLarge();
                return Val.OfComplex(rr, ri);
            }
            if (a.exact && b.exact) {
                if (a.r!.IsZero()) {
                    if (b.r!.Signum() < 0) throw ErrorType.ZeroDivision();
                    return Val.OfRat(Rat.OfInt(0));
                }
                if (b.r!.IsInteger()) {
                    long? e = BigToLongOrNull(b.r!.num);
                    if (e != null) {
                        return Val.OfRat(a.r!.PowInt(e.Value));
                    }
                    // huge exponent: fall through to double and overflow-check
                } else {
                    long? p = BigToLongOrNull(b.r!.num);
                    long? q = BigToLongOrNull(b.r!.den);
                    if (p != null && q != null && q > 0 && q <= 64) {
                        Rat? root = a.r!.ExactRoot((int)q.Value);
                        if (root != null) {
                            return Val.OfRat(root.PowInt(p.Value));
                        }
                    }
                }
            }
            double ad = a.ToDoubleValue();
            double bd = b.ToDoubleValue();
            double res = Math.Pow(ad, bd);
            if (IsNaN(res)) {
                if (ad < 0.0) throw ErrorType.OutOfRange();
                throw ErrorType.NotANumber();
            }
            if (!IsFinite(res)) {
                throw ErrorType.ValueTooLarge();
            }
            return Val.OfDouble(res);
        }

        public static Val Factorial(Val v) {
            RequireReal(v);
            long? n;
            if (v.exact) {
                if (!v.r!.IsInteger() || v.r!.num < BigInteger.Zero) {
                    throw ErrorType.BadFactorial();
                }
                n = BigToLongOrNull(v.r!.num);
                if (n == null || n > 10000L) {
                    throw ErrorType.ValueTooLarge();
                }
            } else {
                double d = v.approx;
                if (!IsFinite(d) || d < 0.0 || d != Math.Round(d)) {
                    throw ErrorType.BadFactorial();
                }
                n = long.Parse(Math.Round(d).ToString());
                if (n > 10000L) {
                    throw ErrorType.ValueTooLarge();
                }
            }
            BigInteger f = BigInteger.One;
            for (long k = 2L; k <= n; k = k + 1) {
                f = f * new BigInteger(k);
            }
            return OfBigInt(f);
        }

        public static Val FloorMod(Val a, Val b) {
            RequireReal(a);
            RequireReal(b);
            if (a.exact && b.exact) {
                Val q = Val.OfRat(Rat.FloorRat(a.r!.Div(b.r!)));
                return Val.OfRat(a.r!.Sub(q.r!.Mul(b.r!)));
            }
            double ad = a.ToDoubleValue();
            double bd = b.ToDoubleValue();
            if (bd == 0.0) {
                throw ErrorType.ZeroDivision();
            }
            double m = ad - bd * Math.Floor(ad / bd);
            return Val.OfDouble(m);
        }

        public static int CompareVals(Val a, Val b) {
            RequireReal(a);
            RequireReal(b);
            if (a.exact && b.exact) {
                BigInteger x = a.r!.num * b.r!.den;
                BigInteger y = b.r!.num * a.r!.den;
                return (x < y) ? -1 : (x > y ? 1 : 0);
            }
            double ad = a.ToDoubleValue();
            double bd = b.ToDoubleValue();
            if (ad < bd) return -1;
            if (ad > bd) return 1;
            return 0;
        }

        // Complex guard: any operation that is only defined for real values.
        public static Val RequireReal(Val v) {
            if (v.mat != null) {
                throw ErrorType.Create("InvalidInput", "This operation does not support matrices.");
            }
            if (v.complex) {
                throw ErrorType.Create("InvalidInput", "This operation does not support complex numbers.");
            }
            return v;
        }

        public static double ReOf(Val v) {
            return v.complex ? v.re : v.ToDoubleValue();
        }

        public static double ImOf(Val v) {
            return v.complex ? v.im : 0.0;
        }

        public static Val AddValues(Val a, Val b) {
            if (a.text != null || b.text != null) throw ErrorType.NotAnInteger();
            if (a.mat != null || b.mat != null) {
                if (a.mat == null || b.mat == null) {
                    throw ErrorType.Create("BadMatrixDims", "Matrix and scalar cannot be added.");
                }
                return Val.OfMatrix(MatrixAdd(a.mat!, b.mat!, false));
            }
            if (a.complex || b.complex) {
                return Val.OfComplex(ReOf(a) + ReOf(b), ImOf(a) + ImOf(b));
            }
            if (a.exact && b.exact) return Val.OfRat(a.r!.Add(b.r!));
            return Val.OfDouble(a.ToDoubleValue() + b.ToDoubleValue());
        }

        public static Val SubValues(Val a, Val b) {
            if (a.text != null || b.text != null) throw ErrorType.NotAnInteger();
            if (a.mat != null || b.mat != null) {
                if (a.mat == null || b.mat == null) {
                    throw ErrorType.Create("BadMatrixDims", "Matrix and scalar cannot be subtracted.");
                }
                return Val.OfMatrix(MatrixAdd(a.mat!, b.mat!, true));
            }
            if (a.complex || b.complex) {
                return Val.OfComplex(ReOf(a) - ReOf(b), ImOf(a) - ImOf(b));
            }
            if (a.exact && b.exact) return Val.OfRat(a.r!.Sub(b.r!));
            return Val.OfDouble(a.ToDoubleValue() - b.ToDoubleValue());
        }

        public static Val MulValues(Val a, Val b) {
            if (a.text != null || b.text != null) throw ErrorType.NotAnInteger();
            if (a.mat != null && b.mat != null) return Val.OfMatrix(MatrixMul(a.mat!, b.mat!));
            if (a.mat != null) return Val.OfMatrix(MatrixScl(b, a.mat!));
            if (b.mat != null) return Val.OfMatrix(MatrixScl(a, b.mat!));
            if (a.complex || b.complex) {
                double ar = ReOf(a), ai = ImOf(a), br = ReOf(b), bi = ImOf(b);
                double nr = ar * br - ai * bi;
                double ni = ar * bi + ai * br;
                if (IsNaN(nr) || IsNaN(ni)) throw ErrorType.NotANumber();
                if (Math.Abs(nr) > 1.7976931348623157e308 || Math.Abs(ni) > 1.7976931348623157e308) throw ErrorType.ValueTooLarge();
                return Val.OfComplex(nr, ni);
            }
            if (a.exact && b.exact) return Val.OfRat(a.r!.Mul(b.r!));
            return Val.OfDouble(a.ToDoubleValue() * b.ToDoubleValue());
        }

        public static Val DivValues(Val a, Val b) {
            if (a.text != null || b.text != null) throw ErrorType.NotAnInteger();
            if (a.mat != null && b.mat != null) {
                throw ErrorType.Create("BadMatrixDims", "Matrix division is not defined.");
            }
            if (a.mat != null) return Val.OfMatrix(MatrixDiv(a.mat!, b));
            if (b.mat != null) {
                throw ErrorType.Create("BadMatrixDims", "Cannot divide a scalar by a matrix.");
            }
            if (a.complex || b.complex) {
                double ar = ReOf(a), ai = ImOf(a), br = ReOf(b), bi = ImOf(b);
                double d = br * br + bi * bi;
                if (d == 0.0) throw ErrorType.ZeroDivision();
                double nr = (ar * br + ai * bi) / d;
                double ni = (ai * br - ar * bi) / d;
                if (IsNaN(nr) || IsNaN(ni)) throw ErrorType.NotANumber();
                return Val.OfComplex(nr, ni);
            }
            if (a.exact && b.exact) return Val.OfRat(a.r!.Div(b.r!));
            double bd = b.ToDoubleValue();
            if (bd == 0.0) throw ErrorType.ZeroDivision();
            return Val.OfDouble(a.ToDoubleValue() / bd);
        }

        public static Val Invert(Val v) {
            if (v.complex) {
                double dmag = v.re * v.re + v.im * v.im;
                if (dmag == 0.0) throw ErrorType.ZeroDivision();
                return Val.OfComplex(v.re / dmag, -v.im / dmag);
            }
            if (v.exact) {
                if (v.r!.IsZero()) throw ErrorType.ZeroDivision();
                return Val.OfRat(v.r!.Inv());
            }
            double d = v.approx;
            if (d == 0.0) throw ErrorType.ZeroDivision();
            return Val.OfDouble(1.0 / d);
        }

        public static ErrorType WrongArity(string fn, int expected, int got) {
            return ErrorType.Create("WrongArity", "'" + fn + "' expects " + expected + " argument(s), got " + got + ".");
        }

        public static Val Evaluate(ANode node, AngleMode mode, Val? xValue) {
            return Evaluate(node, mode, xValue, null);
        }

        public static Val Evaluate(ANode node, AngleMode mode, Val? xValue, Val? ans) {
            if (node.kind == "number") {
                return Val.OfRat(node.value!);
            }
            if (node.kind == "ident") {
                string name = node.text.ToLower();
                if (name == "x" || name == "θ") {
                    if (xValue == null) {
                        throw ErrorType.UnknownIdentifier(node.text);
                    }
                    return xValue;
                }
                if (name == "ans") {
                    if (ans == null) {
                        throw ErrorType.UnknownIdentifier(node.text);
                    }
                    return ans;
                }
                if (name == "pi" || name == "π") return Val.OfDouble(3.141592653589793);
                if (name == "tau" || name == "τ") return Val.OfDouble(6.2831853071795864);
                if (name == "e") return Val.OfDouble(2.718281828459045);
                if (name == "i") return Val.OfComplex(0.0, 1.0);
                if (vars.ContainsKey(name)) return vars[name]!;
                throw ErrorType.UnknownIdentifier(node.text);
            }
            if (node.kind == "prefix") {
                Val v = Evaluate(node.kids[0], mode, xValue, ans);
                if (node.text == "-") {
                    if (v.complex) return Val.OfComplex(-v.re, -v.im);
                    return v.exact ? Val.OfRat(v.r!.Neg()) : Val.OfDouble(-v.approx);
                }
                if (node.text == "+") {
                    return v;
                }
                // bitwise NOT: ~x = -x - 1 (two's complement identity)
                BigInteger b = ToBigInt(v);
                return OfBigInt(-(b + BigInteger.One));
            }
            if (node.kind == "postfix") {
                Val v = Evaluate(node.kids[0], mode, xValue, ans);
                if (node.text == "%") {
                    RequireReal(v);
                    // standalone percent: divide by 100
                    if (v.exact) return Val.OfRat(v.r!.Div(Rat.OfInt(100)));
                    return Val.OfDouble(v.approx / 100.0);
                }
                return Factorial(v);
            }
            if (node.kind == "infix") {
                string op = node.text;
                Val a = Evaluate(node.kids[0], mode, xValue, ans);
                ANode right = node.kids[1];
                bool rightIsPercent = right.kind == "postfix" && right.text == "%";
                // Windows percent semantics: 10+5% = 10.5, 200*15% = 30
                if (rightIsPercent && (op == "+" || op == "-")) {
                    Val raw = RequireReal(Evaluate(right.kids[0], mode, xValue, ans));
                    RequireReal(a);
                    Val delta;
                    if (a.exact && raw.exact) delta = Val.OfRat(a.r!.Mul(raw.r!).Div(Rat.OfInt(100)));
                    else delta = Val.OfDouble(a.ToDoubleValue() * raw.ToDoubleValue() / 100.0);
                    if (op == "+") return AddValues(a, delta);
                    return SubValues(a, delta);
                }
                if (rightIsPercent && (op == "*" || op == "/")) {
                    Val raw = RequireReal(Evaluate(right.kids[0], mode, xValue, ans));
                    RequireReal(a);
                    Val factor;
                    if (raw.exact) factor = Val.OfRat(raw.r!.Div(Rat.OfInt(100)));
                    else factor = Val.OfDouble(raw.approx / 100.0);
                    if (op == "*") return MulValues(a, factor);
                    return DivValues(a, factor);
                }
                Val b = Evaluate(right, mode, xValue, ans);
                if (op == "+") return AddValues(a, b);
                if (op == "-") return SubValues(a, b);
                if (op == "*") return MulValues(a, b);
                if (op == "/") return DivValues(a, b);
                if (op == "^") return Power(a, b);
                if (op == "mod") return FloorMod(a, b);
                BigInteger ba = ToBigInt(a);
                BigInteger bb = ToBigInt(b);
                if (op == "and") return OfBigInt(ba & bb);
                if (op == "or") return OfBigInt(ba | bb);
                if (op == "xor") return OfBigInt(ba ^ bb);
                if (op == "shl" || op == "shr") {
                    long? s = BigToLongOrNull(bb);
                    if (s == null || s < 0L || s > 4096L) {
                        throw ErrorType.ValueTooLarge();
                    }
                    if (op == "shl") return OfBigInt(ba << (int)s.Value);
                    return OfBigInt(ba >> (int)s.Value);
                }
                throw ErrorType.Create("UnknownOperator", "Unknown operator '" + op + "'.");
            }
            if (node.kind == "call") {
                string fn = node.text.ToLower();
                if (fn == "arcsin") fn = "asin";
                if (fn == "arccos") fn = "acos";
                if (fn == "arctan") fn = "atan";
                if (fn == "arcsinh") fn = "asinh";
                if (fn == "arccosh") fn = "acosh";
                if (fn == "arctanh") fn = "atanh";
                int argc = node.kids.Length;

                if (fn == "sin" || fn == "cos" || fn == "tan" || fn == "cot") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    return TrigWrap(fn, Evaluate(node.kids[0], mode, xValue, ans), mode);
                }
                if (fn == "sec" || fn == "csc") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    Val inner = TrigWrap(fn == "sec" ? "cos" : "sin", v, mode);
                    return Invert(inner);
                }
                if (fn == "asin" || fn == "acos") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    double d = v.ToDoubleValue();
                    if (d < -1.0 || d > 1.0) throw ErrorType.OutOfRange();
                    Val? sp = SpecialInvTrig(fn, d, mode);
                    if (sp != null) return sp;
                    double res = fn == "asin" ? Math.Asin(d) : Math.Acos(d);
                    return RadToAngle(res, mode);
                }
                if (fn == "atan") {
                    if (argc < 1 || argc > 2) throw WrongArity(fn, 1, argc);
                    if (argc == 2) {
                        Val y = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                        Val xx = RequireReal(Evaluate(node.kids[1], mode, xValue, ans));
                        return RadToAngle(Math.Atan2(y.ToDoubleValue(), xx.ToDoubleValue()), mode);
                    }
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    return RadToAngle(Math.Atan(v.ToDoubleValue()), mode);
                }
                if (fn == "sinh" || fn == "cosh" || fn == "tanh" ||
                    fn == "asinh" || fn == "acosh" || fn == "atanh") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    double d = RequireReal(Evaluate(node.kids[0], mode, xValue, ans)).ToDoubleValue();
                    double res;
                    if (fn == "sinh") res = (Math.Exp(d) - Math.Exp(-d)) / 2.0;
                    else if (fn == "cosh") res = (Math.Exp(d) + Math.Exp(-d)) / 2.0;
                    else if (fn == "tanh") {
                        double q = Math.Exp(-2.0 * d);
                        res = (1.0 - q) / (1.0 + q);
                    }
                    else if (fn == "asinh") res = Math.Log(d + Math.Sqrt(d * d + 1.0));
                    else if (fn == "acosh") {
                        if (d < 1.0) throw ErrorType.OutOfRange();
                        res = Math.Log(d + Math.Sqrt(d * d - 1.0));
                    }
                    else {
                        if (d <= -1.0 || d >= 1.0) throw ErrorType.OutOfRange();
                        res = Math.Log((1.0 + d) / (1.0 - d)) / 2.0;
                    }
                    if (IsNaN(res)) throw ErrorType.OutOfRange();
                    return Val.OfDouble(res);
                }
                if (fn == "sqrt" || fn == "cbrt") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (v.complex) {
                        if (fn == "cbrt") throw ErrorType.Create("InvalidInput", "This operation does not support complex numbers.");
                        double r = Math.Sqrt(v.re * v.re + v.im * v.im);
                        double nr = Math.Sqrt((r + v.re) / 2.0);
                        double ni = (v.im < 0.0 ? -1.0 : 1.0) * Math.Sqrt((r - v.re) / 2.0);
                        return Val.OfComplex(nr, ni);
                    }
                    if (v.exact) {
                        Rat? root = v.r!.ExactRoot(fn == "sqrt" ? 2 : 3);
                        if (root != null) return Val.OfRat(root);
                        if (fn == "sqrt" && v.r!.Signum() < 0) {
                            return Val.OfComplex(0.0, Math.Sqrt(-v.r!.ToDoubleValue()));
                        }
                    }
                    double d = v.ToDoubleValue();
                    if (fn == "sqrt") {
                        if (d < 0.0) return Val.OfComplex(0.0, Math.Sqrt(-d));
                        return Val.OfDouble(Math.Sqrt(d));
                    }
                    return Val.OfDouble(Math.Cbrt(d));
                }
                if (fn == "abs") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (v.complex) return Val.OfDouble(Math.Sqrt(v.re * v.re + v.im * v.im));
                    if (v.exact) return Val.OfRat(v.r!.Signum() < 0 ? v.r!.Neg() : v.r!);
                    return Val.OfDouble(Math.Abs(v.approx));
                }
                if (fn == "conj" || fn == "real" || fn == "imag") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (fn == "conj") {
                        if (!v.complex) return v;
                        return Val.OfComplex(v.re, -v.im);
                    }
                    if (fn == "real") {
                        if (!v.complex) return v;
                        return Val.OfDouble(v.re);
                    }
                    if (!v.complex) return Val.OfInt(0);
                    return Val.OfDouble(v.im);
                }
                if (fn == "arg") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (!v.complex) {
                        double d = v.ToDoubleValue();
                        if (d < 0.0) throw ErrorType.Create("InvalidInput", "arg() needs a complex number (a + b*i).");
                        return Val.OfInt(0);
                    }
                    return RadToAngle(Math.Atan2(v.im, v.re), mode);
                }
                if (fn == "cmplx") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    Val ca = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    Val cb = RequireReal(Evaluate(node.kids[1], mode, xValue, ans));
                    return Val.OfComplex(ca.ToDoubleValue(), cb.ToDoubleValue());
                }
                if (fn == "poly2" || fn == "poly3" || fn == "poly4") {
                    int wantp = fn == "poly2" ? 3 : fn == "poly3" ? 4 : 5;
                    if (argc != wantp) throw WrongArity(node.text, wantp, argc);
                    Val[] coVals = new Val[argc];
                    for (int ci = 0; ci < argc; ci = ci + 1)
                        coVals[ci] = RequireReal(Evaluate(node.kids[ci], mode, xValue, ans));
                    if (coVals[0].ToDoubleValue() == 0.0)
                        throw ErrorType.Create("OutOfRange", "The leading coefficient cannot be 0.");
                    if (fn == "poly2") return Poly2(coVals[0], coVals[1], coVals[2]);
                    double[] pd = new double[argc];
                    for (int ci = 0; ci < argc; ci = ci + 1) pd[ci] = coVals[ci].ToDoubleValue();
                    return Val.OfRoots(DurandKerner(pd));
                }
                if (fn == "solve") {
                    if (argc != 3) throw WrongArity(node.text, 3, argc);
                    if (node.kids[1].kind != "ident")
                        throw ErrorType.Create("SyntaxError", "solve() needs a variable name (x) as its 2nd argument.");
                    Val start = Evaluate(node.kids[2], mode, xValue, ans);
                    return NewtonSolve(node.kids[0], mode, ans, start);
                }
                if (fn == "integrate") {
                    // integrate(f, a, b) or Windows-style integrate(f, x, a, b).
                    if (argc < 3 || argc > 4) throw WrongArity(fn, 3, argc);
                    int ai = argc == 4 ? 2 : 1;
                    int bi = argc == 4 ? 3 : 2;
                    if (argc == 4 && node.kids[1].kind != "ident")
                        throw ErrorType.Create("SyntaxError", "integrate() needs a variable name (x) as its 2nd argument.");
                    Val va = RequireReal(Evaluate(node.kids[ai], mode, xValue, ans));
                    Val vb = RequireReal(Evaluate(node.kids[bi], mode, xValue, ans));
                    double lo = va.ToDoubleValue();
                    double hi = vb.ToDoubleValue();
                    if (double.IsNaN(lo) || double.IsNaN(hi) || double.IsInfinity(lo) || double.IsInfinity(hi))
                        throw ErrorType.OutOfRange();
                    if (lo == hi) return Val.OfInt(0);
                    int n = 1000; // composite Simpson, even interval count
                    double h = (hi - lo) / n;
                    double total = 0.0;
                    double comp = 0.0; // Kahan compensation keeps summing noise at eps level
                    for (int i = 0; i <= n; i++) {
                        double xi = lo + i * h;
                        Val fv;
                        try {
                            fv = Evaluate(node.kids[0], mode, Val.OfDouble(xi), ans);
                        } catch (ErrorType) {
                            throw ErrorType.Create("InvalidInput", "The function is not defined over the integration interval.");
                        }
                        double fy = RequireReal(fv).ToDoubleValue();
                        if (double.IsNaN(fy))
                            throw ErrorType.Create("InvalidInput", "The function is not defined over the integration interval.");
                        double w = (i == 0 || i == n) ? 1.0 : (i % 2 == 1 ? 4.0 : 2.0);
                        double y = w * fy - comp;
                        double t = total + y;
                        comp = t - total - y;
                        total = t;
                    }
                    double res = total * h / 3.0;
                    if (double.IsNaN(res) || double.IsInfinity(res)) throw ErrorType.ValueTooLarge();
                    return Val.OfDouble(res);
                }
                if (fn == "deriv") {
                    // deriv(f, a) or Windows-style deriv(f, x, a), central difference.
                    if (argc < 2 || argc > 3) throw WrongArity(fn, 2, argc);
                    int dai = argc == 3 ? 2 : 1;
                    if (argc == 3 && node.kids[1].kind != "ident")
                        throw ErrorType.Create("SyntaxError", "deriv() needs a variable name (x) as its 2nd argument.");
                    Val va = RequireReal(Evaluate(node.kids[dai], mode, xValue, ans));
                    double a = va.ToDoubleValue();
                    if (double.IsNaN(a) || double.IsInfinity(a)) throw ErrorType.OutOfRange();
                    // Windows Calculator uses complex-step differentiation: f(a + i*h)/h,
                    // which has no subtractive cancellation. Fall back to Richardson
                    // (two-level central differences) when the function rejects complex x.
                    double slope = double.NaN;
                    double hs = 1e-20;
                    try {
                        Val cv = Evaluate(node.kids[0], mode, Val.OfComplex(a, hs), ans);
                        if (cv.complex && !double.IsNaN(cv.im)) slope = cv.im / hs;
                    } catch (ErrorType) {
                        // Function does not accept complex input; Richardson fallback below.
                    }
                    if (double.IsNaN(slope)) {
                        double h1 = Math.Max(Math.Abs(a), 1.0) * 1e-4;
                        double d1;
                        double d2;
                        try {
                            double up1 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a + h1), ans)).ToDoubleValue();
                            double dn1 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a - h1), ans)).ToDoubleValue();
                            double up2 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a + h1 / 2.0), ans)).ToDoubleValue();
                            double dn2 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a - h1 / 2.0), ans)).ToDoubleValue();
                            d1 = (up1 - dn1) / (2.0 * h1);
                            d2 = (up2 - dn2) / h1;
                        } catch (ErrorType) {
                            throw ErrorType.Create("InvalidInput", "The derivative does not exist at this point.");
                        }
                        slope = (4.0 * d2 - d1) / 3.0;
                    }
                    if (double.IsNaN(slope) || double.IsInfinity(slope))
                        throw ErrorType.Create("InvalidInput", "The derivative does not exist at this point.");
                    return Val.OfDouble(slope);
                }
                if (fn == "sum" || fn == "prod") {
                    // sum(f, lo, hi) / sum(f, x, lo, hi); x bound in f. Exact
                    // rational accumulation is preserved by AddValues/MulValues.
                    if (argc < 3 || argc > 4) throw WrongArity(fn, 3, argc);
                    int loIdx = 1, hiIdx = 2;
                    if (argc == 4) {
                        string vname = node.kids[1].text;
                        if (node.kids[1].kind != "ident" || (vname != "x" && vname != "X")) {
                            throw ErrorType.Create("SyntaxError", "sum()/prod() need 'x' as their 2nd argument.");
                        }
                        loIdx = 2;
                        hiIdx = 3;
                    }
                    Val vlo = Evaluate(node.kids[loIdx], mode, xValue, ans);
                    Val vhi = Evaluate(node.kids[hiIdx], mode, xValue, ans);
                    if (vlo.mat != null || vhi.mat != null) throw ErrorType.NotAnInteger();
                    if (!vlo.exact || vlo.r == null || !vlo.r!.IsInteger() || !vhi.exact || vhi.r == null || !vhi.r!.IsInteger()) {
                        throw ErrorType.NotAnInteger();
                    }
                    BigInteger lo = vlo.r!.num, hi = vhi.r!.num;
                    if (lo > hi) {
                        throw ErrorType.Create("OutOfRange", "sum()/prod() lower bound must not exceed the upper bound.");
                    }
                    if (hi - lo > 10000) {
                        throw ErrorType.Create("OutOfRange", "sum()/prod() ranges are limited to 10000 terms.");
                    }
                    Val acc = fn == "sum" ? Val.OfInt(0) : Val.OfInt(1);
                    for (BigInteger i = lo; i <= hi; i = i + BigInteger.One) {
                        Val xi = OfBigInt(i);
                        Val term = Evaluate(node.kids[0], mode, xi, ans);
                        acc = fn == "sum" ? AddValues(acc, term) : MulValues(acc, term);
                    }
                    return acc;
                }
                if (fn == "deriv2") {
                    // deriv2(f, a) or deriv2(f, x, a): second central
                    // difference with Richardson extrapolation.
                    if (argc < 2 || argc > 3) throw WrongArity(fn, 2, argc);
                    int dai = argc == 3 ? 2 : 1;
                    if (argc == 3 && node.kids[1].kind != "ident") {
                        throw ErrorType.Create("SyntaxError", "deriv2() needs a variable name (x) as its 2nd argument.");
                    }
                    Val va = RequireReal(Evaluate(node.kids[dai], mode, xValue, ans));
                    double a = va.ToDoubleValue();
                    if (IsNaN(a) || double.IsInfinity(a)) throw ErrorType.OutOfRange();
                    double h1 = Math.Max(Math.Abs(a), 1.0) * 1e-3;
                    double d1;
                    double d2;
                    try {
                        double f0 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a), ans)).ToDoubleValue();
                        double up1 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a + h1), ans)).ToDoubleValue();
                        double dn1 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a - h1), ans)).ToDoubleValue();
                        double up2 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a + h1 / 2.0), ans)).ToDoubleValue();
                        double dn2 = RequireReal(Evaluate(node.kids[0], mode, Val.OfDouble(a - h1 / 2.0), ans)).ToDoubleValue();
                        d1 = (up1 - 2.0 * f0 + dn1) / (h1 * h1);
                        d2 = (up2 - 2.0 * f0 + dn2) / ((h1 / 2.0) * (h1 / 2.0));
                    } catch (ErrorType) {
                        throw ErrorType.Create("InvalidInput", "The second derivative does not exist at this point.");
                    }
                    double slope2 = (4.0 * d2 - d1) / 3.0;
                    if (IsNaN(slope2) || double.IsInfinity(slope2)) {
                        throw ErrorType.Create("InvalidInput", "The second derivative does not exist at this point.");
                    }
                    return Val.OfDouble(slope2);
                }
                if (fn == "speedoflight") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfInt(299792458);
                }
                if (fn == "planck") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(662607015), BigInteger.Pow(new BigInteger(10), 42)));
                }
                if (fn == "charge") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(1602176634), BigInteger.Pow(new BigInteger(10), 28)));
                }
                if (fn == "boltzmann") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(1380649), BigInteger.Pow(new BigInteger(10), 29)));
                }
                if (fn == "avogadro") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return OfBigInt(new BigInteger(602214076) * BigInteger.Pow(new BigInteger(10), 15));
                }
                if (fn == "gasconstant") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(831446261815324), BigInteger.Pow(new BigInteger(10), 14)));
                }
                if (fn == "standardg") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(980665), BigInteger.Pow(new BigInteger(10), 5)));
                }
                if (fn == "atmosphere") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfInt(101325);
                }
                if (fn == "gravitation") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(667430), BigInteger.Pow(new BigInteger(10), 16)));
                }
                if (fn == "electronmass") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(91093837015), BigInteger.Pow(new BigInteger(10), 41)));
                }
                if (fn == "protonmass") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(167262192369), BigInteger.Pow(new BigInteger(10), 38)));
                }
                if (fn == "neutronmass") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfRat(Rat.Make(new BigInteger(167492749804), BigInteger.Pow(new BigInteger(10), 38)));
                }
                if (fn == "hex" || fn == "oct" || fn == "bin") {
                    if (argc != 1) throw WrongArity(node.text, 1, argc);
                    BigInteger n = IntArgOf(Evaluate(node.kids[0], mode, xValue, ans));
                    int radix = fn == "hex" ? 16 : fn == "oct" ? 8 : 2;
                    return Val.OfText(ToRadix(n, radix));
                }
                if (fn == "base") {
                    if (argc != 2) throw WrongArity(node.text, 2, argc);
                    BigInteger n = IntArgOf(Evaluate(node.kids[0], mode, xValue, ans));
                    BigInteger rad = IntArgOf(Evaluate(node.kids[1], mode, xValue, ans));
                    if (rad < 2 || rad > 36)
                        throw ErrorType.Create("OutOfRange", "base() needs a radix from 2 to 36.");
                    return Val.OfText(ToRadix(n, (int)rad));
                }
                if (fn == "fact") {
                    if (argc != 1) throw WrongArity(node.text, 1, argc);
                    BigInteger n = IntArgOf(Evaluate(node.kids[0], mode, xValue, ans));
                    if (n < BigInteger.Zero)
                        throw ErrorType.Create("OutOfRange", "fact() needs a non-negative integer.");
                    if (n > 10000)
                        throw ErrorType.Create("OutOfRange", "fact() is limited to 10000.");
                    BigInteger acc = BigInteger.One;
                    for (BigInteger k = new BigInteger(2); k <= n; k++) acc = acc * k;
                    return OfBigInt(acc);
                }
                if (fn == "isprime") {
                    if (argc != 1) throw WrongArity(node.text, 1, argc);
                    BigInteger n = IntArgOf(Evaluate(node.kids[0], mode, xValue, ans));
                    if (n > 1000000000000000L)
                        throw ErrorType.Create("OutOfRange", "isprime() supports integers up to 1000000000000000.");
                    return Val.OfInt(IsPrimeLong((long)n) ? 1 : 0);
                }
                if (fn == "factors") {
                    if (argc != 1) throw WrongArity(node.text, 1, argc);
                    BigInteger n = IntArgOf(Evaluate(node.kids[0], mode, xValue, ans));
                    if (n < 2 || n > 1000000000000000L)
                        throw ErrorType.Create("OutOfRange", "factors() needs an integer from 2 to 1000000000000000.");
                    List<long> fs = FactorList((long)n);
                    Val[] list = new Val[fs.Count];
                    for (int i = 0; i < fs.Count; i = i + 1) list[i] = Val.OfInt(fs[i]);
                    return Val.OfRoots(list);
                }
                if (fn == "divisors") {
                    if (argc != 1) throw WrongArity(node.text, 1, argc);
                    BigInteger n = IntArgOf(Evaluate(node.kids[0], mode, xValue, ans));
                    if (n < 1 || n > 1000000000000000L)
                        throw ErrorType.Create("OutOfRange", "divisors() needs an integer from 1 to 1000000000000000.");
                    List<long> ds = DivisorList((long)n);
                    Val[] list = new Val[ds.Count];
                    for (int i = 0; i < ds.Count; i = i + 1) list[i] = Val.OfInt(ds[i]);
                    return Val.OfRoots(list);
                }
                if (fn == "phi") {
                    if (argc != 1) throw WrongArity(node.text, 1, argc);
                    BigInteger n = IntArgOf(Evaluate(node.kids[0], mode, xValue, ans));
                    if (n < 1 || n > 1000000000000000L)
                        throw ErrorType.Create("OutOfRange", "phi() needs an integer from 1 to 1000000000000000.");
                    long v = (long)n;
                    long result = v;
                    List<long> fs = FactorList(v);
                    for (int i = 0; i < fs.Count; i = i + 1) {
                        long p = fs[i];
                        bool seen = false;
                        for (int j = 0; j < i; j = j + 1) {
                            if (fs[j] == p) seen = true;
                        }
                        if (!seen) result = result / p * (p - 1);
                    }
                    return Val.OfInt(result);
                }
                if (fn == "combin" || fn == "ncr" || fn == "perm" || fn == "npr") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    BigInteger bn = ToBigInt(Evaluate(node.kids[0], mode, xValue, ans));
                    BigInteger br = ToBigInt(Evaluate(node.kids[1], mode, xValue, ans));
                    if (bn < BigInteger.Zero || br < BigInteger.Zero)
                        throw ErrorType.Create("OutOfRange", "nCr/nPr need non-negative integers (n, r).");
                    if (br > bn)
                        throw ErrorType.Create("OutOfRange", "nCr/nPr need r <= n.");
                    BigInteger acc = BigInteger.One;
                    if (fn == "perm" || fn == "npr") {
                        for (BigInteger k = BigInteger.Zero; k < br; k++) acc = acc * (bn - k);
                    } else {
                        for (BigInteger k = BigInteger.One; k <= br; k++) acc = acc * (bn - br + k) / k;
                    }
                    return OfBigInt(acc);
                }
                if (fn == "sign") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    if (v.exact) return Val.OfInt(v.r!.Signum());
                    double d = v.approx;
                    if (d > 0.0) return Val.OfInt(1);
                    if (d < 0.0) return Val.OfInt(-1);
                    return Val.OfInt(0);
                }
                if (fn == "floor" || fn == "ceil") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    if (v.exact) {
                        if (fn == "floor") return Val.OfRat(Rat.FloorRat(v.r!));
                        return Val.OfRat(Rat.FloorRat(v.r!.Neg()).Neg());
                    }
                    double d = v.approx;
                    return Val.OfDouble(fn == "floor" ? Math.Floor(d) : Math.Ceiling(d));
                }
                if (fn == "round") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    if (v.exact) {
                        // half away from zero, like the Windows Calculator
                        Rat half = Rat.Make(new BigInteger(1), new BigInteger(2));
                        Rat m = v.r!.Signum() < 0 ? v.r!.Neg() : v.r!;
                        Rat r_ = Rat.FloorRat(m.Add(half));
                        return Val.OfRat(v.r!.Signum() < 0 ? r_.Neg() : r_);
                    }
                    double d = v.approx;
                    double res = d >= 0.0 ? Math.Floor(d + 0.5) : Math.Ceiling(d - 0.5);
                    return Val.OfDouble(res);
                }
                if (fn == "exp") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    double res = Math.Exp(v.ToDoubleValue());
                    if (!IsFinite(res)) throw ErrorType.ValueTooLarge();
                    return Val.OfDouble(res);
                }
                if (fn == "ln") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    double d = v.ToDoubleValue();
                    if (d <= 0.0) throw ErrorType.OutOfRange();
                    return Val.OfDouble(Math.Log(d));
                }
                if (fn == "log" || fn == "log10") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    if (v.exact && !v.r!.IsZero()) {
                        long a = PowTenCount(v.r!.num);
                        long b2 = PowTenCount(v.r!.den);
                        if (a >= 0 && b2 >= 0) {
                            return Val.OfInt(a - b2);
                        }
                    }
                    double d = v.ToDoubleValue();
                    if (d <= 0.0) throw ErrorType.OutOfRange();
                    return Val.OfDouble(Math.Log10(d));
                }
                if (fn == "log2") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    double d = v.ToDoubleValue();
                    if (d <= 0.0) throw ErrorType.OutOfRange();
                    return Val.OfDouble(Math.Log2(d));
                }
                if (fn == "pow") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    return Power(Evaluate(node.kids[0], mode, xValue, ans), Evaluate(node.kids[1], mode, xValue, ans));
                }
                if (fn == "min" || fn == "max") {
                    if (argc < 1) throw WrongArity(fn, 1, argc);
                    Val best = Evaluate(node.kids[0], mode, xValue, ans);
                    for (int k = 1; k < argc; k = k + 1) {
                        Val cand = Evaluate(node.kids[k], mode, xValue, ans);
                        int cmp = CompareVals(cand, best);
                        if ((fn == "min" && cmp < 0) || (fn == "max" && cmp > 0)) best = cand;
                    }
                    return best;
                }
                if (fn == "hypot") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    Val a = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    Val b = RequireReal(Evaluate(node.kids[1], mode, xValue, ans));
                    if (a.exact && b.exact) {
                        Rat s = a.r!.Mul(a.r!).Add(b.r!.Mul(b.r!));
                        Rat? root = s.ExactRoot(2);
                        if (root != null) return Val.OfRat(root);
                    }
                    double ad = a.ToDoubleValue();
                    double bd = b.ToDoubleValue();
                    return Val.OfDouble(Math.Sqrt(ad * ad + bd * bd));
                }
                if (fn == "gcd" || fn == "lcm") {
                    if (argc < 2) throw WrongArity(fn, 2, argc);
                    BigInteger g = BigInteger.Zero;
                    for (int k = 0; k < argc; k = k + 1) {
                        Val v = Evaluate(node.kids[k], mode, xValue, ans);
                        BigInteger a = ToBigInt(v);
                        if (a < BigInteger.Zero) a = -a;
                        if (k == 0) g = a;
                        else g = Rat.Gcd(g, a);
                        if (g == BigInteger.Zero && k + 1 < argc) continue;
                    }
                    if (fn == "gcd") return OfBigInt(g);
                    BigInteger l = BigInteger.Zero;
                    for (int k = 0; k < argc; k = k + 1) {
                        Val v = Evaluate(node.kids[k], mode, xValue, ans);
                        BigInteger a = ToBigInt(v);
                        if (a < BigInteger.Zero) a = -a;
                        if (a == BigInteger.Zero) { l = BigInteger.Zero; break; }
                        if (l == BigInteger.Zero) { l = a; continue; }
                        BigInteger gg = Rat.Gcd(l, a);
                        l = (l / gg) * a;
                    }
                    return OfBigInt(l);
                }
                if (fn == "mod") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    return FloorMod(Evaluate(node.kids[0], mode, xValue, ans),
                                    Evaluate(node.kids[1], mode, xValue, ans));
                }
                if (fn == "idiv") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    BigInteger a = ToBigInt(Evaluate(node.kids[0], mode, xValue, ans));
                    BigInteger b = ToBigInt(Evaluate(node.kids[1], mode, xValue, ans));
                    if (b == BigInteger.Zero) throw ErrorType.Create("ZeroDivisionError", "Can't divide by 0.");
                    return OfBigInt(a / b);   // truncates toward zero
                }
                if (fn == "dms") {
                    if (argc != 3) throw WrongArity(fn, 3, argc);
                    Val vd = Evaluate(node.kids[0], mode, xValue, ans);
                    Val vm = Evaluate(node.kids[1], mode, xValue, ans);
                    Val vs = Evaluate(node.kids[2], mode, xValue, ans);
                    if (!vd.exact || vd.r == null || !vd.r!.IsInteger() || !vm.exact || vm.r == null || !vm.r!.IsInteger()) {
                        throw ErrorType.Create("NotAnInteger", "dms(): degrees and minutes must be integers.");
                    }
                    if (vm.r!.num < BigInteger.Zero) {
                        throw ErrorType.Create("OutOfRange", "Minutes and seconds must be non-negative.");
                    }
                    double sd = vs.ToDoubleValue();
                    if (double.IsNaN(sd) || sd < 0.0 || sd > 1.7976931348623157e308) {
                        throw ErrorType.Create("OutOfRange", "Minutes and seconds must be non-negative.");
                    }
                    BigInteger dabs = vd.r!.num < BigInteger.Zero ? -vd.r!.num : vd.r!.num;
                    Rat total = Rat.Make(dabs, BigInteger.One)
                        .Add(Rat.Make(vm.r!.num, new BigInteger(60)));
                    if (vs.exact && vs.r != null) {
                        total = total.Add(vs.r!.Div(Rat.Make(new BigInteger(3600), BigInteger.One)));
                        return Val.OfRat(vd.r!.num < BigInteger.Zero ? total.Neg() : total);
                    }
                    return Val.OfDouble(total.ToDoubleValue() + sd / 3600.0);
                }
                if (fn == "frac") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    Val va = Evaluate(node.kids[0], mode, xValue, ans);
                    Val vb = Evaluate(node.kids[1], mode, xValue, ans);
                    if (!va.exact || va.r == null || !vb.exact || vb.r == null
                        || !va.r!.IsInteger() || !vb.r!.IsInteger()) {
                        throw ErrorType.Create("NotAnInteger", "frac(): numerator and denominator must be integers.");
                    }
                    if (vb.r!.IsZero()) throw ErrorType.Create("ZeroDivision", "Can't divide by 0.");
                    return Val.OfRat(va.r!.Div(vb.r!));
                }
                if (fn == "square") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    if (v.exact && v.r != null) return Val.OfRat(v.r!.PowInt(2));
                    double ds = v.ToDoubleValue();
                    return Val.OfDouble(ds * ds);
                }
                if (fn == "cube") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    if (v.exact && v.r != null) return Val.OfRat(v.r!.PowInt(3));
                    double dc = v.ToDoubleValue();
                    return Val.OfDouble(dc * dc * dc);
                }
                if (fn == "recip") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (v.complex) return Invert(v);
                    if (v.exact && v.r != null) {
                        if (v.r!.IsZero()) throw ErrorType.Create("ZeroDivision", "Can't divide by 0.");
                        return Val.OfRat(v.r!.Inv());
                    }
                    double dr = v.ToDoubleValue();
                    if (dr == 0.0) throw ErrorType.Create("ZeroDivision", "Can't divide by 0.");
                    return Val.OfDouble(1.0 / dr);
                }
                if (fn == "tenpow") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    if (v.exact && v.r != null && v.r!.IsInteger()) {
                        BigInteger k = v.r!.num;
                        BigInteger ten = new BigInteger(10);
                        if (k.Sign == 0) return Val.OfInt(1);
                        if (k.Sign > 0 && k <= int.MaxValue)
                            return Val.OfRat(Rat.Make(Rat.PowBig(ten, (long)k), BigInteger.One));
                        if (k.Sign < 0 && -k <= int.MaxValue)
                            return Val.OfRat(Rat.Make(BigInteger.One, Rat.PowBig(ten, (long)(-k))));
                    }
                    double dt = v.ToDoubleValue();
                    double rt = Math.Pow(10.0, dt);
                    if (double.IsNaN(rt)) throw ErrorType.Create("Undefined", "10^x is undefined for this value.");
                    return Val.OfDouble(rt);
                }
                if (fn == "logb") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    double b = RequireReal(Evaluate(node.kids[0], mode, xValue, ans)).ToDoubleValue();
                    double x = RequireReal(Evaluate(node.kids[1], mode, xValue, ans)).ToDoubleValue();
                    if (double.IsNaN(b) || double.IsNaN(x) || b <= 0.0 || x <= 0.0 || b == 1.0) {
                        throw ErrorType.Create("OutOfRange", "logb(): base must be positive and not 1, argument must be positive.");
                    }
                    return Val.OfDouble(Math.Log(x, b));
                }
                if (fn == "rand") {
                    if (argc != 0) throw WrongArity(fn, 0, argc);
                    return Val.OfDouble(System.Random.Shared.NextDouble());
                }
                if (fn == "randint") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    Val va = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    Val vb = RequireReal(Evaluate(node.kids[1], mode, xValue, ans));
                    if (!va.exact || va.r == null || !vb.exact || vb.r == null
                        || !va.r!.IsInteger() || !vb.r!.IsInteger()) {
                        throw ErrorType.Create("NotAnInteger", "randint(): bounds must be integers.");
                    }
                    BigInteger lo = va.r!.num, hi = vb.r!.num;
                    if (lo > hi) throw ErrorType.Create("OutOfRange", "randint(): lower bound must be <= upper bound.");
                    long lim = 9223372036854775806;
                    if (lo < -lim || hi > lim) throw ErrorType.Create("ValueTooLarge", "randint(): bounds too large.");
                    long a = (long)lo, b2 = (long)hi;
                    long pick = System.Random.Shared.NextInt64(a, b2 + 1);
                    return Val.OfInt(pick);
                }
                if (fn == "pol") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    Val vr = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    Val vt = RequireReal(Evaluate(node.kids[1], mode, xValue, ans));
                    Val px = MulVal(vr, TrigWrap("cos", vt, mode));
                    Val py = MulVal(vr, TrigWrap("sin", vt, mode));
                    lastX = px;
                    lastY = py;
                    return px;
                }
                if (fn == "rec") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    Val vx = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    Val vy = RequireReal(Evaluate(node.kids[1], mode, xValue, ans));
                    Val rv;
                    if (vx.exact && vy.exact && vx.r != null && vy.r != null) {
                        Rat sum = vx.r!.Mul(vx.r!).Add(vy.r!.Mul(vy.r!));
                        Rat? root = sum.ExactRoot(2);
                        rv = root != null ? Val.OfRat(root) : Val.OfDouble(Math.Sqrt(sum.ToDoubleValue()));
                    } else {
                        double dx0 = vx.ToDoubleValue(), dy0 = vy.ToDoubleValue();
                        rv = Val.OfDouble(Math.Sqrt(dx0 * dx0 + dy0 * dy0));
                    }
                    double dx = vx.ToDoubleValue(), dy = vy.ToDoubleValue();
                    double th;
                    if (dx == 0.0 && dy == 0.0) {
                        th = 0.0;
                    } else {
                        th = Math.Atan2(dy, dx);
                        if (mode == AngleMode.Deg) th = th * 180.0 / piValue;
                        else if (mode == AngleMode.Grad) th = th * 200.0 / piValue;
                    }
                    lastX = rv;
                    lastY = Val.OfDouble(th);
                    return rv;
                }
                if (fn == "x") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    if (lastX == null) throw ErrorType.Create("NoStoredPair", "X(): no Pol/Rec result available. Evaluate pol() or rec() first.");
                    return lastX!;
                }
                if (fn == "y") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    if (lastY == null) throw ErrorType.Create("NoStoredPair", "Y(): no Pol/Rec result available. Evaluate pol() or rec() first.");
                    return lastY!;
                }
                if (fn == "m") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return memVal != null ? memVal : Val.OfInt(0);
                }
                if (fn == "madd") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    memVal = memVal != null ? AddVal(memVal, v) : v;
                    return memVal;
                }
                if (fn == "mc") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    memVal = null;
                    return Val.OfInt(0);
                }
                if (fn == "sto") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    ANode target = node.kids[0];
                    if (target.kind != "ident") {
                        throw ErrorType.Create("BadStore", "sto(): first argument must be a single letter (A-Z).");
                    }
                    string key = target.text.ToLower();
                    if (key.Length != 1 || key[0] < 'a' || key[0] > 'z') {
                        throw ErrorType.Create("BadStore", "sto(): first argument must be a single letter (A-Z).");
                    }
                    if (key == "x" || key == "e" || key == "ans" || key == "pi" || key == "tau") {
                        throw ErrorType.Create("BadStore", "sto(): 'sto' cannot store to reserved name '" + key + "'.");
                    }
                    Val vv = Evaluate(node.kids[1], mode, xValue, ans);
                    vars[key] = vv;
                    return vv;
                }
                if (fn == "rcl") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    ANode target = node.kids[0];
                    if (target.kind != "ident") {
                        throw ErrorType.Create("BadStore", "rcl(): argument must be a stored letter (A-Z).");
                    }
                    return Evaluate(target, mode, xValue, ans);
                }
                // ---- Matrices and vectors ----
                if (fn == "mat") {
                    if (argc < 3) throw WrongArity(fn, 3, argc);
                    Val rv = Evaluate(node.kids[0], mode, xValue, ans);
                    Val cv = Evaluate(node.kids[1], mode, xValue, ans);
                    if (rv.mat != null || cv.mat != null) throw ErrorType.NotAnInteger();
                    if (!rv.exact || rv.r == null || !cv.exact || cv.r == null) throw ErrorType.NotAnInteger();
                    if (!rv.r!.IsInteger() || !cv.r!.IsInteger()) throw ErrorType.NotAnInteger();
                    BigInteger rnum = rv.r!.num, cnum = cv.r!.num;
                    if (rnum < 1 || cnum < 1 || rnum > 6 || cnum > 6) {
                        throw ErrorType.Create("BadMatrixDims", "mat() needs rows and columns from 1 to 6.");
                    }
                    int rows = (int)rnum, cols = (int)cnum;
                    if (argc - 2 != rows * cols) {
                        throw ErrorType.Create("BadMatrixDims", "mat() needs " + (rows * cols).ToString() + " values for a " + rows.ToString() + "x" + cols.ToString() + " matrix.");
                    }
                    Val[][] m = new Val[rows][];
                    int k = 0;
                    for (int i = 0; i < rows; i = i + 1) {
                        m[i] = new Val[cols];
                        for (int j = 0; j < cols; j = j + 1) {
                            Val v = Evaluate(node.kids[2 + k], mode, xValue, ans);
                            if (v.mat != null) {
                                throw ErrorType.Create("BadMatrixDims", "A matrix cannot be an element of a matrix.");
                            }
                            m[i][j] = v;
                            k = k + 1;
                        }
                    }
                    Val res = Val.OfMatrix(m);
                    // Auto-store Casio-style: first free MatA..MatF slot wins.
                    for (int s = 0; s < 6; s = s + 1) {
                        if (matSlots[s] == null) { matSlots[s] = res; break; }
                    }
                    return res;
                }
                if (fn.Length == 4 && fn[0] == 'm' && fn[1] == 'a' && fn[2] == 't' && fn[3] >= 'a' && fn[3] <= 'f') {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    int idx = fn[3] - 'a';
                    if (matSlots[idx] == null) {
                        throw ErrorType.Create("NoData", MName(idx) + " is empty - store a matrix with mat() first.");
                    }
                    return matSlots[idx]!;
                }
                if (fn == "matclr") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    for (int s = 0; s < 6; s = s + 1) matSlots[s] = null;
                    return Val.OfInt(0);
                }
                if (fn == "det") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (v.mat == null) throw ErrorType.Create("InvalidInput", "det() needs a matrix.");
                    Val[][] m = v.mat!;
                    if (m.Length != m[0].Length) throw ErrorType.Create("BadMatrixDims", "det() needs a square matrix.");
                    return DetOf(m);
                }
                if (fn == "minv") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (v.mat == null) throw ErrorType.Create("InvalidInput", "minv() needs a matrix.");
                    Val[][] m = v.mat!;
                    if (m.Length != m[0].Length) throw ErrorType.Create("BadMatrixDims", "minv() needs a square matrix.");
                    return Val.OfMatrix(InvertOf(m));
                }
                if (fn == "mtr") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (v.mat == null) throw ErrorType.Create("InvalidInput", "mtr() needs a matrix.");
                    return Val.OfMatrix(TransposeOf(v.mat!));
                }
                if (fn == "melt") {
                    if (argc != 3) throw WrongArity(fn, 3, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    Val rr = Evaluate(node.kids[1], mode, xValue, ans);
                    Val rc = Evaluate(node.kids[2], mode, xValue, ans);
                    if (v.mat == null) throw ErrorType.Create("InvalidInput", "melt() needs a matrix.");
                    if (rr.mat != null || rc.mat != null) throw ErrorType.NotAnInteger();
                    if (!rr.exact || rr.r == null || !rc.exact || rc.r == null) throw ErrorType.NotAnInteger();
                    if (!rr.r!.IsInteger() || !rc.r!.IsInteger()) throw ErrorType.NotAnInteger();
                    BigInteger ri = rr.r!.num, ci = rc.r!.num;
                    if (ri < 1 || ci < 1 || ri > v.mat!.Length || ci > v.mat![0].Length) {
                        throw ErrorType.Create("OutOfRange", "Element position is out of range for this matrix.");
                    }
                    return v.mat![(int)ri - 1][(int)ci - 1];
                }
                if (fn == "dot") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    Val a = Evaluate(node.kids[0], mode, xValue, ans);
                    Val b = Evaluate(node.kids[1], mode, xValue, ans);
                    if (a.mat == null || b.mat == null) {
                        throw ErrorType.Create("InvalidInput", "dot() needs vectors (one-row or one-column matrices).");
                    }
                    Val[] fa = FlatOf(a), fb = FlatOf(b);
                    if (fa.Length != fb.Length) throw ErrorType.Create("BadMatrixDims", "dot() needs equal-length vectors.");
                    Val sum = MulValues(fa[0], fb[0]);
                    for (int i = 1; i < fa.Length; i = i + 1) sum = AddValues(sum, MulValues(fa[i], fb[i]));
                    return sum;
                }
                if (fn == "cross") {
                    if (argc != 2) throw WrongArity(fn, 2, argc);
                    Val a = Evaluate(node.kids[0], mode, xValue, ans);
                    Val b = Evaluate(node.kids[1], mode, xValue, ans);
                    if (a.mat == null || b.mat == null) {
                        throw ErrorType.Create("InvalidInput", "cross() needs vectors (one-row or one-column matrices).");
                    }
                    Val[] fa = FlatOf(a), fb = FlatOf(b);
                    if (fa.Length != 3 || fb.Length != 3) throw ErrorType.Create("BadMatrixDims", "cross() needs 3-element vectors.");
                    Val[][] r = new Val[1][];
                    r[0] = new Val[3];
                    r[0][0] = SubValues(MulValues(fa[1], fb[2]), MulValues(fa[2], fb[1]));
                    r[0][1] = SubValues(MulValues(fa[2], fb[0]), MulValues(fa[0], fb[2]));
                    r[0][2] = SubValues(MulValues(fa[0], fb[1]), MulValues(fa[1], fb[0]));
                    return Val.OfMatrix(r);
                }
                if (fn == "vmag") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = Evaluate(node.kids[0], mode, xValue, ans);
                    if (v.mat == null) throw ErrorType.Create("InvalidInput", "vmag() needs a vector (one-row or one-column matrix).");
                    Val[] fa = FlatOf(v);
                    bool ex = true;
                    for (int i = 0; i < fa.Length; i = i + 1) {
                        if (!fa[i].exact || fa[i].r == null) ex = false;
                    }
                    if (ex) {
                        Rat sumr = Rat.OfInt(0);
                        for (int i = 0; i < fa.Length; i = i + 1) sumr = sumr.Add(fa[i].r!.Mul(fa[i].r!));
                        Rat? root = sumr.ExactRoot(2);
                        return root != null ? Val.OfRat(root) : Val.OfDouble(Math.Sqrt(sumr.ToDoubleValue()));
                    }
                    double s = 0.0;
                    for (int i = 0; i < fa.Length; i = i + 1) {
                        double d = fa[i].ToDoubleValue();
                        s = s + d * d;
                    }
                    if (IsNaN(s)) throw ErrorType.NotANumber();
                    return Val.OfDouble(Math.Sqrt(s));
                }
                if (fn == "data") {
                    if (argc != 1) throw WrongArity(fn, 1, argc);
                    Val v = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    Stats.xs.Add(v);
                    return v;
                }
                if (fn == "clrd") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    Stats.Clear();
                    return Val.OfInt(0);
                }
                if (fn == "n") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfInt(Stats.xs.Count);
                }
                if (fn == "sumx" || fn == "sumx2" || fn == "mean" || fn == "sd" || fn == "sds" || fn == "var") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    int cnt = Stats.xs.Count;
                    if (cnt == 0) throw ErrorType.Create("NoData", "No data values. Add one with data() first.");
                    if (fn == "sumx") {
                        Rat? sx = Stats.SumX();
                        return sx != null ? Val.OfRat(sx) : Val.OfDouble(Stats.SumXDouble());
                    }
                    if (fn == "sumx2") {
                        Rat? sx2 = Stats.SumX2();
                        return sx2 != null ? Val.OfRat(sx2) : Val.OfDouble(Stats.SumX2Double());
                    }
                    if (fn == "mean") {
                        Rat? sx = Stats.SumX();
                        return sx != null ? Val.OfRat(sx.Div(Rat.OfInt(cnt))) : Val.OfDouble(Stats.SumXDouble() / cnt);
                    }
                    // Population variance: (Σx² / n) − (x̄)², exact when data is exact.
                    Rat? sx2e = Stats.SumX2();
                    Rat? mx = Stats.SumX();
                    bool popVarExact = sx2e != null && mx != null;
                    Rat? popVarR = null;
                    Val? popVarD = null;
                    if (popVarExact) {
                        Rat meanR = mx!.Div(Rat.OfInt(cnt));
                        popVarR = sx2e!.Div(Rat.OfInt(cnt)).Sub(meanR.Mul(meanR));
                    } else {
                        double md = Stats.SumXDouble() / cnt;
                        popVarD = Val.OfDouble(Stats.SumX2Double() / cnt - md * md);
                    }
                    if (fn == "var") {
                        return popVarExact ? Val.OfRat(popVarR!) : popVarD!;
                    }
                    if (fn == "sd") {
                        if (popVarExact) {
                            Rat? rt = popVarR!.ExactRoot(2);
                            return rt != null ? Val.OfRat(rt) : Val.OfDouble(Math.Sqrt(popVarR!.ToDoubleValue()));
                        }
                        return Val.OfDouble(Math.Sqrt(popVarD!.ToDoubleValue()));
                    }
                    // Sample standard deviation: needs n >= 2.
                    if (cnt < 2) throw ErrorType.Create("NoData", "Sample standard deviation needs at least 2 data points.");
                    if (popVarExact) {
                        Rat vs = popVarR!.Mul(Rat.OfInt(cnt)).Div(Rat.OfInt(cnt - 1));
                        Rat? rt2 = vs.ExactRoot(2);
                        return rt2 != null ? Val.OfRat(rt2) : Val.OfDouble(Math.Sqrt(vs.ToDoubleValue()));
                    }
                    double vd = popVarD!.ToDoubleValue() * cnt / (cnt - 1);
                    return Val.OfDouble(Math.Sqrt(vd));
                }
                if (fn == "minx" || fn == "maxx") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    if (Stats.xs.Count == 0) throw ErrorType.Create("NoData", "No data values. Add one with data() first.");
                    return fn == "minx" ? Stats.MinVal() : Stats.MaxVal();
                }
                // ---- REG mode: pairs, model setters, fits, predictions ----
                if (fn == "dat") {
                    if (argc != 2) throw WrongArity(node.text, 2, argc);
                    Val x = RequireReal(Evaluate(node.kids[0], mode, xValue, ans));
                    Val y = RequireReal(Evaluate(node.kids[1], mode, xValue, ans));
                    Stats.px.Add(x);
                    Stats.py.Add(y);
                    return y;
                }
                if (fn == "rcld") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    Stats.ClearPairs();
                    return Val.OfInt(0);
                }
                if (fn == "rn") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    return Val.OfInt(Stats.px.Count);
                }
                if (fn == "rsumx" || fn == "rsumy" || fn == "rsumx2" || fn == "rsumy2" || fn == "rsumxy") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    if (Stats.px.Count == 0) throw ErrorType.Create("NoData", "No data pairs. Add one with dat() first.");
                    double s = 0;
                    for (int i = 0; i < Stats.px.Count; i++) {
                        double x = Stats.px[i].ToDoubleValue(), y = Stats.py[i].ToDoubleValue();
                        if (fn == "rsumx") s += x;
                        else if (fn == "rsumy") s += y;
                        else if (fn == "rsumx2") s += x * x;
                        else if (fn == "rsumy2") s += y * y;
                        else s += x * y;
                    }
                    return Val.OfDouble(s);
                }
                if (fn == "rmeanx" || fn == "rmeany") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    if (Stats.px.Count == 0) throw ErrorType.Create("NoData", "No data pairs. Add one with dat() first.");
                    double s = 0;
                    for (int i = 0; i < Stats.px.Count; i++)
                        s += fn == "rmeanx" ? Stats.px[i].ToDoubleValue() : Stats.py[i].ToDoubleValue();
                    return Val.OfDouble(s / Stats.px.Count);
                }
                if (fn == "lin" || fn == "logreg" || fn == "expreg" || fn == "powreg" || fn == "invreg" || fn == "quadreg") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    Stats.regType = fn == "logreg" ? "log" : fn == "expreg" ? "exp" : fn == "powreg" ? "pow"
                        : fn == "invreg" ? "inv" : fn == "quadreg" ? "quad" : "lin";
                    return Val.OfInt(0);
                }
                if (fn == "a" || fn == "b" || fn == "c") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    if (Stats.regType == "quad") {
                        double[] abc = Stats.FitQuad();
                        return Val.OfDouble(fn == "a" ? abc[0] : fn == "b" ? abc[1] : abc[2]);
                    }
                    if (fn == "c") throw ErrorType.Create("BadReg", "C() is only available for quadratic regression (use QUAD first).");
                    double[] ab = Stats.FitAB();
                    return Val.OfDouble(fn == "a" ? ab[0] : ab[1]);
                }
                if (fn == "r") {
                    if (argc != 0) throw WrongArity(node.text, 0, argc);
                    if (Stats.px.Count == 0) throw ErrorType.Create("NoData", "No data pairs. Add one with dat() first.");
                    double rv = Stats.regType == "quad" ? Stats.CorrQuad() : Stats.CorrAB();
                    return Val.OfDouble(rv);
                }
                if (fn == "yhat" || fn == "xhat") {
                    if (argc != 1) throw WrongArity(node.text, 1, argc);
                    double v = Evaluate(node.kids[0], mode, xValue, ans).ToDoubleValue();
                    return Val.OfDouble(fn == "yhat" ? Stats.ModelY(v) : Stats.ModelX(v));
                }
                throw ErrorType.Create("UnknownFunction", "Unknown function '" + node.text + "'.");
            }
            throw ErrorType.Create("BadNode", "Internal: cannot evaluate node kind '" + node.kind + "'.");
        }

        // ---- Equation solving (iteration 12) ------------------------------
        // poly2 solves a*x^2 + b*x + c = 0 exactly when the discriminant is a
        // perfect square, doubles otherwise, a complex pair when D < 0.
        // poly3/poly4 run Durand-Kerner (all complex roots, iteratively).
        // solve(f, x, a) is Newton iteration from a with a complex-safe
        // central-difference derivative.

        static Rat ToRat(Val v) {
            if (v.complex) throw ErrorType.Create("NotAnInteger", "Solvers need real numbers.");
            if (v.exact) return v.r!;
            return Rat.FromAuto(v.approx.ToString("R"));
        }

        static Val CleanRoot(double re, double im) {
            double rr = Math.Round(re);
            if (Math.Abs(re - rr) < 1e-9) re = rr;
            double ri = Math.Round(im);
            if (Math.Abs(im - ri) < 1e-9) im = ri;
            if (im == 0.0) return Val.OfDouble(re);
            return Val.OfComplex(re, im);
        }

        static Val Poly2(Val a, Val b, Val c) {
            Rat ra = ToRat(a), rb = ToRat(b), rc = ToRat(c);
            if (ra.IsZero()) {
                if (rb.IsZero()) {
                    if (rc.IsZero()) throw ErrorType.Create("Info", "Every x solves the equation (infinite solutions).");
                    throw ErrorType.Create("NoSolution", "No solution (the equation is never true).");
                }
                return Val.OfRoots(new Val[] { Val.OfRat(rc.Div(rb).Neg()) });
            }
            Rat d = rb.PowInt(2).Sub(Rat.OfInt(4).Mul(ra).Mul(rc));
            Rat den = Rat.OfInt(2).Mul(ra);
            double denD = den.ToDoubleValue();
            if (d.Signum() == 0)
                return Val.OfRoots(new Val[] { Val.OfRat(rb.Neg().Div(den)) });
            if (d.Signum() < 0) {
                double re = rb.Neg().ToDoubleValue() / denD;
                double im = Math.Sqrt(-d.ToDoubleValue()) / Math.Abs(denD);
                return Val.OfRoots(new Val[] { Val.OfComplex(re, im), Val.OfComplex(re, -im) });
            }
            Rat? s = d.ExactRoot(2);
            if (s != null) {
                Val r1 = Val.OfRat(rb.Neg().Add(s).Div(den));
                Val r2 = Val.OfRat(rb.Neg().Sub(s).Div(den));
                return Val.OfRoots(new Val[] { r1, r2 });
            }
            double sd = Math.Sqrt(d.ToDoubleValue());
            double baseR = rb.Neg().ToDoubleValue() / denD;
            return Val.OfRoots(new Val[] { Val.OfDouble(baseR + sd / denD), Val.OfDouble(baseR - sd / denD) });
        }

        static Val[] DurandKerner(double[] co) {
            int n = co.Length - 1;
            double[] zr = new double[n];
            double[] zi = new double[n];
            double wr = 0.4, wi = 0.9;
            for (int k = 0; k < n; k = k + 1) {
                zr[k] = wr;
                zi[k] = wi;
                double nr = wr * 0.4 - wi * 0.9;
                double ni = wr * 0.9 + wi * 0.4;
                wr = nr;
                wi = ni;
            }
            for (int iter = 0; iter < 600; iter = iter + 1) {
                double maxDelta = 0.0;
                for (int k = 0; k < n; k = k + 1) {
                    double pr = co[0], pi = 0.0;
                    for (int j = 1; j <= n; j = j + 1) {
                        double nr = pr * zr[k] - pi * zi[k] + co[j];
                        double ni = pr * zi[k] + pi * zr[k];
                        pr = nr;
                        pi = ni;
                    }
                    double dr = 1.0, di = 0.0;
                    for (int j = 0; j < n; j = j + 1) {
                        if (j == k) continue;
                        double ar = zr[k] - zr[j], ai = zi[k] - zi[j];
                        double nr = dr * ar - di * ai;
                        double ni = dr * ai + di * ar;
                        dr = nr;
                        di = ni;
                    }
                    double dm = dr * dr + di * di;
                    if (dm == 0.0) continue;
                    double qr = (pr * dr + pi * di) / dm;
                    double qi = (pi * dr - pr * di) / dm;
                    double nzr = zr[k] - qr, nzi = zi[k] - qi;
                    double delta = Math.Abs(nzr - zr[k]) + Math.Abs(nzi - zi[k]);
                    if (delta > maxDelta) maxDelta = delta;
                    zr[k] = nzr;
                    zi[k] = nzi;
                }
                if (maxDelta < 1e-13) break;
            }
            Val[] roots = new Val[n];
            for (int k = 0; k < n; k = k + 1) roots[k] = CleanRoot(zr[k], zi[k]);
            return roots;
        }

        static Val NewtonSolve(ANode f, AngleMode mode, Val? ans, Val start) {
            RequireReal(start);
            double z = start.ToDoubleValue();
            int perturb = 0;
            try {
                for (int iter = 0; iter < 100; iter = iter + 1) {
                    double h = Math.Max(Math.Abs(z), 1.0) * 1e-6;
                    Val fz = RequireReal(Evaluate(f, mode, Val.OfDouble(z), ans));
                    double fr = fz.ToDoubleValue();
                    if (Double.IsNaN(fr) || Double.IsInfinity(fr)) {
                        perturb = perturb + 1;
                        if (perturb > 20) break;
                        z = z + 0.17;
                        continue;
                    }
                    double slope = 1.0;
                    bool slopeFailed = false;
                    try {
                        double fu = RequireReal(Evaluate(f, mode, Val.OfDouble(z + h), ans)).ToDoubleValue();
                        double fd = RequireReal(Evaluate(f, mode, Val.OfDouble(z - h), ans)).ToDoubleValue();
                        slope = (fu - fd) / (2 * h);
                    }
                    catch (ErrorType) {
                        slopeFailed = true;
                    }
                    if (Math.Abs(fr) < 1e-12) {
                        if (slopeFailed || Math.Abs(slope) > 1e-10) return CleanRoot(z, 0.0);
                        perturb = perturb + 1;
                        if (perturb > 20) break;
                        z = z + 0.17;
                        continue;
                    }
                    if (slopeFailed) {
                        perturb = perturb + 1;
                        if (perturb > 20) break;
                        z = z + 0.17;
                        continue;
                    }
                    if (Math.Abs(slope) < 1e-15) {
                        perturb = perturb + 1;
                        if (perturb > 20) break;
                        z = z + 0.17;
                        continue;
                    }
                    z = z - fr / slope;
                    if (Double.IsNaN(z) || Double.IsInfinity(z)) break;
                }
            }
            catch (ErrorType) {
                throw ErrorType.Create("NoSolution", "The function is not defined near that starting point.");
            }
            throw ErrorType.Create("NoSolution", "No solution near that starting point.");
        }
    }
}
