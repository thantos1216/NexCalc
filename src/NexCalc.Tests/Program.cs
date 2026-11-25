using System;
using NexCalc.Core;

// Headless regression suite for NexCalc.Core: arithmetic, parsing rules,
// formatting, angle modes, graphing plans, and Python script execution.
// Custom runner (no test framework): prints PASS/FAIL per group and exits
// with 0 on all-pass, 1 otherwise. Doubles are compared with relative
// tolerance 1e-12; rational results are checked as exact display text.

public class Program {
    static int passed = 0;
    static int failed = 0;

    static void Check(string label, string expected, string actual) {
        if (expected == actual) {
            passed = passed + 1;
        } else {
            failed = failed + 1;
            Console.WriteLine("FAIL " + label + ": expected [" + expected + "] got [" + actual + "]");
        }
    }

    // Expect a successful evaluation with the given display text.
    static void Calc(string label, string expr, AngleMode mode, string expected) {
        string? actual = Expression.DisplayText(expr, mode);
        Check(label, expected, actual ?? "(null)");
    }

    // MathPrint: rendered parse tree; lines trimmed of trailing spaces.
    static void MP(string label, string expr, string expected) {
        string[] ls = Format.MathPrintLines(expr);
        string actual = "";
        for (int i = 0; i < ls.Length; i++) {
            if (i > 0) actual += "\n";
            actual += ls[i].TrimEnd();
        }
        Check(label, expected, actual);
    }

    // Programmer mode: full pipeline result as "dec | hex | oct | bin".
    static void ProgCheck(string label, string expr, int radix, int bits, string expected) {
        string actual;
        try {
            string[] r = Programmer.Run(expr, radix, bits);
            actual = string.Join(" | ", r);
        } catch (ErrorType ex) {
            actual = "ERROR " + ex.userMsg;
        }
        Check(label, expected, actual);
    }

    // Programmer preprocessing: expected literal-rewritten expression.
    static void PreCheck(string label, string expr, int radix, int bits, string expected) {
        Check(label, expected, Programmer.Preprocess(expr, radix, bits));
    }

    // Programmer mode: expect a failure whose message contains the part.
    static void ProgErr(string label, string expr, int radix, int bits, string expectedPart) {
        string actual;
        try {
            string[] r = Programmer.Run(expr, radix, bits);
            actual = "OK " + string.Join(" | ", r);
        } catch (ErrorType ex) {
            actual = ex.userMsg;
        } catch (Exception ex) {
            actual = ex.Message;
        }
        if (actual.Contains(expectedPart)) {
            passed = passed + 1;
        } else {
            failed = failed + 1;
            Console.WriteLine("FAIL " + label + ": expected part [" + expectedPart + "] got [" + actual + "]");
        }
    }

    // Display under FIX/SCI/NORM with a digit count, exact string compare.
    static void FmtCheck(string label, string expr, string fmt, int digits, string expected) {
        Result r = Expression.Calculate(expr, AngleMode.Deg);
        string got;
        if (!r.ok) {
            got = "ERROR " + r.message;
        } else {
            DisplayFormat f = fmt == "FIX" ? DisplayFormat.Fix
                : fmt == "SCI" ? DisplayFormat.Sci
                : fmt == "DMS" ? DisplayFormat.Dms
                : fmt == "FRAC" ? DisplayFormat.Frac
                : fmt == "MIXED" ? DisplayFormat.Mixed : DisplayFormat.Norm;
            got = Format.Display(r.value!, f, digits);
        }
        Check(label, expected, got);
    }

    // Base conversion: evaluate in fromRadix, digits rendered in toRadix.
    static void ConvertCheck(string label, string expr, int fromR, int toR, int bits, string expected) {
        string got;
        try {
            got = Programmer.ConvertBase(expr, fromR, toR, bits);
        } catch (ErrorType ex) {
            got = "ERROR " + ex.userMsg;
        } catch (Exception ex) {
            got = "ERROR " + ex.Message;
        }
        Check(label, expected, got);
    }

    static void ConvertErr(string label, string expr, int fromR, int toR, int bits, string expectedPart) {
        string got;
        try {
            got = "OK " + Programmer.ConvertBase(expr, fromR, toR, bits);
        } catch (ErrorType ex) {
            got = ex.userMsg;
        } catch (Exception ex) {
            got = ex.Message;
        }
        if (got.Contains(expectedPart)) {
            passed = passed + 1;
        } else {
            failed = failed + 1;
            Console.WriteLine("FAIL " + label + ": expected part [" + expectedPart + "] got [" + got + "]");
        }
    }

    // Expect a failure whose message contains the expected part.
    static void CalcError(string label, string expr, AngleMode mode, string expectedPart) {
        Result r = Expression.Calculate(expr, mode);
        if (r.ok) {
            failed = failed + 1;
            Console.WriteLine("FAIL " + label + ": expected error, got value " + Format.Display(r.value!));
        } else {
            string msg = r.message ?? "(null)";
            if (msg.Contains(expectedPart)) {
                passed = passed + 1;
            } else {
                failed = failed + 1;
                Console.WriteLine("FAIL " + label + ": expected message containing [" + expectedPart + "] got [" + msg + "]");
            }
        }
    }

    // Random functions: verify result lands inside the valid range.
    static void RandRangeCheck(string label, string expr, double lo, double hi) {
        string? t = Expression.DisplayText(expr, AngleMode.Deg);
        if (t == null) { Check(label, "in", "error"); return; }
        double d = double.Parse(t, System.Globalization.CultureInfo.InvariantCulture);
        Check(label, "in", (d >= lo && d < hi) ? "in" : "out " + t);
    }

    static void RandIntCheck(string label, string expr, long lo, long hi) {
        string? t = Expression.DisplayText(expr, AngleMode.Deg);
        if (t == null) { Check(label, "in", "error"); return; }
        long n = long.Parse(t, System.Globalization.CultureInfo.InvariantCulture);
        Check(label, "in", (n >= lo && n <= hi) ? "in" : "out " + t);
    }

    static void CheckClose(string label, double expected, double actual) {
        bool ok = Math.Abs(expected - actual) <= 1e-12 * Math.Max(1.0, Math.Abs(expected));
        if (ok) {
            passed = passed + 1;
        } else {
            failed = failed + 1;
            Console.WriteLine("FAIL " + label + ": expected [" + expected + "] got [" + actual + "]");
        }
    }

    static void CheckPolarRange(string label, GraphPlan p, double xLo, double xHi, double yLo, double yHi) {
        double pxa = 1e300, pxb = -1e300, pya = 1e300, pyb = -1e300;
        for (int q = 0; q < p.xs.Length; q = q + 1) {
            double px = p.polarXs![q];
            double py = p.polarYs![q];
            if (!Eval.IsNaN(px)) { if (px < pxa) pxa = px; if (px > pxb) pxb = px; }
            if (!Eval.IsNaN(py)) { if (py < pya) pya = py; if (py > pyb) pyb = py; }
        }
        bool ok = Math.Abs(pxa - xLo) <= 0.001 && Math.Abs(pxb - xHi) <= 0.001
               && Math.Abs(pya - yLo) <= 0.001 && Math.Abs(pyb - yHi) <= 0.001;
        if (ok) {
            passed = passed + 1;
        } else {
            failed = failed + 1;
            Console.WriteLine("FAIL " + label + ": expected x[" + xLo + "," + xHi + "] y[" + yLo + "," + yHi + "] got x[" + pxa + "," + pxb + "] y[" + pya + "," + pyb + "]");
        }
    }

    static int Main() {
        AngleMode deg = AngleMode.Deg;
        AngleMode rad = AngleMode.Rad;
        AngleMode grad = AngleMode.Grad;

        // ---- basic arithmetic, precedence and associativity ----
        Calc("2+3*4", "2+3*4", deg, "14");
        Calc("2*3+4*5", "2*3+4*5", deg, "26");
        Calc("10-2-3", "10-2-3", deg, "5");
        Calc("2^3^2 (right assoc)", "2^3^2", deg, "512");
        Calc("(-3)^2", "(-3)^2", deg, "9");
        Calc("unary minus binds to primary: -3^2", "-3^2", deg, "9");
        Calc("6/4", "6/4", deg, "1.5");
        Calc("1/4 exact decimal", "1/4", deg, "0.25");
        Calc("10/4 exact decimal", "10/4", deg, "2.5");
        Calc("1/3 repeating -> 16 sig digits", "1/3", deg, "0.3333333333333333");
        Calc("2/3 repeating -> 16 sig digits", "2/3", deg, "0.6666666666666666");
        Calc("2^70 exact big integer -> E notation", "2^70", deg, "1.180591620717411E+21");
        Calc("1e21 -> E notation", "10^21", deg, "1E+21");
        Calc("parentheses", "(2+3)*4", deg, "20");

        // ---- factorial ----
        Calc("5!", "5!", deg, "120");
        Calc("0!", "0!", deg, "1");
        Calc("10!", "10!", deg, "3628800");
        CalcError("(-3)! -> NotAnInteger", "(-3)!", deg, "factorial");
        CalcError("2.5! -> not integer", "2.5!", deg, "factorial");

        // ---- word operators (Windows Calc semantics) ----
        Calc("10 mod 3", "10 mod 3", deg, "1");
        Calc("10+5% -> 10.5", "10+5%", deg, "10.5");
        Calc("200*15% -> 30", "200*15%", deg, "30");
        Calc("12 and 10", "12 and 10", deg, "8");
        Calc("12 or 10", "12 or 10", deg, "14");
        Calc("12 xor 10", "12 xor 10", deg, "6");
        Calc("1 shl 3", "1 shl 3", deg, "8");
        Calc("10 shr 1", "10 shr 1", deg, "5");
        Calc("~5 = -6", "~5", deg, "-6");

        // ---- base literals ----
        Calc("0xFF", "0xFF", deg, "255");
        Calc("0b1010", "0b1010", deg, "10");
        Calc("0o17", "0o17", deg, "15");

        // ---- functions ----
        Calc("sin(90 DEG) exact 1", "sin(90)", deg, "1");
        Calc("cos(60 DEG) exact 1/2", "cos(60)", deg, "0.5");
        Calc("tan(45 DEG) exact 1", "tan(45)", deg, "1");
        CalcError("tan(90 DEG) -> OutOfRange", "tan(90)", deg, "undefined");
        Calc("sin(pi/2 RAD) = 1", "sin(pi/2)", rad, "1");
        Calc("sqrt(4)", "sqrt(4)", deg, "2");
        Calc("sqrt(2)", "sqrt(2)", deg, "1.414213562373095");
        Calc("cbrt(-8)", "cbrt(-8)", deg, "-2");
        Calc("hypot(3,4)", "hypot(3,4)", deg, "5");
        Calc("log(1000)", "log(1000)", deg, "3");
        Calc("ln(e)", "ln(e)", deg, "1");
        Calc("exp(1) approx", "exp(1)", deg, "2.718281828459045");
        Calc("abs(-7)", "abs(-7)", deg, "7");
        Calc("floor(2.7)", "floor(2.7)", deg, "2");
        Calc("ceil(2.2)", "ceil(2.2)", deg, "3");
        Calc("2^0.5", "2^0.5", deg, "1.414213562373095");
        Calc("8^(1/3)", "8^(1/3)", deg, "2");
        Calc("gcd(12,18)", "gcd(12,18)", deg, "6");
        Calc("lcm(4,6)", "lcm(4,6)", deg, "12");

        // ---- GRAD mode ----
        Calc("sin(100 GRAD) = 1", "sin(100)", AngleMode.Grad, "1");

        // ---- juxtaposition and unary chains ----
        Calc("2(3) = 6 implicit mult", "2(3)", deg, "6");
        Calc("-(-5) = 5", "-(-5)", deg, "5");

        // ---- error handling ----
        CalcError("div by zero", "1/0", deg, "divide by 0");
        CalcError("missing closing paren", "(2+3", deg, "mismatch");
        CalcError("unknown identifier", "foo(2)", deg, "Unknown function");
        CalcError("empty expression", "", deg, "empty");

        // ---- display helpers ----
        Check("ToBase hex", "FF", Format.ToBase(new System.Numerics.BigInteger(255), 16, 0));
        Check("ToBase bin", "1010", Format.ToBase(new System.Numerics.BigInteger(10), 2, 0));

        // ---- graphing: 2D function plan ----
        GraphPlan p = Graph.Plan("sin(x)", -3.141592653589793, 3.141592653589793, 9, AngleMode.Rad);
        if (!p.ok) { failed = failed + 1; Console.WriteLine("FAIL graph sin(x): ok=false " + p.message); }
        else {
            passed = passed + 1;
            CheckClose("graph x[0]", -3.141592653589793, p.xs[0]);
            CheckClose("graph x[4] ~ 0", 0.0, p.xs[4]);
            CheckClose("graph y[4] ~ sin(0)", 0.0, p.ys[4]);
            Check("graph y range spans [-1,1]", "True", (p.yMin <= -1.0 && p.yMax >= 1.0).ToString());
        }

        GraphPlan hyp = Graph.Plan("1/x", -2.0, 2.0, 9, AngleMode.Rad);
        if (!hyp.ok) { failed = failed + 1; Console.WriteLine("FAIL graph 1/x: ok=false"); }
        else {
            passed = passed + 1;
            Check("graph 1/x center NaN", "True", double.IsNaN(hyp.ys[4]).ToString());
            if (hyp.breaks < 1) { failed = failed + 1; Console.WriteLine("FAIL graph 1/x: expected breaks >= 1, got " + hyp.breaks); }
        }

        GraphPlan badGraph = Graph.Plan("sin(", 0, 1, 5, AngleMode.Deg);
        if (badGraph.ok) { failed = failed + 1; Console.WriteLine("FAIL graph parse error not reported"); }
        else {
            string gmsg = badGraph.message ?? "(null)";
            if (gmsg.Contains("mismatch")) { passed = passed + 1; }
            else { failed = failed + 1; Console.WriteLine("FAIL graph parse error message: [" + gmsg + "]"); }
        }

        // ---- graphing: polar plan (circle r = 1) ----
        GraphPlan c = Graph.PlanPolar("1", 0, 360, 5, AngleMode.Deg);
        if (!c.ok) { failed = failed + 1; Console.WriteLine("FAIL polar r=1: ok=false"); }
        else {
            passed = passed + 1;
            CheckClose("polar x[0] = 1", 1.0, c.polarXs![0]);
            CheckClose("polar y[0] = 0", 0.0, c.polarYs![0]);
            CheckClose("polar x[2] = -1", -1.0, c.polarXs![2]);
            CheckClose("polar y[1] = 1 (theta 90)", 1.0, c.polarYs![1]);
            CheckClose("polar y[3] = -1 (theta 270)", -1.0, c.polarYs![3]);
        }

        // ---- Python execution ----
        PythonRunner.RunResult pr = PythonRunner.Run("print(2+3)\nprint('nexcalc')", 15000);
        if (pr.note != null) {
            Console.WriteLine("SKIP python: " + pr.note);
        } else {
            Check("python exit code", "0", pr.exitCode.ToString());
            Check("python output", "5\nnexcalc\n", pr.output.Replace("\r\n", "\n"));
            Check("python stderr empty", "", pr.errorText);
        }

        PythonRunner.RunResult pe = PythonRunner.Run("import sys\nsys.stderr.write('boom')\nsys.exit(2)", 15000);
        if (pe.note != null) {
            Console.WriteLine("SKIP python-error: " + pe.note);
        } else {
            Check("python nonzero exit", "2", pe.exitCode.ToString());
            Check("python stderr text", "boom", pe.errorText);
        }

        PythonRunner.RunResult pt = PythonRunner.Run("while True: pass", 2000);
        if (pt.note != null) {
            Console.WriteLine("SKIP python-timeout: " + pt.note);
        } else {
            Check("python timeout kills run", "True", pt.timedOut.ToString());
        }

        // ---- ANS (last answer) ----
        Val one = Val.OfDouble(1.0);
        Result ar = Expression.Calculate("ANS*2", deg, one);
        Check("ANS*2 display", "2", ar.ok && ar.value != null ? Format.Display(ar.value) : "(fail)");
        CalcError("ANS-without-last", "ANS", deg, "Unknown identifier");

        // ---- polar full-circle sweeps (angle modes) ----
        GraphPlan pgDeg = Graph.PlanPolar("cos(θ)", 0, 360, 100, deg);
        CheckPolarRange("DEG polar cos", pgDeg, 0.0, 1.0, -0.5, 0.5);
        GraphPlan pgRad = Graph.PlanPolar("cos(θ)", 0, 2 * Math.PI, 100, rad);
        CheckPolarRange("RAD polar cos", pgRad, 0.0, 1.0, -0.5, 0.5);
        GraphPlan pgGrad = Graph.PlanPolar("cos(θ)", 0, 400, 100, grad);
        CheckPolarRange("GRAD polar cos", pgGrad, 0.0, 1.0, -0.5, 0.5);

        // ---- constants and parentheses (keypad row 8) ----
        Check("pi display", Math.PI.ToString("R"), Expression.DisplayText("pi", deg) ?? "(null)");
        Check("e display", Math.E.ToString("R"), Expression.DisplayText("e", deg) ?? "(null)");
        Check("tau display", (2 * Math.PI).ToString("R"), Expression.DisplayText("tau", deg) ?? "(null)");
        Calc("2*(3+4) = 14", "2*(3+4)", deg, "14");
        Calc("pi symbol \u03C0", "2*\u03C0", deg, (2 * Math.PI).ToString("R"));
        Calc("\u03C0*2 order", "\u03C0*2", deg, (2 * Math.PI).ToString("R"));
        Calc("\u03C0 alone", "\u03C0", deg, Math.PI.ToString("R"));

        // ---- Programmer (Base-N) mode ----
        ProgCheck("F0+30 HEX32", "F0 + 30", 16, 32,
            "288 | 00000120 | 00000000440 | 00000000000000000000000100100000");
        ProgCheck("FF as signed BIN8", "FF", 16, 8, "-1 | FF | 377 | 11111111");
        ProgCheck("1011 BIN8", "1011", 2, 8, "11 | 0B | 013 | 00001011");
        ProgCheck("12 OCT8", "12", 8, 8, "10 | 0A | 012 | 00001010");
        ProgCheck("288 DEC16", "288", 10, 16, "288 | 0120 | 000440 | 0000000100100000");
        ProgCheck("F0 and 30", "F0 and 30", 16, 8, "48 | 30 | 060 | 00110000");
        ProgCheck("1 shl 4", "1 shl 4", 10, 8, "16 | 10 | 020 | 00010000");
        ProgCheck("NOT ~5 BIN8", "~5", 10, 8, "-6 | FA | 372 | 11111010");
        ProgCheck("127+1 as word BIN8", "127+1", 10, 8, "-128 | 80 | 200 | 10000000");
        ProgCheck("-128 BIN8", "-128", 10, 8, "-128 | 80 | 200 | 10000000");
        PreCheck("pre F0+30", "F0 + 30", 16, 32, "0xF0 + 0x30");
        PreCheck("pre abs(FF)", "abs(FF)", 16, 32, "abs(0xFF)");
        PreCheck("pre e constant", "e*2", 16, 32, "e*0x2");
        PreCheck("pre 12 octal", "12", 8, 32, "0o12");
        PreCheck("pre 1011 bin", "1011", 2, 32, "0b1011");
        PreCheck("pre dec", "12", 10, 32, "12");
        PreCheck("pre 0x verbatim", "0xF0", 16, 32, "0xF0");
        PreCheck("pre pi*2", "pi*2", 16, 32, "pi*0x2");
        PreCheck("pre tau+1", "tau+1", 16, 32, "tau+0x1");
        PreCheck("pre ans+1", "ans+1", 16, 32, "ans+0x1");
        ProgErr("F0+30 overflow 8bit", "F0 + 30", 16, 8, "representable");
        ProgErr("123 too many HEX digits", "123", 16, 8, "digits exceeds");
        ProgErr("8 invalid in OCT", "8", 8, 32, "not valid");
        ProgErr("F invalid in DEC", "F", 10, 32, "not valid");
        ProgErr("F0/25 non-integer", "F0 / 25", 16, 32, "integer");
        ProgCheck("F0/30 exact quotient", "F0 / 30", 16, 32, "5 | 00000005 | 00000000005 | 00000000000000000000000000000101");
        Calc("idiv(15,2) = 7", "idiv(15,2)", deg, "7");
        Calc("idiv(-15,2) = -7", "idiv(-15,2)", deg, "-7");
        CalcError("idiv by zero", "idiv(1,0)", deg, "divide by 0");

        // ---- display formats (iteration 2) ----
        FmtCheck("norm 2/3", "2/3", "NORM", 0, "0.6666666666666666");
        FmtCheck("fix2 2/3", "2/3", "FIX", 2, "0.67");
        FmtCheck("fix0 1.5", "3/2", "FIX", 0, "2");
        FmtCheck("fix0 -2.5", "-5/2", "FIX", 0, "-3");
        FmtCheck("fix4 pi", "pi", "FIX", 4, "3.1416");
        FmtCheck("fix2 big", "123456.789", "FIX", 2, "123456.79");
        FmtCheck("fix0 zero", "0", "FIX", 0, "0");
        FmtCheck("fix4 e", "e", "FIX", 4, "2.7183");
        FmtCheck("sci3 123456", "123456", "SCI", 3, "1.235e+5");
        FmtCheck("sci0 123456", "123456", "SCI", 0, "1e+5");
        FmtCheck("sci5 small", "0.0012345", "SCI", 5, "1.23450e-3");
        FmtCheck("sci2 10^21", "10^21", "SCI", 2, "1.00e+21");
        FmtCheck("sci3 carry", "9.999", "SCI", 2, "1.00e+1");
        FmtCheck("sci0 0", "0", "SCI", 0, "0e+0");
        FmtCheck("sci3 neg", "-123456", "SCI", 3, "-1.235e+5");

        // ---- base conversion (programmer base switching) ----
        ConvertCheck("conv 16 DEC->HEX", "16", 10, 16, 32, "10");
        ConvertCheck("conv 16 DEC->BIN", "16", 10, 2, 32, "10000");
        ConvertCheck("conv F0+30 HEX->DEC", "F0 + 30", 16, 10, 32, "288");
        ConvertCheck("conv 288 DEC->HEX", "288", 10, 16, 32, "120");
        ConvertCheck("conv -5 DEC->HEX", "-5", 10, 16, 8, "-5");
        ConvertCheck("conv 288 DEC->BIN", "288", 10, 2, 16, "100100000");
        ConvertCheck("conv FF HEX->DEC 8bit", "FF", 16, 10, 8, "-1");
        ConvertErr("conv 288 DEC->BIN 8bit", "288", 10, 2, 8, "representable");
        ConvertErr("conv 288 DEC->HEX 8bit", "288", 10, 16, 8, "representable");

        // ---- DMS (degrees/minutes/seconds) ----
        Calc("dms 12,30,15", "dms(12,30,15)", deg, "12.50416666666667");
        Calc("dms 12,30,0", "dms(12,30,0)", deg, "12.5");
        Calc("dms -12,30,15", "dms(-12,30,15)", deg, "-12.50416666666667");
        Calc("dms 12,60,0", "dms(12,60,0)", deg, "13");
        Calc("dms 0,0,3600", "dms(0,0,3600)", deg, "1");
        Calc("dms 12,30,15.5", "dms(12,30,15.5)", deg, "12.50430555555556");
        CalcError("dms arity", "dms(12,30)", deg, "3 argument");
        CalcError("dms neg min", "dms(12,-30,0)", deg, "non-negative");
        CalcError("dms frac deg", "dms(12.5,30,0)", deg, "integers");

        FmtCheck("dms disp 12.5", "12.5", "DMS", 0, "12°30′0″");
        FmtCheck("dms disp neg", "-12.5", "DMS", 0, "-12°30′0″");
        FmtCheck("dms disp carry", "11.999999", "DMS", 0, "12°0′0″");
        FmtCheck("dms disp sec", "12.504166666666667", "DMS", 0, "12°30′15″");
        FmtCheck("dms disp tiny", "0.0000001", "DMS", 5, "0°0′0.00036″");
        FmtCheck("dms disp int", "45", "DMS", 0, "45°0′0″");

        // ---- Fractions ----
        Calc("frac 3/2", "frac(3,2)", deg, "1.5");
        Calc("frac 6/4 slash", "6/4", deg, "1.5");
        Calc("frac 1/3", "frac(1,3)", deg, "0.3333333333333333");
        Calc("frac neg", "frac(-3,2)", deg, "-1.5");
        CalcError("frac zero", "frac(3,0)", deg, "divide by 0");
        CalcError("frac arity", "frac(3)", deg, "2 argument");
        CalcError("frac non-int", "frac(1.5,2)", deg, "integers");

        FmtCheck("frac disp 1/3", "1/3", "FRAC", 0, "1/3");
        FmtCheck("frac disp reduce", "6/4", "FRAC", 0, "1 1/2");
        FmtCheck("frac disp int", "4", "FRAC", 0, "4");
        FmtCheck("frac disp neg", "-3/2", "FRAC", 0, "-1 1/2");
        FmtCheck("frac disp 17/10", "17/10", "FRAC", 0, "1 7/10");
        FmtCheck("frac disp 17/10 via frac()", "frac(17,10)", "FRAC", 0, "1 7/10");
        FmtCheck("frac disp irrational", "sqrt(2)", "FRAC", 0, "1.414213562373095");
        FmtCheck("mixed 6/4", "6/4", "MIXED", 0, "1 1/2");
        FmtCheck("mixed 10/3", "10/3", "MIXED", 0, "3 1/3");
        FmtCheck("mixed neg", "-10/3", "MIXED", 0, "-3 1/3");
        FmtCheck("mixed int", "6/3", "MIXED", 0, "2");

        // ---- Extra functions ----
        Calc("square 5", "square(5)", deg, "25");
        Calc("square 0.5", "square(0.5)", deg, "0.25");
        Calc("cube 2", "cube(2)", deg, "8");
        Calc("cube -3", "cube(-3)", deg, "-27");
        Calc("recip 4", "recip(4)", deg, "0.25");
        Calc("recip 2", "recip(2)", deg, "0.5");
        Calc("tenpow 3", "tenpow(3)", deg, "1000");
        Calc("tenpow -2", "tenpow(-2)", deg, "0.01");
        Calc("tenpow 0", "tenpow(0)", deg, "1");
        Calc("logb 2 8", "logb(2,8)", deg, "3");
        Calc("logb 10 1000", "logb(10,1000)", deg, "3");
        Calc("logb 2 1", "logb(2,1)", deg, "0");
        CalcError("recip 0", "recip(0)", deg, "divide by 0");
        CalcError("logb base1", "logb(1,5)", deg, "not 1");
        CalcError("logb neg base", "logb(-2,5)", deg, "positive");
        CalcError("logb neg arg", "logb(2,-8)", deg, "positive");
        CalcError("square arity", "square(2,3)", deg, "1 argument");
        CalcError("randint reverse", "randint(5,2)", deg, "<= upper");
        CalcError("randint frac", "randint(1.5,4)", deg, "integers");
        CalcError("randint arity", "randint(1)", deg, "2 argument");
        CalcError("rand with arg", "rand(1)", deg, "0 argument");
        FmtCheck("square frac", "square(3/2)", "FRAC", 0, "2 1/4");
        Calc("mixed input 1 1/2", "1 1/2", deg, "1.5");
        Calc("mixed input 2 3/4", "2 3/4", deg, "2.75");
        Calc("mixed input neg", "-1 1/2", deg, "-1.5");
        Calc("mixed input add", "60+2 1/2", deg, "62.5");
        Calc("mixed input mul", "2 3/4*2", deg, "5.5");
        Calc("mixed input paren square", "(1 1/2)^2", deg, "2.25");
        Calc("mixed input decimal whole", "2.5 1/2", deg, "3");
        Calc("mixed input improper part", "3 12/5", deg, "5.4");
        CalcError("mixed input zero den", "2 3/0", deg, "divide by 0");
        CalcError("mixed input lone pair", "2 3", deg, "missing operator");
        FmtCheck("mixed input disp", "1 1/2", "FRAC", 0, "1 1/2");

        // ---- Fraction key behavior: what the d/c + SPC keys generate ----
        Calc("key 7/16", "7/16", deg, "0.4375");
        Calc("key empty 1/2", "1/2", deg, "0.5");
        Calc("key mixed 2 2/5", "2 2/5", deg, "2.4");
        Calc("key mixed spaces", "2 2/5 + 1", deg, "3.4");
        Calc("key mixed in call", "sqrt(1 1/4)", deg, "1.118033988749895");
        FmtCheck("key mixed FRAC", "2 2/5", "FRAC", 0, "2 2/5");
        Calc("key dslash", "1/2/3", deg, "0.1666666666666666");
        FmtCheck("tenpow -2 frac", "tenpow(-2)", "FRAC", 0, "1/100");
        RandRangeCheck("rand 0-1 a", "rand()", 0.0, 1.0);
        RandRangeCheck("rand 0-1 b", "rand()", 0.0, 1.0);
        RandRangeCheck("rand 0-1 c", "rand()", 0.0, 1.0);
        RandIntCheck("randint 1-6 a", "randint(1,6)", 1, 6);
        RandIntCheck("randint 1-6 b", "randint(1,6)", 1, 6);
        RandIntCheck("randint 1-6 c", "randint(1,6)", 1, 6);
        RandIntCheck("randint 1-6 d", "randint(1,6)", 1, 6);

        // ---- Pol / Rec (ordered state checks: pol/rec store the pair, X()/Y() recall it) ----
        CalcError("X before any pair", "X()", deg, "no Pol/Rec result available");
        CalcError("Y before any pair", "Y()", deg, "no Pol/Rec result available");
        Calc("pol 5,0 deg", "pol(5,0)", deg, "5");
        Calc("Y after pol 5,0", "Y()", deg, "0");
        Calc("pol 5,90 deg", "pol(5,90)", deg, "0");
        Calc("Y after pol 5,90", "Y()", deg, "5");
        Calc("pol 5,60 deg", "pol(5,60)", deg, "2.5");
        Calc("Y after pol 5,60", "Y()", deg, Format.Display(Val.OfDouble(5.0 * Math.Sin(Eval.ToRad(Val.OfInt(60), deg)))));
        Calc("rec 3,4 deg", "rec(3,4)", deg, "5");
        Calc("X after rec 3,4", "X()", deg, "5");
        Calc("Y after rec 3,4", "Y()", deg, Format.Display(Val.OfDouble(Math.Atan2(4.0, 3.0) * 180.0 / Eval.ToRad(Val.OfInt(180), deg))));
        Calc("rec 0,0", "rec(0,0)", deg, "0");
        Calc("Y after rec 0,0", "Y()", deg, "0");
        Calc("pol 1,0 rad", "pol(1,0)", rad, "1");
        Calc("pol 1,100 grad", "pol(1,100)", grad, "0");
        Calc("rec 1,0 rad", "rec(1,0)", rad, "1");
        Calc("Y after rec 1,0 rad", "Y()", rad, "0");
        CalcError("pol arity", "pol(5)", deg, "2 argument");
        CalcError("rec arity", "rec(3)", deg, "2 argument");
        CalcError("X with arg", "X(1)", deg, "0 argument");
        CalcError("Y with arg", "Y(2)", deg, "0 argument");

        // ---- Memory + variables (ordered: madd/mc mutate memory, sto binds letters) ----
        Calc("m empty", "m()", deg, "0");
        Calc("madd 2", "madd(2)", deg, "2");
        Calc("m is 2", "m()", deg, "2");
        Calc("madd 3", "madd(3)", deg, "5");
        Calc("m is 5", "m()", deg, "5");
        Calc("mc clears", "mc()", deg, "0");
        Calc("m after mc", "m()", deg, "0");
        CalcError("unbound u", "u+1", deg, "Unknown identifier");
        Calc("sto a 5", "sto(a,5)", deg, "5");
        Calc("use a", "a+2", deg, "7");
        Calc("sto b nested", "sto(b,sto(a,3))", deg, "3");
        Calc("use b", "b*10", deg, "30");
        Calc("rcl a", "rcl(a)", deg, "3");
        Calc("sto frac", "sto(d,1/4)", deg, "0.25");
        Calc("use d", "d*8", deg, "2");
        CalcError("sto reserved e", "sto(e,1)", deg, "reserved");
        CalcError("sto non-letter", "sto(2,3)", deg, "single letter");
        CalcError("sto arity", "sto(a)", deg, "2 argument");
        CalcError("rcl unbound", "rcl(k)", deg, "Unknown identifier");
        CalcError("rcl arity", "rcl()", deg, "1 argument");
        CalcError("madd arity", "madd(1,2)", deg, "1 argument");

        // ---- Statistics (SD mode; ordered: data() accumulates, clrd() resets) ----
        CalcError("stats empty sd", "sd()", deg, "No data values");
        CalcError("stats empty mean", "mean()", deg, "No data values");
        Calc("data 10", "data(10)", deg, "10");
        Calc("data 12", "data(12)", deg, "12");
        Calc("data 14", "data(14)", deg, "14");
        Calc("stats n", "n()", deg, "3");
        Calc("stats sumx", "sumx()", deg, "36");
        Calc("stats sumx2", "sumx2()", deg, "440");
        Calc("stats mean", "mean()", deg, "12");
        Calc("stats var", "var()", deg, "2.666666666666666");
        Calc("stats sd", "sd()", deg, "1.632993161855452");
        Calc("stats sds", "sds()", deg, "2");
        Calc("data frac", "data(1/2)", deg, "0.5");
        Calc("stats n4", "n()", deg, "4");
        Calc("stats sumx4", "sumx()", deg, "36.5");
        Calc("stats minx", "minx()", deg, "0.5");
        Calc("stats maxx", "maxx()", deg, "14");
        Calc("stats clrd", "clrd()", deg, "0");
        Calc("stats n0", "n()", deg, "0");
        Calc("data 5", "data(5)", deg, "5");
        CalcError("sds one point", "sds()", deg, "at least 2");
        CalcError("sd after clear", "clrd() sd()", deg, "No data values");
        CalcError("data arity", "data(1,2)", deg, "1 argument");
        CalcError("n arity", "n(1)", deg, "0 argument");
        CalcError("sumx arity", "sumx(2)", deg, "0 argument");

        // ---- Statistics REG mode: (x,y) pairs, six regression models ----
        CalcError("reg r empty", "r()", deg, "No data pairs. Add one with dat() first.");
        Calc("dat pair 1", "dat(1,2)", deg, "2");
        Calc("dat pair 2", "dat(2,4)", deg, "4");
        Calc("dat pair 3", "dat(3,8)", deg, "8");
        Calc("reg rn", "rn()", deg, "3");
        Calc("reg rsumx", "rsumx()", deg, "6");
        Calc("reg rsumy", "rsumy()", deg, "14");
        Calc("reg rsumx2", "rsumx2()", deg, "14");
        Calc("reg rsumy2", "rsumy2()", deg, "84");
        Calc("reg rsumxy", "rsumxy()", deg, "34");
        Calc("reg rmeanx", "rmeanx()", deg, "2");
        Calc("reg rmeany", "rmeany()", deg, "4.666666666666667");
        Calc("set lin", "lin()", deg, "0");
        Calc("reg lin a", "a()", deg, "3");
        Calc("reg lin b", "b()", deg, "-1.333333333333333");
        Calc("reg lin r", "r()", deg, "0.9819805060619658");
        Calc("reg lin yhat", "yhat(4)", deg, "10.66666666666667");
        Calc("reg lin xhat", "xhat(10)", deg, "3.777777777777778");
        Calc("reg rcld", "rcld()", deg, "0");
        Calc("reg rn0", "rn()", deg, "0");
        Calc("dat pair 1b", "dat(1,2)", deg, "2");
        Calc("dat pair 2b", "dat(2,4)", deg, "4");
        Calc("dat pair 3b", "dat(3,8)", deg, "8");
        Calc("set quad", "quadreg()", deg, "0");
        Calc("reg quad a", "a()", deg, "1.000000000000009");
        Calc("reg quad b", "b()", deg, "-1.000000000000036");
        Calc("reg quad c", "c()", deg, "2.000000000000033");
        Calc("reg quad r", "r()", deg, "1");
        Calc("reg quad yhat", "yhat(4)", deg, "14.00000000000002");
        Calc("reg quad xhat", "xhat(11)", deg, "3.541381265149108");
        Calc("reg rcld2", "rcld()", deg, "0");
        Calc("dat pair 1c", "dat(0,1)", deg, "1");
        Calc("dat pair 2c", "dat(1,2)", deg, "2");
        Calc("dat pair 3c", "dat(2,4)", deg, "4");
        Calc("set exp", "expreg()", deg, "0");
        Calc("reg exp a", "a()", deg, "0.62703493969601");
        Calc("reg exp b", "b()", deg, "0.460857067136603");
        Calc("reg exp r", "r()", deg, "0.997508537508909");
        Calc("reg exp yhat", "yhat(3)", deg, "9.883596577980228");
        Calc("reg rcld3", "rcld()", deg, "0");
        Calc("dat pair 1d", "dat(1,2)", deg, "2");
        Calc("dat pair 2d", "dat(2,8)", deg, "8");
        Calc("dat pair 3d", "dat(4,32)", deg, "32");
        Calc("set pow", "powreg()", deg, "0");
        Calc("reg pow a", "a()", deg, "2.000000000000001");
        Calc("reg pow b", "b()", deg, "1.999999999999999");
        Calc("reg pow r", "r()", deg, "1");
        Calc("reg pow yhat", "yhat(3)", deg, "18");
        Calc("reg pow xhat", "xhat(50)", deg, "5.000000000000001");
        Calc("reg rcld4", "rcld()", deg, "0");
        Calc("dat pair 1e", "dat(1,0)", deg, "0");
        Calc("dat pair 2e", "dat(e,1)", deg, "1");
        Calc("dat pair 3e", "dat(e*e,2)", deg, "2");
        Calc("set log", "logreg()", deg, "0");
        Calc("reg log a", "a()", deg, "0");
        Calc("reg log b", "b()", deg, "1");
        Calc("reg log r", "r()", deg, "1");
        Calc("reg log yhat", "yhat(100)", deg, "4.605170185988091");
        Calc("reg rcld5", "rcld()", deg, "0");
        Calc("dat pair 1f", "dat(1,3)", deg, "3");
        Calc("dat pair 2f", "dat(2,4)", deg, "4");
        Calc("dat pair 3f", "dat(4,5)", deg, "5");
        Calc("set inv", "invreg()", deg, "0");
        Calc("reg inv a", "a()", deg, "-2.571428571428572");
        Calc("reg inv b", "b()", deg, "5.5");
        Calc("reg inv r", "r()", deg, "-0.9819805060619658");
        Calc("reg rcld6", "rcld()", deg, "0");
        CalcError("quad empty pairs", "quadreg() a()", deg, "No data pairs");
        Calc("dat pair 1g", "dat(1,5)", deg, "5");
        CalcError("quad two pairs", "quadreg() a()", deg, "at least 3");
        CalcError("lin one pair", "lin() a()", deg, "at least 2 data pairs");
        Calc("dat pair 2g", "dat(1,7)", deg, "7");
        CalcError("equal x values", "a()", deg, "all x values are equal");
        CalcError("c non-quad", "c()", deg, "C() is only available for quadratic");
        Calc("reg rcld7", "rcld()", deg, "0");
        Calc("dat pair neg y", "dat(1,-2)", deg, "-2");
        Calc("dat pair pos", "dat(2,8)", deg, "8");
        CalcError("pow nonpositive", "powreg() a()", deg, "needs all x > 0 and y > 0");
        CalcError("rsumxy arity", "rsumxy(1)", deg, "expects 0 argument(s), got 1");
        CalcError("dat arity 1", "dat(1)", deg, "expects 2 argument(s), got 1");
        CalcError("dat arity 3", "dat(1,2,3)", deg, "expects 2 argument(s), got 3");
        Calc("reg rcld8", "rcld()", deg, "0");

        // ---- complex numbers ----
        Calc("i imaginary unit", "i", deg, "i");
        Calc("3i implicit mult", "3i", deg, "3i");
        Calc("2i implicit mult", "2i", deg, "2i");
        Calc("neg i", "-i", deg, "-i");
        Calc("2+3i", "2+3i", deg, "2+3i");
        Calc("complex mul", "(2+3i)*(4-1i)", deg, "11+10i");
        Calc("complex div", "(2+3i)/(4+i)", deg, "0.6470588235294118+0.5882352941176471i");
        Calc("5/(2+3i)", "5/(2+3i)", deg, "0.7692307692307693-1.153846153846154i");
        Calc("complex div conj", "(1+2i)/(1-2i)", deg, "-0.6+0.8000000000000001i");
        Calc("i^2", "i^2", deg, "-1");
        Calc("i^3", "i^3", deg, "-i");
        Calc("i^4", "i^4", deg, "1");
        Calc("i^-1", "i^-1", deg, "-i");
        Calc("2i^-1", "2i^-1", deg, "-2i");
        Calc("i^i principal", "i^i", deg, "0.2078795763507619");
        Calc("2+3i^2 precedence", "2+3i^2", deg, "-1");
        Calc("2i*3i", "2i*3i", deg, "-6");
        Calc("i^2+i", "i^2+i", deg, "-1+i");
        Calc("neg complex", "-(2+3i)", deg, "-2-3i");
        Calc("sqrt negative", "sqrt(-4)", deg, "2i");
        Calc("sqrt negative double", "sqrt(-0.25)", deg, "0.5i");
        Calc("sqrt of complex", "sqrt(2+3i)", deg, "1.67414922803554+0.895977476129838i");
        Calc("modulus", "abs(3+4i)", deg, "5");
        Calc("modulus neg", "abs(-3-4i)", deg, "5");
        Calc("conjugate", "conj(2-3i)", deg, "2+3i");
        Calc("conj real passthrough", "conj(5)", deg, "5");
        Calc("real part", "real(2+3i)", deg, "2");
        Calc("imag part", "imag(2+3i)", deg, "3");
        Calc("real of real", "real(5)", deg, "5");
        Calc("imag of real", "imag(5)", deg, "0");
        Calc("arg complex", "arg(2+3i)", deg, "56.30993247402022");
        Calc("arg 1-i", "arg(1-i)", deg, "-45");
        Calc("arg -1+i", "arg(-1+i)", deg, "135");
        Calc("arg -2-3i", "arg(-2-3i)", deg, "-123.6900675259798");
        Calc("cmplx build", "cmplx(2,3)", deg, "2+3i");
        Calc("cmplx unit", "cmplx(1,1)", deg, "1+i");
        Calc("percent complex mix", "5%+2i", deg, "0.05+2i");
        CalcError("percent on complex", "2i%", deg, "does not support complex");
        CalcError("sin complex", "sin(2+3i)", deg, "does not support complex");
        CalcError("floor complex", "floor(2+3i)", deg, "does not support complex");
        CalcError("mod complex", "(2+3i) mod 2", deg, "does not support complex");
        CalcError("rec complex", "rec(2+3i,1)", deg, "does not support complex");
        CalcError("pol complex", "pol(2+3i,0)", deg, "does not support complex");
        CalcError("cmplx complex args", "cmplx(2,3i)", deg, "does not support complex");
        CalcError("zero complex div", "1/(0+0i)", deg, "divide by 0");
        FmtCheck("complex fix", "2+3i", "FIX", 2, "2.00+3.00i");
        FmtCheck("complex sci", "2+3i", "SCI", 2, "2.00e+0+3.00e+0i");

        // ---- inverse trig angle conversion (Windows behavior) ----
        Calc("asin 1 deg", "asin(1)", deg, "90");
        Calc("asin half deg", "asin(0.5)", deg, "30");
        Calc("asin neg half deg", "asin(-0.5)", deg, "-30");
        Calc("asin zero deg", "asin(0)", deg, "0");
        Calc("asin sqrt2 half deg", "asin(sqrt(2)/2)", deg, "45");
        Calc("acos half deg", "acos(0.5)", deg, "60");
        Calc("acos neg half deg", "acos(-0.5)", deg, "120");
        Calc("acos 1 deg", "acos(1)", deg, "0");
        Calc("acos 0 deg", "acos(0)", deg, "90");
        Calc("acos neg 1 deg", "acos(-1)", deg, "180");
        Calc("acos sqrt3 half deg", "acos(sqrt(3)/2)", deg, "30");
        Calc("asin half rad", "asin(0.5)", rad, "0.5235987755982988");
        Calc("asin half grad", "asin(0.5)", grad, "33.33333333333334");
        Calc("acos half grad", "acos(0.5)", grad, "66.66666666666667");
        Calc("asin general deg", "asin(0.3)", deg, "17.4576031237221");
        Calc("atan 1 deg", "atan(1)", deg, "45");
        Calc("atan neg pair", "atan(-1,-1)", deg, "-135");
        Calc("atan rad", "atan(1)", rad, "0.7853981633974483");
        Calc("atan grad", "atan(1)", grad, "50");
        CalcError("asin domain", "asin(1.5)", deg, "out of range");

        // ---- Calculus + combinatorics (iteration 11) ----
        Calc("integrate x^2", "integrate(x^2,0,10)", deg, "333.3333333333333");
        Calc("integrate x^2 w/ x var", "integrate(x^2,x,0,10)", deg, "333.3333333333333");
        Calc("integrate 2x", "integrate(2x,0,5)", deg, "25");
        Calc("integrate x reversed", "integrate(x,10,0)", deg, "-50");
        Calc("integrate (x-1)^2", "integrate(x^2-2x+1,0,3)", deg, "3");
        Calc("integrate 1/x", "integrate(1/x,1,2)", deg, "0.6931471805599766");
        Calc("integrate sqrt(x)", "integrate(sqrt(x),0,9)", deg, "17.99993068335565");
        Calc("integrate reversed 2..1", "integrate(x^2,2,1)", deg, "-2.333333333333334");
        Calc("integrate a==b", "integrate(x^2,0,0)", deg, "0");
        Calc("integrate sin RAD", "integrate(sin(x),0,pi)", rad, "2.000000000001082");
        Result cosineIntegral = Expression.Calculate("integrate(cos(x),0,pi)", rad);
        CheckClose("integrate cos RAD", 0.0, cosineIntegral.ok
            ? cosineIntegral.value!.ToDoubleValue() : double.NaN);
        CalcError("integrate singularity", "integrate(1/x,-1,1)", deg, "not defined over");
        CalcError("integrate bare sin", "integrate(sin,0,pi)", deg, "not defined over");
        CalcError("integrate 4-arg non-ident", "integrate(x^2,2,0,10)", deg, "variable name");
        CalcError("integrate complex bound", "integrate(x^2,0,1+i)", deg, "does not support complex");
        CalcError("integrate arity", "integrate(x^2,0)", deg, "expects 3");
        Calc("deriv x^2 at 1", "deriv(x^2,1)", deg, "2");
        Calc("deriv 3x^2 at 1", "deriv(3x^2,1)", deg, "6");
        Calc("deriv x^2 w/ x var", "deriv(x^2,x,3)", deg, "6");
        Calc("deriv x^2 at 2", "deriv(x^2,2)", deg, "4");
        Calc("deriv x^2 at 0", "deriv(x^2,0)", deg, "0");
        Calc("deriv constant", "deriv(5,3)", deg, "0");
        Calc("deriv cos at 0", "deriv(cos(x),0)", deg, "0");
        Calc("deriv abs at 0", "deriv(abs(x),0)", deg, "0");
        Calc("deriv sin RAD", "deriv(sin(x),0)", rad, "1");
        Calc("deriv sin DEG", "deriv(sin(x),0)", deg, "0.01745329251994329");
        // ANS chaining inside integrate (observed in the GUI): complex ANS is
        // guarded by RequireReal; a rational ANS scales the integrand exactly.
        Val ansC = Val.OfComplex(0.0, 1.0);
        Result rcAns = Expression.Calculate("integrate(ANS*x,0,2)", deg, ansC);
        Check("integrate complex ANS", "This operation does not support complex numbers.", rcAns.ok ? "ok" : rcAns.message ?? "(null)");
        Val ansR = Val.OfInt(126410606437752);
        Result rrAns = Expression.Calculate("integrate(ANS*x,0,2)", deg, ansR);
        Check("integrate rational ANS", "252821212875504", rrAns.ok ? Format.Display(rrAns.value!) : rrAns.message ?? "(null)");
        Calc("deriv cos RAD pi/2", "deriv(cos(x),pi/2)", rad, "-0.999999999998336");
        Calc("deriv exp RAD", "deriv(exp(x),1)", rad, "2.7182818284599");
        Calc("deriv 1/x at 0", "deriv(1/x,0)", deg, "-1E+40");
        Calc("deriv 3i const", "deriv(3i,1)", deg, "300000000000000000000");
        CalcError("deriv bare sin", "deriv(sin,0)", deg, "derivative does not exist");
        CalcError("deriv arity", "deriv(x^2)", deg, "expects 2");
        CalcError("deriv 3-arg non-ident", "deriv(x^2,2,3)", deg, "variable name");
        CalcError("deriv complex point", "deriv(x^2,1+i)", deg, "does not support complex");
        Calc("nCr 5 2", "combin(5,2)", deg, "10");
        Calc("nCr alias 50 25", "nCr(50,25)", deg, "126410606437752");
        Calc("nPr 5 2", "perm(5,2)", deg, "20");
        Calc("nPr alias 0 0", "nPr(0,0)", deg, "1");
        Calc("nCr n=n", "nCr(5,5)", deg, "1");
        Calc("nPr n=n", "nPr(5,5)", deg, "120");
        Calc("nCr 1000 500", "combin(1000,500)", deg, "2.702882409454366E+299");
        CalcError("nCr r>n", "combin(2,3)", deg, "r <= n");
        CalcError("nCr non-int", "combin(2.5,1)", deg, "not an integer");
        CalcError("nCr negative", "combin(-1,1)", deg, "non-negative");
        CalcError("nCr complex", "combin(2+i,1)", deg, "not an integer");

        // ---- Equation solving (iteration 12) ----
        Calc("poly2 exact roots", "poly2(1,-5,6)", AngleMode.Deg, "3, 2");
        Calc("poly2 complex pair", "poly2(1,2,5)", AngleMode.Deg, "-1+2i, -1-2i");
        Calc("poly2 irrational roots", "poly2(1,0,-2)", AngleMode.Deg, "1.414213562373095, -1.414213562373095");
        Calc("poly2 double root", "poly2(1,-5,6.25)", AngleMode.Deg, "2.5");
        Calc("poly2 complex 2.5", "poly2(1,-5,7)", AngleMode.Deg, "2.5+0.8660254037844386i, 2.5-0.8660254037844386i");
        Calc("poly2 leading 2", "poly2(2,-4,-6)", AngleMode.Deg, "3, -1");
        CalcError("poly2 a=0", "poly2(0,2,-4)", AngleMode.Deg, "The leading coefficient cannot be 0.");
        CalcError("poly2 all zero", "poly2(0,0,0)", AngleMode.Deg, "The leading coefficient cannot be 0.");
        CalcError("poly2 arity", "poly2(1,2)", AngleMode.Deg, "poly2' expects 3 argument(s), got 2.");
        CalcError("poly3 arity", "poly3(1,2)", AngleMode.Deg, "poly3' expects 4 argument(s), got 2.");
        Calc("poly4 four roots", "poly4(1,0,-5,0,4)", AngleMode.Deg, "2, 1, -1, -2");
        Calc("poly3 odd roots", "poly3(1,0,-6,0)", AngleMode.Deg, "2.449489742783178, 0, -2.449489742783178");
        Calc("poly3 integer roots", "poly3(1,-6,11,-6)", AngleMode.Deg, "3, 2, 1");
        Calc("poly4 roots of 16", "poly4(1,0,0,0,-16)", AngleMode.Deg, "2, 2i, -2i, -2");
        Calc("solve x^2-4 from 1", "solve(x^2-4,x,1)", AngleMode.Deg, "2");
        Calc("solve x^2-4 from -3", "solve(x^2-4,x,-3)", AngleMode.Deg, "-2");
        Calc("solve linear", "solve(2*x-6,x,0)", AngleMode.Deg, "3");
        Calc("solve sin DEG from 30", "solve(sin(x),x,30)", AngleMode.Deg, "0");
        Calc("solve cos DEG from 89", "solve(cos(x),x,89)", AngleMode.Deg, "90");
        Calc("solve cos DEG from 3", "solve(cos(x),x,3)", AngleMode.Deg, "1350");
        Calc("solve sin DEG from 100", "solve(sin(x),x,100)", AngleMode.Deg, "360");
        Calc("solve x^3-x from 0.5", "solve(x^3-x,x,0.5)", AngleMode.Deg, "-1");
        Calc("solve ln(x) from 2", "solve(ln(x),x,2)", AngleMode.Deg, "1");
        Calc("solve sin(x)+x", "solve(sin(x)+x,x,0.1)", AngleMode.Deg, "0");
        Calc("solve x^2-2", "solve(x^2-2,x,1)", AngleMode.Deg, "1.414213562373095");
        Calc("solve sqrt(x) undefined mid", "solve(sqrt(x),x,4)", AngleMode.Deg, "The function is not defined near that starting point.");
        CalcError("solve no real root", "solve(x^2+1,x,1)", AngleMode.Deg, "No solution near that starting point.");
        CalcError("solve exp no root", "solve(exp(x),x,1)", AngleMode.Deg, "No solution near that starting point.");
        CalcError("solve 1/x no root", "solve(1/x,x,1)", AngleMode.Deg, "No solution near that starting point.");
        CalcError("solve complex start", "solve(x^2-4,x,cmplx(1,2))", AngleMode.Deg, "This operation does not support complex numbers.");
        Calc("solve RAD sin from 3", "solve(sin(x),x,3)", AngleMode.Rad, "3.141592653589793");
        Calc("solve RAD cos from 1", "solve(cos(x),x,1)", AngleMode.Rad, "1.570796326795487");
        Calc("solve RAD pi start", "solve(x^2-4,x,pi)", AngleMode.Rad, "2");
        Calc("solve ident variable y", "solve(x^2-4,y,1)", AngleMode.Deg, "2");
        CalcError("solve arity", "solve(x^2-4,x)", AngleMode.Deg, "solve' expects 3 argument(s), got 2.");
        Calc("sinh 1", "sinh(1)", AngleMode.Deg, "1.175201193643801");
        Calc("cosh 1", "cosh(1)", AngleMode.Deg, "1.543080634815244");
        Calc("tanh 1", "tanh(1)", AngleMode.Deg, "0.7615941559557649");
        Calc("tanh 0", "tanh(0)", AngleMode.Deg, "0");
        Calc("sinh+cosh 0.5", "sinh(0.5)+cosh(0.5)", AngleMode.Deg, "1.648721270700128");
        Calc("tenpow 2 (EXP key)", "tenpow(2)", AngleMode.Deg, "100");
        Calc("roots list is display-only", "poly2(1,2,5)+1", AngleMode.Deg, "1");

        // ---- Graph Root / Inter (iteration 13D) ----
        Calc("root solve from 5", "solve(x^2-4,x,5)", deg, "2");
        Calc("root solve from trace x", "solve(x^2-4,x,5.050505050505051)", deg, "2");
        Calc("root solve from 0", "solve(x^2-4,x,0)", deg, "2");
        Calc("inter solve from 0", "solve(sin(x)-(x^2/10-3),x,0)", deg, "-5.390780905724709");
        Calc("inter solve from trace x", "solve(sin(x)-(x^2/10-3),x,0.10101010101010101)", deg, "5.565047855195381");
        Calc("inter parenthesized form", "solve((sin(x))-(x^2/10-3),x,5.0505050505050505)", deg, "5.565047855195381");

        // ---- Parametric graphs (iteration 13) ----
        GraphPlan pp = Graph.PlanParam("cos(x)", "sin(x)", 0, 2 * Math.PI, 100, AngleMode.Rad);
        Check("param ok", "True", pp.ok.ToString());
        Check("param breaks 0", "0", pp.breaks.ToString());
        CheckClose("param t0", 0.0, pp.xs[0]);
        CheckClose("param X0", 1.0, pp.polarXs![0]);
        CheckClose("param Y0", 0.0, pp.polarYs![0]);
        CheckClose("param t99", 2 * Math.PI, pp.xs[99]);
        CheckClose("param X99", 1.0, pp.polarXs![99]);
        CheckClose("param Y99", -2.4492935982947064E-16, pp.polarYs![99]);
        CheckPolarRange("param unit circle", pp, -1.0, 1.0, -1.0, 1.0);
        CheckClose("param yMin", -0.999874127673875, pp.yMin);
        CheckClose("param yMax", 0.999874127673875, pp.yMax);
        GraphPlan perr = Graph.PlanParam("x^", "x", 0, 10, 10, AngleMode.Deg);
        Check("param parse error", "Expression ended too early: value is missing.", perr.message ?? "");
        Check("param not ok", "False", perr.ok.ToString());
        GraphPlan pb = Graph.PlanParam("1/x", "x", -5, 5, 11, AngleMode.Deg);
        Check("param breaks at asymptote", "2", pb.breaks.ToString());
        CheckClose("param yMin lo", -5.0, pb.yMin);
        CheckClose("param yMax hi", 5.0, pb.yMax);
        Check("param NaN point", "True", Eval.IsNaN(pb.polarXs![5]).ToString());
        Check("param finite point", "False", Eval.IsNaN(pb.polarXs![4]).ToString());

        // ---- graph Tangent sketch (iteration 13E) ----
        // The Tangent button runs deriv(Y1, x, traced x) through Expression.Calculate.
        // These are the exact slopes for the trace points of x^2-4 over [0, 10]
        // (samples 100: point 52 is x=5.152) and sin(x) over [-10, 10].
        Calc("tangent slope x^2 at trace 52", "deriv(x^2,5.151515151515152)", AngleMode.Deg, "10.3030303030303");
        Calc("tangent slope x^2 at trace 51", "deriv(x^2,5.050505050505051)", AngleMode.Deg, "10.1010101010101");
        Calc("tangent slope x^2 at sin trace x", "deriv(x^2,0.30303030303030304)", AngleMode.Deg, "0.6060606060606061");
        Calc("tangent slope sin at trace 52", "deriv(sin(x),0.30303030303030304)", AngleMode.Deg, "0.0174530484168854");

        // ---- matrices and vectors (iteration 14) ----
        // These share matrix memory state (MatA..MatF, filled in order by
        // mat()); they must run in this exact order.
        Calc("mata empty", "mata()", AngleMode.Deg, "MatA is empty - store a matrix with mat() first.");
        Calc("mat 2x2 store", "mat(2,2,1,2,3,4)", AngleMode.Deg, "[[1 2][3 4]]");
        Calc("mat second store", "mat(2,2,5,6,7,8)", AngleMode.Deg, "[[5 6][7 8]]");
        Calc("mat add", "mata()+matb()", AngleMode.Deg, "[[6 8][10 12]]");
        Calc("mat sub", "mata()-matb()", AngleMode.Deg, "[[-4 -4][-4 -4]]");
        Calc("mat scalar left mul", "2*mata()", AngleMode.Deg, "[[2 4][6 8]]");
        Calc("mat scalar right mul", "mata()*2", AngleMode.Deg, "[[2 4][6 8]]");
        Calc("mat mul A*B", "mata()*matb()", AngleMode.Deg, "[[19 22][43 50]]");
        Calc("mat mul B*A", "matb()*mata()", AngleMode.Deg, "[[23 34][31 46]]");
        Calc("det A", "det(mata())", AngleMode.Deg, "-2");
        Calc("det B", "det(matb())", AngleMode.Deg, "-2");
        Calc("minv A", "minv(mata())", AngleMode.Deg, "[[-2 1][1.5 -0.5]]");
        Calc("mat inverse identity", "mata()*minv(mata())", AngleMode.Deg, "[[1 0][0 1]]");
        Calc("mtr A", "mtr(mata())", AngleMode.Deg, "[[1 3][2 4]]");
        Calc("melt row1col2", "melt(mata(),1,2)", AngleMode.Deg, "2");
        Calc("melt row2col1", "melt(mata(),2,1)", AngleMode.Deg, "3");
        Calc("melt out of range", "melt(mata(),3,1)", AngleMode.Deg, "Element position is out of range for this matrix.");
        Calc("mat 3x3 store", "mat(3,3,1,2,3,4,5,6,7,8,10)", AngleMode.Deg, "[[1 2 3][4 5 6][7 8 10]]");
        Calc("det C", "det(matc())", AngleMode.Deg, "-3");
        Calc("det 3x3 corner", "melt(matc(),3,3)", AngleMode.Deg, "10");
        Calc("minv identity 3x3", "minv(mat(3,3,1,0,0,0,1,0,0,0,1))", AngleMode.Deg, "[[1 0 0][0 1 0][0 0 1]]");
        Calc("dot row vectors", "dot(mat(1,3,1,2,3),mat(1,3,4,5,6))", AngleMode.Deg, "32");
        Calc("dot column vectors", "dot(mat(3,1,1,2,3),mat(3,1,4,5,6))", AngleMode.Deg, "32");
        Calc("cross", "cross(mat(1,3,1,2,3),mat(1,3,4,5,6))", AngleMode.Deg, "[[-3 6 -3]]");
        Calc("vmag 3-4-0", "vmag(mat(1,3,3,4,0))", AngleMode.Deg, "5");
        Calc("vmag sqrt2", "vmag(mat(1,2,1,1))", AngleMode.Deg, "1.414213562373095");
        Calc("minv singular", "minv(mat(2,2,1,2,2,4))", AngleMode.Deg, "Matrix is singular - no inverse.");
        Calc("det non square", "det(mat(2,3,1,2,3,4,5,6))", AngleMode.Deg, "det() needs a square matrix.");
        Calc("scalar add matrix", "2+mat(2,2,1,2,3,4)", AngleMode.Deg, "Matrix and scalar cannot be added.");
        Calc("mat add dim mismatch", "mat(2,2,1,2,3,4)+mat(2,3,1,2,3,4,5,6)", AngleMode.Deg, "Matrix dimensions do not match.");
        Calc("mat mul dim mismatch", "mat(2,2,1,2,3,4)*mat(3,3,1,0,0,0,1,0,0,0,1)", AngleMode.Deg, "Cannot multiply: columns of the left matrix must equal rows of the right.");
        Calc("dot length mismatch", "dot(mat(1,2,1,2),mat(1,3,1,2,3))", AngleMode.Deg, "dot() needs equal-length vectors.");
        Calc("cross length", "cross(mat(1,2,1,2),mat(1,2,3,4))", AngleMode.Deg, "cross() needs 3-element vectors.");
        Calc("dot needs vector shape", "dot(mat(2,2,1,2,3,4),mat(2,2,1,1,1,1))", AngleMode.Deg, "dot(), cross() and vmag() need vectors: one-row or one-column matrices.");
        Calc("mat missing values", "mat(2,2,1,2,3)", AngleMode.Deg, "mat() needs 4 values for a 2x2 matrix.");
        Calc("mat extra values", "mat(2,2,1,2,3,4,5)", AngleMode.Deg, "mat() needs 4 values for a 2x2 matrix.");
        Calc("mat rows zero", "mat(0,2,1)", AngleMode.Deg, "mat() needs rows and columns from 1 to 6.");
        Calc("sin matrix", "sin(mat(2,2,1,2,3,4))", AngleMode.Deg, "This operation does not support matrices.");
        Calc("mat scalar div", "mat(2,2,2,4,6,8)/2", AngleMode.Deg, "[[1 2][3 4]]");
        Calc("scalar over matrix", "2/mat(2,2,1,2,3,4)", AngleMode.Deg, "Cannot divide a scalar by a matrix.");
        Calc("matrix power", "mat(2,2,1,2,3,4)^2", AngleMode.Deg, "This operation does not support matrices.");
        Calc("sto matrix", "sto(A,mat(2,2,1,2,3,4))", AngleMode.Deg, "[[1 2][3 4]]");
        Calc("rcl matrix scaled", "2*rcl(A)", AngleMode.Deg, "[[2 4][6 8]]");
        Calc("det 1x1", "det(mat(1,1,5))", AngleMode.Deg, "5");
        Calc("1x1 mul", "mat(1,1,5)*mat(1,1,3)", AngleMode.Deg, "[[15]]");
        Calc("det irrational", "det(mat(2,2,1,pi,2,3))", AngleMode.Deg, "-3.283185307179586");
        Calc("minv irrational", "minv(mat(2,2,1,pi,2,3))", AngleMode.Deg, "[[-0.9137467792145866 0.9568733896072934][0.6091645194763911 -0.3045822597381956]]");
        Calc("matclr", "matclr()", AngleMode.Deg, "0");
        Calc("fill slot A", "mat(1,1,1)", AngleMode.Deg, "[[1]]");
        Calc("fill slot B", "mat(1,1,2)", AngleMode.Deg, "[[2]]");
        Calc("fill slot C", "mat(1,1,3)", AngleMode.Deg, "[[3]]");
        Calc("fill slot D", "mat(1,1,4)", AngleMode.Deg, "[[4]]");
        Calc("fill slot E", "mat(1,1,5)", AngleMode.Deg, "[[5]]");
        Calc("fill slot F", "mat(1,1,6)", AngleMode.Deg, "[[6]]");
        Calc("seventh matrix unstored", "mat(1,1,7)", AngleMode.Deg, "[[7]]");
        Calc("mata after fill", "mata()", AngleMode.Deg, "[[1]]");
        Calc("matf after fill", "matf()", AngleMode.Deg, "[[6]]");
        Calc("matclr clears all", "matclr()", AngleMode.Deg, "0");
        Calc("mata after clr", "mata()", AngleMode.Deg, "MatA is empty - store a matrix with mat() first.");

        // ---- calculus loops, second derivative, physical constants (iteration 15) ----
        Calc("sum 1..10", "sum(x,1,10)", AngleMode.Deg, "55");
        Calc("sum powers of 2", "sum(2^x,0,10)", AngleMode.Deg, "2047");
        Calc("prod factorial 10", "prod(x,1,10)", AngleMode.Deg, "3628800");
        Calc("sum squares", "sum(x^2,1,5)", AngleMode.Deg, "55");
        Calc("sum constant rational", "sum(1/2,1,3)", AngleMode.Deg, "1.5");
        Calc("prod const 2", "prod(2,x,1,4)", AngleMode.Deg, "16");
        Calc("sum bound to x", "sum(x,x,1,5)", AngleMode.Deg, "15");
        Calc("sum big range", "sum(x,0,10000)", AngleMode.Deg, "50005000");
        Calc("prod single term", "prod(x,1,1)", AngleMode.Deg, "1");
        Calc("prod factorial 5", "prod(x,1,5)", AngleMode.Deg, "120");
        Calc("harmonic sum", "sum(1/x,x,1,4)", AngleMode.Deg, "2.083333333333333");
        Calc("sum single term", "sum(x,0,0)", AngleMode.Deg, "0");
        Calc("sum of 1s", "sum(1,x,1,3)", AngleMode.Deg, "3");
        Calc("sum degree sine", "sum(sin(x),x,0,90)", AngleMode.Deg, "57.79432506465479");
        Calc("sum explicit x form", "sum(x^2,x,1,5)", AngleMode.Deg, "55");
        Calc("sum arity low", "sum(x,1)", AngleMode.Deg, "'sum' expects 3 argument(s), got 2.");
        Calc("sum reversed bounds", "sum(x,10,1)", AngleMode.Deg, "sum()/prod() lower bound must not exceed the upper bound.");
        Calc("sum range cap", "sum(x,1,20000)", AngleMode.Deg, "sum()/prod() ranges are limited to 10000 terms.");
        Calc("sum non-integer bound", "sum(x,2.5,5)", AngleMode.Deg, "Value is not an integer.");
        Calc("sum matrix bound", "sum(mat(2,2,1,2,3,4),1,3)", AngleMode.Deg, "Matrix and scalar cannot be added.");
        Calc("prod reversed bounds", "prod(x,1,0)", AngleMode.Deg, "sum()/prod() lower bound must not exceed the upper bound.");
        Calc("sum unknown ident", "sum(t^2,x,1,3)", AngleMode.Deg, "Unknown identifier 't'.");
        Calc("sum wrong var name", "sum(x,y,1,5)", AngleMode.Deg, "sum()/prod() need 'x' as their 2nd argument.");
        Calc("sum arity 5", "sum(x,x,1,5,7)", AngleMode.Deg, "'sum' expects 3 argument(s), got 5.");

        Calc("deriv2 quadratic", "deriv2(x^2,1)", AngleMode.Deg, "2.000000000464593");
        Calc("deriv2 cubic", "deriv2(x^3,2)", AngleMode.Deg, "11.99999999886477");
        Calc("deriv2 reciprocal", "deriv2(1/x,2)", AngleMode.Deg, "0.2499999998961666");
        Calc("deriv2 explicit x", "deriv2(x^2,x,3)", AngleMode.Deg, "1.999999999555855");
        Calc("deriv2 sine peak", "deriv2(sin(x),0)", AngleMode.Rad, "0");
        Calc("deriv2 sine 90 rad", "deriv2(sin(x),90)", AngleMode.Rad, "-0.8939966228752416");
        Calc("deriv2 pole", "deriv2(1/x,0)", AngleMode.Deg, "The second derivative does not exist at this point.");
        Calc("deriv2 sine DEG", "deriv2(sin(x),90)", AngleMode.Deg, "-0.0003046174197613043");

        Calc("speed of light", "speedoflight()", AngleMode.Deg, "299792458");
        Calc("planck", "planck()", AngleMode.Deg, "6.62607015E-34");
        Calc("elementary charge", "charge()", AngleMode.Deg, "0.0000000000000000001602176634");
        Calc("boltzmann", "boltzmann()", AngleMode.Deg, "1.380649E-23");
        Calc("avogadro", "avogadro()", AngleMode.Deg, "6.02214076E+23");
        Calc("gas constant", "gasconstant()", AngleMode.Deg, "8.31446261815324");
        Calc("standard gravity", "standardg()", AngleMode.Deg, "9.80665");
        Calc("atmosphere", "atmosphere()", AngleMode.Deg, "101325");
        Calc("gravitation", "gravitation()", AngleMode.Deg, "0.000000000066743");
        Calc("electron mass", "electronmass()", AngleMode.Deg, "9.1093837015E-31");
        Calc("proton mass", "protonmass()", AngleMode.Deg, "1.67262192369E-27");
        Calc("neutron mass", "neutronmass()", AngleMode.Deg, "1.67492749804E-27");
        Calc("planck times charge", "planck()*charge()", AngleMode.Deg, "1.061613476957488E-52");
        Calc("boltzmann times gas", "boltzmann()*gasconstant()", AngleMode.Deg, "1.147935449929065E-22");
        Calc("speedoflight arity", "speedoflight(1)", AngleMode.Deg, "'speedoflight' expects 0 argument(s), got 1.");
        Calc("2g", "2*standardg()", AngleMode.Deg, "19.6133");

        // ---- base conversions + number theory (iteration 16) ----
        Calc("hex 255", "hex(255)", AngleMode.Deg, "FF");
        Calc("hex 0", "hex(0)", AngleMode.Deg, "0");
        Calc("hex -255", "hex(-255)", AngleMode.Deg, "-FF");
        Calc("oct 15", "oct(15)", AngleMode.Deg, "17");
        Calc("bin 10", "bin(10)", AngleMode.Deg, "1010");
        Calc("bin 0", "bin(0)", AngleMode.Deg, "0");
        Calc("base 255 radix 36", "base(255,36)", AngleMode.Deg, "73");
        Calc("base 255 radix 10", "base(255,10)", AngleMode.Deg, "255");
        Calc("base 255 radix 2", "base(255,2)", AngleMode.Deg, "11111111");
        Calc("base 10 radix 2", "base(10,2)", AngleMode.Deg, "1010");
        Calc("base 31 radix 16", "base(31,16)", AngleMode.Deg, "1F");
        Calc("base 16 radix 16", "base(16,16)", AngleMode.Deg, "10");
        Calc("base big radix 16", "base(1000000000000000,16)", AngleMode.Deg, "38D7EA4C68000");
        Calc("hex of sqrt 4", "hex(sqrt(4))", AngleMode.Deg, "2");
        CalcError("hex extra arg", "hex(255,2)", AngleMode.Deg, "expects 1 argument");
        CalcError("base radix 1", "base(255,1)", AngleMode.Deg, "radix from 2 to 36");
        CalcError("base radix 37", "base(255,37)", AngleMode.Deg, "radix from 2 to 36");
        CalcError("base radix non-int", "base(255,2.5)", AngleMode.Deg, "not an integer");
        CalcError("base value non-int", "base(2.5,10)", AngleMode.Deg, "not an integer");
        CalcError("hex non-int", "hex(2.5)", AngleMode.Deg, "not an integer");
        CalcError("hex matrix", "hex(mat(2,2,1,2,3,4))", AngleMode.Deg, "not an integer");
        CalcError("hex in arithmetic", "hex(255)+1", AngleMode.Deg, "not an integer");
        CalcError("hex left add", "1+hex(255)", AngleMode.Deg, "not an integer");
        CalcError("hex of bin text", "hex(bin(10))", AngleMode.Deg, "not an integer");
        Calc("sto base string", "sto(a,hex(255))", AngleMode.Deg, "FF");
        Calc("rcl base string", "rcl(a)", AngleMode.Deg, "FF");

        Calc("fact 5", "fact(5)", AngleMode.Deg, "120");
        Calc("fact 0", "fact(0)", AngleMode.Deg, "1");
        Calc("fact 1", "fact(1)", AngleMode.Deg, "1");
        Calc("fact 20", "fact(20)", AngleMode.Deg, "2432902008176640000");
        Calc("fact 21", "fact(21)", AngleMode.Deg, "51090942171709440000");
        Calc("fact 10000", "fact(10000)", AngleMode.Deg, "2.846259680917055E+35659");
        Calc("fact plus", "fact(5)+1", AngleMode.Deg, "121");
        Calc("postfix bang matches fact", "5!", AngleMode.Deg, "120");
        CalcError("fact negative", "fact(-1)", AngleMode.Deg, "non-negative");
        CalcError("fact non-int", "fact(2.5)", AngleMode.Deg, "not an integer");
        CalcError("fact over cap", "fact(10001)", AngleMode.Deg, "limited to 10000");

        Calc("isprime 2", "isprime(2)", AngleMode.Deg, "1");
        Calc("isprime 1", "isprime(1)", AngleMode.Deg, "0");
        Calc("isprime 97", "isprime(97)", AngleMode.Deg, "1");
        Calc("isprime 100", "isprime(100)", AngleMode.Deg, "0");
        Calc("isprime -4", "isprime(-4)", AngleMode.Deg, "0");
        Calc("isprime big prime", "isprime(999999999999989)", AngleMode.Deg, "1");
        Calc("isprime 1e15", "isprime(1000000000000000)", AngleMode.Deg, "0");
        CalcError("isprime over cap", "isprime(1000000000000001)", AngleMode.Deg, "up to 1000000000000000");

        Calc("factors 12", "factors(12)", AngleMode.Deg, "3, 2, 2");
        Calc("factors prime 97", "factors(97)", AngleMode.Deg, "97");
        Calc("factors 2", "factors(2)", AngleMode.Deg, "2");
        Calc("factors 999999999999", "factors(999999999999)", AngleMode.Deg, "9901, 101, 37, 13, 11, 7, 3, 3, 3");
        CalcError("factors 1", "factors(1)", AngleMode.Deg, "from 2 to 1000000000000000");
        CalcError("factors 0", "factors(0)", AngleMode.Deg, "from 2 to 1000000000000000");

        Calc("divisors 12", "divisors(12)", AngleMode.Deg, "12, 6, 4, 3, 2, 1");
        Calc("divisors 1", "divisors(1)", AngleMode.Deg, "1");
        Calc("divisors 36", "divisors(36)", AngleMode.Deg, "36, 18, 12, 9, 6, 4, 3, 2, 1");
        CalcError("divisors 0", "divisors(0)", AngleMode.Deg, "from 1 to 1000000000000000");

        Calc("phi 12", "phi(12)", AngleMode.Deg, "4");
        Calc("phi 1", "phi(1)", AngleMode.Deg, "1");
        Calc("phi 97", "phi(97)", AngleMode.Deg, "96");
        Calc("phi 36", "phi(36)", AngleMode.Deg, "12");
        Calc("phi 1e12", "phi(1000000000000)", AngleMode.Deg, "400000000000");
        CalcError("phi 0", "phi(0)", AngleMode.Deg, "from 1 to 1000000000000000");

        // ---- iteration 17: MathPrint (multi-line textbook display) ----

        MP("mp plain", "2+3*4", "2 + 3·4");
        MP("mp fraction", "2/3", "2\n─\n3");
        MP("mp frac sum", "1/2+1/3", "1   1\n─   ─\n2 + 3");
        MP("mp exp", "2^3", "2³");
        MP("mp exp 10^21", "10^21", "10²¹");
        MP("mp sqrt", "sqrt(2)", "√‾\n 2");
        MP("mp exp x", "2^x", "2ˣ");
        MP("mp factorial", "5!", "5!");
        MP("mp unary exp", "-3^2", "-3²");
        MP("mp exp expr", "2^(x+1)", " x + 1\n2");
        MP("mp implicit mul", "2pi", "2·pi");
        MP("mp mod", "3 mod 4", "3 mod 4");
        MP("mp call", "sin(30)", "sin(30)");
        MP("mp big frac", "(1+2)/(3+4)", "1 + 2\n─────\n3 + 4");
        MP("mp frac wide", "12/6", "12\n──\n6");
        MP("mp squares", "x^2+y^2", "x² + y²");
        MP("mp abs", "abs(-3)", "|-3|");
        MP("mp pi half", "pi/2", "pi\n──\n2");
        MP("mp mixed", "2 3/4", "2 3/4");
        MP("mp frac product", "1/3*1/5", "1\n─\n3·1\n───\n 5");
        MP("mp exp tower", "2^2^3", " 2³\n2");
        MP("mp sqrt sum", "sqrt(2)+sqrt(3)", "√‾   √‾\n 2 +  3");
        MP("mp nested frac", "(2+3)/(1/2)", "2 + 3\n─────\n  1\n  ─\n  2");
        MP("mp integrate", "integrate(x^2,0,10)", "integrate(x², 0, 10)");
        MP("mp neg sqrt", "-sqrt(81)", " √‾‾\n- 81");
        MP("mp paren mul", "3*(2+1)", "3·(2 + 1)");
        MP("mp juxtapose", "2(3)", "2·3");
        MP("mp percent", "100%", "100%");
        MP("mp ans", "ANS", "ANS");
        MP("mp sqrt squared", "sqrt(2)^2", "(√‾²\n 2)");
        MP("mp syntax raw", "2 3", "2 3");
        MP("mp empty", "", "");
        MP("mp div zero raw", "1/0", "1\n─\n0");

        // ---- summary ----
        Console.WriteLine("RESULTS: " + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
