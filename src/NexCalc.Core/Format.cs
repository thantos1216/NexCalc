using System.Numerics;

namespace NexCalc.Core
{
    // User-facing number formatting. Exact results print their exact
    // decimal expansion when that terminates (1/4 -> "0.25"); everything
    // else prints 16 significant digits, with E-notation beyond |1E+/-21|,
    // matching how the Windows Calculator presents results.
    public enum DisplayFormat { Norm, Fix, Sci, Dms, Frac, Mixed }

    public class Format {
        // FIX/SCI presentation over the computed value. NORM delegates to the
        // exact-first Display(v); FIX rounds away from zero to n decimals,
        // SCI renders mantissa "d.ddd" plus signed exponent, as Windows Calc.
        public static string Display(Val v, DisplayFormat fmt, int digits) {
            if (v.text != null) return v.text;
            if (v.roots != null) return Display(v);
            if (v.mat != null) return MatrixString(v.mat!, fmt, digits);
            if (fmt == DisplayFormat.Norm) return Display(v);
            if (v.complex) {
                if (fmt == DisplayFormat.Fix) {
                    double rd = Math.Round(v.re, digits, MidpointRounding.AwayFromZero);
                    double id = Math.Round(v.im, digits, MidpointRounding.AwayFromZero);
                    return ComplexString(rd.ToString("F" + digits.ToString()), id.ToString("F" + digits.ToString()));
                }
                if (fmt == DisplayFormat.Sci) return SciComplex(v, digits);
                return Display(v);
            }
            if (fmt == DisplayFormat.Dms) return DmsString(v, digits);
            // Casio d/c style: Frac breaks improper fractions into a whole
            // number ("17/10" -> "1 7/10"); values below 1 stay as fractions.
            if (fmt == DisplayFormat.Frac) return FracString(v, true);
            if (fmt == DisplayFormat.Mixed) return FracString(v, true);
            if (v.exact && v.r!.IsInteger()) {
                BigInteger big = v.r!.num;
                // Only integers that overflow double need the exact path;
                // smaller ones keep the Fix/Sci double formatting.
                BigInteger b309 = Rat.PowBig(new BigInteger(10), 309L);
                if (big >= b309 || big <= -b309) return ExactE(v.r!);
            }
            double d = v.ToDoubleValue();
            if (Eval.IsNaN(d)) return "NaN";
            if (d > 1.7976931348623157e308) return "Infinity";
            if (d < -1.7976931348623157e308) return "-Infinity";
            if (fmt == DisplayFormat.Fix) {
                double rd = Math.Round(d, digits, MidpointRounding.AwayFromZero);
                return rd.ToString("F" + digits.ToString());
            }
            if (d == 0.0) {
                string z = digits > 0 ? "." + StringOfZeros(digits) : "";
                return "0" + z + "e+0";
            }
            bool neg = d < 0.0;
            double a = Math.Abs(d);
            int e = (int)Math.Floor(Math.Log10(a));
            while (Math.Pow(10.0, e) > a) e = e - 1;
            while (a / Math.Pow(10.0, e) >= 10.0) e = e + 1;
            long m = (long)Math.Round(a / Math.Pow(10.0, e - digits), 0, MidpointRounding.AwayFromZero);
            long cap = (long)Math.Pow(10.0, digits + 1);
            if (m >= cap) { m = m / 10L; e = e + 1; }
            string ds = m.ToString();
            string res = neg ? "-" : "";
            res = res + ds.Substring(0, 1);
            if (digits > 0) res = res + "." + ds.Substring(1);
            res = res + "e" + (e >= 0 ? "+" : "-") + (e < 0 ? -e : e).ToString();
            return res;
        }

        // Matrix presentation: outer brackets around rows of bracketed,
        // space-separated entries, e.g. [[1 2][3 4]]. Each entry is
        // formatted by the current display mode.
        private static string MatrixString(Val[][] m, DisplayFormat fmt, int digits) {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[");
            for (int i = 0; i < m.Length; i = i + 1) {
                sb.Append("[");
                for (int j = 0; j < m[i].Length; j = j + 1) {
                    if (j > 0) sb.Append(" ");
                    sb.Append(Display(m[i][j], fmt, digits));
                }
                sb.Append("]");
            }
            sb.Append("]");
            return sb.ToString();
        }

        // Root list: sorted by real part (then imaginary), joined with ", ".
        private static string RootsString(Val[] roots) {
            Val[] copy = new Val[roots.Length];
            roots.CopyTo(copy, 0);
            System.Array.Sort(copy, delegate (Val a, Val b) {
                double ar = a.complex ? a.re : a.ToDoubleValue();
                double br = b.complex ? b.re : b.ToDoubleValue();
                if (ar != br) return br.CompareTo(ar);
                double ai = a.complex ? a.im : 0.0;
                double bi = b.complex ? b.im : 0.0;
                return bi.CompareTo(ai);
            });
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < copy.Length; i = i + 1) {
                if (i > 0) sb.Append(", ");
                sb.Append(Display(copy[i]));
            }
            return sb.ToString();
        }

        public static string Display(Val v) {
            if (v.text != null) return v.text;
            if (v.roots != null) return RootsString(v.roots);
            if (v.mat != null) return MatrixString(v.mat!, DisplayFormat.Norm, 0);
            if (v.complex) return ComplexString(FormatDouble(v.re), FormatDouble(v.im));
            if (v.exact) {
                if (v.r!.IsInteger()) {
                    BigInteger big = v.r!.num;
                    BigInteger b21 = Rat.PowBig(new BigInteger(10), 21L);
                    if (big >= b21 || big <= -b21) {
                        return ExactE(v.r!);
                    }
                    return big.ToString();
                }
                if (v.r!.CleanDecimal()) {
                    double dv = v.r!.ToDoubleValue();
                    if (dv != 0.0) {
                        int ee = (int)Math.Floor(Math.Log10(Math.Abs(dv)));
                        if (ee >= 21 || ee <= -22) return ExactE(v.r!);
                    }
                    return ExactDecimal(v.r!);
                }
                return FormatDouble(v.r!.ToDoubleValue());
            }
            return FormatDouble(v.approx);
        }

        // Exact decimal expansion for rationals whose denominator is 2^a*5^b.
        public static string ExactDecimal(Rat r) {
            BigInteger d = r.den;
            long twos = 0;
            long fives = 0;
            BigInteger two = new BigInteger(2);
            while ((d % two) == BigInteger.Zero) { d = d / two; twos = twos + 1; }
            BigInteger fiveB = new BigInteger(5);
            while ((d % fiveB) == BigInteger.Zero) { d = d / fiveB; fives = fives + 1; }
            long s = twos > fives ? twos : fives;
            BigInteger scale = Rat.PowBig(new BigInteger(10), s);
            BigInteger scaled = r.num * (scale / r.den);
            bool neg = scaled < BigInteger.Zero;
            BigInteger abs = neg ? -scaled : scaled;
            string ds = abs.ToString();
            string frac;
            string intPart;
            if (ds.Length <= s) {
                ds = StringOfZeros(s - ds.Length) + ds;
                intPart = "0";
                frac = ds;
            } else {
                intPart = ds.Substring(0, ds.Length - (int)s);
                frac = ds.Substring(ds.Length - (int)s);
            }
            while (frac.Length > 0 && frac.Substring(frac.Length - 1) == "0") {
                frac = frac.Substring(0, frac.Length - 1);
            }
            string res = neg ? "-" : "";
            res = res + intPart;
            if (frac.Length > 0) res = res + "." + frac;
            return res;
        }

        // E-notation from exact digits (no double scaling noise) for clean
        // decimal rationals and huge integers outside the fixed display
        // window. Mantissa is capped at 16 significant digits with
        // away-from-zero rounding.
        private static string ExactE(Rat r) {
            BigInteger d = r.den;
            long twos = 0;
            long fives = 0;
            BigInteger two = new BigInteger(2);
            BigInteger fiveB = new BigInteger(5);
            while ((d % two) == BigInteger.Zero) { d = d / two; twos = twos + 1; }
            while ((d % fiveB) == BigInteger.Zero) { d = d / fiveB; fives = fives + 1; }
            long s = twos > fives ? twos : fives;
            BigInteger scaled = r.num * (Rat.PowBig(new BigInteger(10), s) / r.den);
            bool neg = scaled < BigInteger.Zero;
            BigInteger abs = neg ? -scaled : scaled;
            string ds = abs.ToString();
            int e = ds.Length - (int)s - 1;
            if (ds.Length > 16) {
                string q = ds.Substring(0, 16);
                if (ds.Substring(16, 1).ToCharArray()[0] >= '5') {
                    BigInteger qB = BigInteger.Parse(q) + BigInteger.One;
                    if (qB >= Rat.PowBig(new BigInteger(10), 16L)) {
                        ds = "1";
                        e = e + 1;
                    } else {
                        string qs = qB.ToString();
                        int keep = qs.Length;
                        while (keep > 1 && qs[keep - 1] == '0') keep = keep - 1;
                        ds = qs.Substring(0, keep);
                    }
                } else {
                    int keep = 16;
                    while (keep > 1 && ds[keep - 1] == '0') keep = keep - 1;
                    ds = ds.Substring(0, keep);
                }
            } else {
                int keep = ds.Length;
                while (keep > 1 && ds[keep - 1] == '0') keep = keep - 1;
                ds = ds.Substring(0, keep);
            }
            string res = neg ? "-" : "";
            res = res + ds.Substring(0, 1);
            if (ds.Length > 1) res = res + "." + ds.Substring(1);
            res = res + "E" + (e >= 0 ? "+" : "-") + (e < 0 ? -e : e).ToString();
            return res;
        }

        public static string StringOfZeros(long count) {
            string s = "";
            for (long k = 0; k < count; k = k + 1) s = s + "0";
            return s;
        }

        // Complex display: "a+bi", "a-bi", "bi", "a" as components already
        // formatted. Signed-component joining mirrors the Windows Calculator
        // complex presentation.
        public static string ComplexString(string reStr, string imStr) {
            bool imZero = imStr == "0" || imStr == "-0";
            if (imZero) return reStr;
            string imPart;
            if (imStr == "1") {
                imPart = "+i";
            } else if (imStr == "-1") {
                imPart = "-i";
            } else if (imStr.Substring(0, 1) == "-") {
                imPart = "-" + imStr.Substring(1) + "i";
            } else {
                imPart = "+" + imStr + "i";
            }
            if (reStr == "0" || reStr == "-0") {
                if (imPart.Substring(0, 1) == "+") imPart = imPart.Substring(1);
                return imPart;
            }
            return reStr + imPart;
        }

        // SCI presentation of a complex value: each component in scientific
        // notation, joined the same way as NORM.
        public static string SciComplex(Val v, int digits) {
            string rs = SciComponent(v.re, digits);
            string is2 = SciComponent(v.im, digits);
            return ComplexString(rs, is2);
        }

        public static string SciComponent(double d, int digits) {
            if (Eval.IsNaN(d)) return "NaN";
            if (d > 1.7976931348623157e308) return "Infinity";
            if (d < -1.7976931348623157e308) return "-Infinity";
            if (d == 0.0) {
                string z = digits > 0 ? "." + StringOfZeros(digits) : "";
                return "0" + z + "e+0";
            }
            bool neg = d < 0.0;
            double a = Math.Abs(d);
            int e = (int)Math.Floor(Math.Log10(a));
            while (Math.Pow(10.0, e) > a) e = e - 1;
            while (a / Math.Pow(10.0, e) >= 10.0) e = e + 1;
            long m = (long)Math.Round(a / Math.Pow(10.0, e - digits), 0, MidpointRounding.AwayFromZero);
            long cap = (long)Math.Pow(10.0, digits + 1);
            if (m >= cap) { m = m / 10L; e = e + 1; }
            string ds = m.ToString();
            string res = neg ? "-" : "";
            res = res + ds.Substring(0, 1);
            if (digits > 0) res = res + "." + ds.Substring(1);
            res = res + "e" + (e >= 0 ? "+" : "-") + (e < 0 ? -e : e).ToString();
            return res;
        }

        // 16 significant digits, E-notation outside the fixed window.
        public static string FormatDouble(double d) {
            if (Eval.IsNaN(d)) return "NaN";
            if (d > 1.7976931348623157e308) return "Infinity";
            if (d < -1.7976931348623157e308) return "-Infinity";
            if (d == 0.0) return "0";
            bool neg = d < 0.0;
            double a = Math.Abs(d);
            int e = (int)Math.Floor(Math.Log10(a));
            while (Math.Pow(10.0, e) > a) e = e - 1;
            while (a / Math.Pow(10.0, e) >= 10.0) e = e + 1;
            // 16-significant-digit rounding of the mantissa
            long m;
            double scaledM = a / Math.Pow(10.0, e - 15);
            m = (long)Math.Round(scaledM);
            if (m >= 10000000000000000L) { m = m / 10L; e = e + 1; }
            if (m <= 0L) return neg ? "-0" : "0";
            string ds = "" + m;
            while (ds.Length > 1 && ds.Substring(ds.Length - 1) == "0") {
                ds = ds.Substring(0, ds.Length - 1);
            }
            string res = neg ? "-" : "";
            if (e >= 21 || e <= -22) {
                res = res + ds.Substring(0, 1);
                if (ds.Length > 1) res = res + "." + ds.Substring(1);
                res = res + "E";
                res = res + (e >= 0 ? "+" : "-");
                res = res + "" + (e < 0 ? -e : e);
                return res;
            }
            if (e >= 0) {
                int intLen = e + 1;
                if (ds.Length <= intLen) {
                    res = res + ds + StringOfZeros(intLen - ds.Length);
                } else {
                    res = res + ds.Substring(0, intLen) + "." + ds.Substring(intLen);
                }
            } else {
                res = res + "0." + StringOfZeros(-e - 1) + ds;
            }
            return res;
        }

        // Base representation for the programmer-style display modes.
        // bits > 0 renders two's complement in that word width; otherwise a
        // plain sign + magnitude is used.
        // Degrees/minutes/seconds display: decimal degrees as D°M′S″.
        // digits controls the seconds fraction; rounding carries 60s upward.
        public static string DmsString(Val v, int digits) {
            double d = v.ToDoubleValue();
            if (Eval.IsNaN(d)) return "NaN";
            double a = Math.Abs(d);
            if (a > 9.2e18) return d < 0.0 ? "-Infinity" : "Infinity";
            bool neg = d < 0.0;
            long deg = (long)Math.Floor(a);
            double rem = (a - (double)deg) * 60.0;
            long min = (long)Math.Floor(rem);
            double sec = Math.Round((rem - (double)min) * 60.0, digits, MidpointRounding.AwayFromZero);
            if (sec >= 60.0) { sec = 0.0; min = min + 1; }
            if (min >= 60) { min = 0; deg = deg + 1; }
            string outStr = deg.ToString() + "°" + min.ToString() + "\u2032"
                + sec.ToString("F" + digits.ToString()) + "\u2033";
            return neg ? "-" + outStr : outStr;
        }

        // Fraction display: reduced improper fraction "3/2" or mixed "1 1/2".
        // Non-exact values fall back to the normal decimal display.
        public static string FracString(Val v, bool mixed) {
            if (!v.exact || v.r == null) return Display(v);
            Rat r = v.r!;
            bool neg = r.Signum() < 0;
            Rat a = neg ? r.Neg() : r;
            string outStr;
            if (a.IsInteger()) {
                outStr = a.num.ToString();
            } else if (mixed) {
                BigInteger whole = a.num / a.den;
                BigInteger rem = a.num - whole * a.den;
                if (rem.IsZero) outStr = whole.ToString();
                else outStr = (whole.IsZero ? "" : whole.ToString() + " ")
                    + rem.ToString() + "/" + a.den.ToString();
            } else {
                outStr = a.num.ToString() + "/" + a.den.ToString();
            }
            return neg ? "-" + outStr : outStr;
        }

        public static string ToBase(BigInteger v, int radix, int bits) {
            string digits = "0123456789ABCDEF";
            bool negative = v < BigInteger.Zero;
            BigInteger u = v;
            string prefix = "";
            if (bits > 0) {
                BigInteger width = Rat.PowBig(new BigInteger(2), bits);
                u = u % width;
                if (u < BigInteger.Zero) u = u + width;
            } else if (negative) {
                prefix = "-";
                u = -u;
            }
            if (u == BigInteger.Zero) return prefix + "0";
            string s = "";
            BigInteger b = new BigInteger(radix);
            while (u != BigInteger.Zero) {
                int dg = int.Parse((u % b).ToString());
                s = digits.Substring(dg, 1) + s;
                u = u / b;
            }
            return prefix + s;
        }

        // ---- MathPrint: render the parse tree as stacked textbook text ----

        public static string[] MathPrintLines(string expr) {
            ANode n;
            try { n = Parser.Parse(expr); }
            catch (ErrorType) { return new string[] { expr }; }
            Block b = MpRender(n);
            string[] outLines = new string[b.lines.Length];
            for (int i = 0; i < b.lines.Length; i++) outLines[i] = b.lines[i].PadRight(b.w);
            return outLines;
        }

        public static string MathPrint(string expr) {
            string[] ls = MathPrintLines(expr);
            string s = "";
            for (int i = 0; i < ls.Length; i++) {
                if (i > 0) s += "\n";
                s += ls[i];
            }
            return s;
        }

        private class Block {
            public string[] lines;
            public int w;
            public Block(string[] lines_, int w_) { lines = lines_; w = w_; }
        }

        private static Block Single(string s) {
            return new Block(new string[] { s }, s.Length);
        }

        private static int OpPrec(string op) {
            if (op == "^") return 3;
            if (op == "+" || op == "-") return 1;
            return 2; // *, /, and word operators parse at the term level
        }

        // Wrap a child block in parentheses when the child parses looser than
        // the parent operator (or binds equal on the right, which changes
        // meaning for left-associative operators).
        private static Block ChildRender(ANode c, int prec, bool isRight) {
            Block blk = MpRender(c);
            if (c.kind == "infix") {
                int cp = OpPrec(c.text);
                if (cp < prec || (isRight && cp == prec && c.text != "^")) {
                    blk = Single("(" + blk.lines[0] + ")");
                }
            }
            return blk;
        }

        // Parenthesize a multi-line block: '(' joins the top line, ')' joins
        // the bottom line.
        private static Block WrapBlock(Block b) {
            string[] lines = new string[b.lines.Length];
            for (int i = 0; i < b.lines.Length; i++) lines[i] = b.lines[i];
            lines[0] = "(" + lines[0];
            lines[lines.Length - 1] = lines[lines.Length - 1].TrimEnd() + ")";
            int w = 0;
            for (int i = 0; i < lines.Length; i++) if (lines[i].Length > w) w = lines[i].Length;
            return new Block(lines, w);
        }

        private static Block MpRender(ANode n) {
            if (n.kind == "number" || n.kind == "ident") return Single(n.text);
            if (n.kind == "prefix") {
                return JoinBottom(new Block[] { Single(n.text), MpRender(n.kids[0]) });
            }
            if (n.kind == "postfix") {
                return JoinBottom(new Block[] { MpRender(n.kids[0]), Single(n.text) });
            }
            if (n.kind == "infix") {
                string op = n.text;
                int prec = OpPrec(op);
                Block a;
                Block b;
                if (op == "^") {
                    a = MpRender(n.kids[0]);
                    if (a.lines.Length > 1) a = WrapBlock(a);
                    b = MpRender(n.kids[1]);
                    return Power(a, b);
                } else if (op == "/") {
                    // In stacked form the bar groups numerator and denominator,
                    // so children need no parentheses.
                    a = MpRender(n.kids[0]);
                    b = MpRender(n.kids[1]);
                    return Fraction(a, b);
                } else {
                    a = ChildRender(n.kids[0], prec, false);
                    b = ChildRender(n.kids[1], prec, true);
                }
                if (op == "*") return JoinBottom(new Block[] { a, Single("·"), b });
                return JoinBottom(new Block[] { a, Single(" " + op + " "), b });
            }
            if (n.kind == "call") {
                string fn = n.text.ToLower();
                if (fn == "sqrt" && n.kids.Length == 1) {
                    Block x = MpRender(n.kids[0]);
                    string top = "√" + new string('‾', x.w);
                    string[] lines = new string[x.lines.Length + 1];
                    lines[0] = top;
                    for (int i = 0; i < x.lines.Length; i++) lines[i + 1] = " " + x.lines[i];
                    int w = top.Length;
                    for (int i = 0; i < x.lines.Length; i++) if (lines[i + 1].Length > w) w = lines[i + 1].Length;
                    return new Block(lines, w);
                }
                if (fn == "abs" && n.kids.Length == 1) {
                    return JoinBottom(new Block[] { Single("|"), MpRender(n.kids[0]), Single("|") });
                }
                List<Block> seq = new List<Block>();
                seq.Add(Single(n.text + "("));
                for (int i = 0; i < n.kids.Length; i++) {
                    seq.Add(MpRender(n.kids[i]));
                    if (i < n.kids.Length - 1) seq.Add(Single(", "));
                }
                seq.Add(Single(")"));
                return JoinBottom(seq.ToArray());
            }
            return Single(n.text);
        }

        private static Block JoinBottom(Block[] blocks) {
            int h = 0, w = 0;
            foreach (Block b in blocks) {
                if (b.lines.Length > h) h = b.lines.Length;
                w += b.w;
            }
            string[] lines = new string[h];
            for (int i = 0; i < h; i++) lines[i] = "";
            foreach (Block b in blocks) {
                int top = h - b.lines.Length;
                for (int i = 0; i < h; i++) {
                    int li = i - top;
                    if (li >= 0 && li < b.lines.Length) lines[i] += b.lines[li].PadRight(b.w);
                    else lines[i] += new string(' ', b.w);
                }
            }
            return new Block(lines, w);
        }

        private static Block Fraction(Block a, Block b) {
            int w = a.w > b.w ? a.w : b.w;
            string[] an = CenterBlock(a, w);
            string[] bn = CenterBlock(b, w);
            string bar = new string('─', w);
            string[] lines = new string[an.Length + 1 + bn.Length];
            for (int i = 0; i < an.Length; i++) lines[i] = an[i];
            lines[an.Length] = bar;
            for (int i = 0; i < bn.Length; i++) lines[an.Length + 1 + i] = bn[i];
            return new Block(lines, w);
        }

        private static string[] CenterBlock(Block b, int w) {
            string[] r = new string[b.lines.Length];
            int lpad = (w - b.w) / 2;
            int rpad = w - b.w - lpad;
            for (int i = 0; i < b.lines.Length; i++)
                r[i] = new string(' ', lpad) + b.lines[i].PadRight(b.w) + new string(' ', rpad);
            return r;
        }

        private static string? Superscript(string s) {
            string res = "";
            foreach (char c in s) {
                switch (c) {
                    case '0': res += "⁰"; break;
                    case '1': res += "¹"; break;
                    case '2': res += "²"; break;
                    case '3': res += "³"; break;
                    case '4': res += "⁴"; break;
                    case '5': res += "⁵"; break;
                    case '6': res += "⁶"; break;
                    case '7': res += "⁷"; break;
                    case '8': res += "⁸"; break;
                    case '9': res += "⁹"; break;
                    case '+': res += "⁺"; break;
                    case '-': res += "⁻"; break;
                    case '(': res += "⁽"; break;
                    case ')': res += "⁾"; break;
                    case 'n': res += "ⁿ"; break;
                    case 'x': res += "ˣ"; break;
                    case 'i': res += "ⁱ"; break;
                    default: return null;
                }
            }
            return res.Length > 0 ? res : null;
        }

        private static Block Power(Block a, Block b) {
            if (b.lines.Length == 1) {
                string? sup = Superscript(b.lines[0]);
                if (sup != null) {
                    if (a.lines.Length > 1) {
                        string[] ls = new string[a.lines.Length];
                        for (int i = 0; i < a.lines.Length; i++) ls[i] = a.lines[i];
                        ls[0] = ls[0] + sup;
                        int w2 = ls[0].Length;
                        for (int i = 1; i < ls.Length; i++) if (ls[i].Length > w2) w2 = ls[i].Length;
                        return new Block(ls, w2);
                    }
                    return Single(a.lines[0] + sup);
                }
            }
            string[] lines = new string[b.lines.Length + a.lines.Length];
            int w = a.w;
            for (int i = 0; i < b.lines.Length; i++) {
                lines[i] = new string(' ', a.w) + b.lines[i];
                if (lines[i].Length > w) w = lines[i].Length;
            }
            for (int i = 0; i < a.lines.Length; i++) lines[b.lines.Length + i] = a.lines[i];
            return new Block(lines, w);
        }
    }
}
