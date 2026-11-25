namespace NexCalc.Core
{
    // Public calculation entry point shared by the UI, the graph engine,
    // and the regression tests. A calculation either yields a Val or a
    // typed error - never an exception crossing the API boundary.
    public class Result {
        public bool ok;
        public Val? value;
        public string? message;   // set when ok == false
        public string? kind;      // machine-readable error kind

        public Result(bool ok_, Val? value_, string? message_, string? kind_) {
            ok = ok_;
            value = value_;
            message = message_;
            kind = kind_;
        }
    }

    public class Expression {
        // Evaluate one expression text under an angle convention.
        public static Result Calculate(string text, AngleMode mode) {
            return Calculate(text, mode, null);
        }

        public static Result Calculate(string text, AngleMode mode, Val? ans) {
            try {
                ANode n = Parser.Parse(text);
                Val v = Eval.Evaluate(n, mode, null, ans);
                return new Result(true, v, null, null);
            } catch (ErrorType e) {
                return new Result(false, null, e.userMsg, e.kind);
            }
        }

        public static Result CalculateWithX(string text, AngleMode mode, Val xValue) {
            try {
                ANode n = Parser.Parse(text);
                Val v = Eval.Evaluate(n, mode, xValue);
                return new Result(true, v, null, null);
            } catch (ErrorType e) {
                return new Result(false, null, e.userMsg, e.kind);
            }
        }

        // Convenience: formatted display text for the tape/history panel.
        public static string? DisplayText(string text, AngleMode mode) {
            Result r = Calculate(text, mode);
            if (r.ok && r.value != null) return Format.Display(r.value);
            return r.message;
        }
    }
}
