namespace NexCalc.Core
{
    // A calculated value: either exact (rational) or an approximation
    // (double). Every intermediate result carries its own precision marker,
    // as the Windows Calculator does, so display formatting can stay honest.
    public class Val {
        public bool exact;
        public Rat? r;
        public double approx;
        public bool complex;
        public double re, im;
        public Val[]? roots;
        // Matrix payload: rows of scalar values (a matrix is never nested
        // inside a matrix). Built via Val.OfMatrix.
        public Val[][]? mat;
        // Digit-string result (base conversions). Displayed verbatim;
        // never fed back into arithmetic.
        public string? text;

        private Val(bool exact_, Rat? r_, double approx_) {
            exact = exact_;
            r = r_;
            approx = approx_;
        }

        // A digit-string result (hex()/oct()/bin()/base()).
        public static Val OfText(string s) {
            Val v = new Val(false, null, 0.0);
            v.text = s;
            return v;
        }

        // A multi-root result (poly2/poly3/poly4). Displayed as a
        // sorted list; it is never fed back into arithmetic.
        public static Val OfRoots(Val[] list) {
            Val v = new Val(false, null, 0.0);
            v.roots = list;
            return v;
        }

        // A matrix result (rows x columns of scalar values).
        public static Val OfMatrix(Val[][] rows) {
            Val v = new Val(false, null, 0.0);
            v.mat = rows;
            return v;
        }

        public static Val OfRat(Rat v) {
            return new Val(true, v, 0.0);
        }

        public static Val OfDouble(double v) {
            return new Val(false, null, v);
        }

        public static Val OfInt(long v) {
            return OfRat(Rat.OfInt(v));
        }

        public static Val OfComplex(double re_, double im_) {
            Val v = new Val(false, null, 0.0);
            v.complex = true;
            v.re = re_;
            v.im = im_;
            return v;
        }

        public double ToDoubleValue() {
            return exact ? r!.ToDoubleValue() : approx;
        }
    }
}
