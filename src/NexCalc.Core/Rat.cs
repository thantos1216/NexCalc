using System.Numerics;

namespace NexCalc.Core
{
    // Exact rational arithmetic over BigInteger. The engine keeps results
    // exact (numerator/denominator) until a transcendental function forces
    // an approximation - the same layering as the Windows Calculator engine.
    // Invariant: den > 0 and gcd(num, den) == 1.
    public class Rat {
        public BigInteger num;   // numerator
        public BigInteger den;   // denominator, always positive

        public Rat(BigInteger n_, BigInteger d_) {
            num = n_;
            den = d_;
        }

        public static BigInteger one() { return new BigInteger(1); }

        public static Rat Make(BigInteger n_, BigInteger d_) {
            if (d_ == BigInteger.Zero) {
                throw ErrorType.ZeroDivision();
            }
            if (d_ < BigInteger.Zero) {
                n_ = -n_;
                d_ = -d_;
            }
            BigInteger g = Gcd(n_, d_);
            if (!g.Equals(one())) {
                n_ = n_ / g;
                d_ = d_ / g;
            }
            return new Rat(n_, d_);
        }

        public static Rat OfInt(long v) {
            return Make(new BigInteger(v), one());
        }

        public static BigInteger Gcd(BigInteger a, BigInteger b) {
            if (a < BigInteger.Zero) a = -a;
            if (b < BigInteger.Zero) b = -b;
            while (b != BigInteger.Zero) {
                BigInteger t = a % b;
                a = b;
                b = t;
            }
            return a;
        }

        public bool IsZero() { return num == BigInteger.Zero; }
        public bool IsNegative() { return num < BigInteger.Zero; }
        public bool IsInteger() { return den.Equals(one()); }
        public int Signum() { return num < BigInteger.Zero ? -1 : (num == BigInteger.Zero ? 0 : 1); }

        public double ToDoubleValue() {
            return double.Parse(num.ToString()) / double.Parse(den.ToString());
        }

        public Rat Neg() {
            return new Rat(-num, den);
        }

        public Rat Add(Rat o) { return Make(num * o.den + o.num * den, den * o.den); }
        public Rat Sub(Rat o) { return Make(num * o.den - o.num * den, den * o.den); }
        public Rat Mul(Rat o) { return Make(num * o.num, den * o.den); }
        public Rat Div(Rat o) {
            if (o.num == BigInteger.Zero) {
                throw ErrorType.ZeroDivision();
            }
            return Make(num * o.den, den * o.num);
        }
        public Rat Inv() {
            if (num == BigInteger.Zero) {
                throw ErrorType.ZeroDivision();
            }
            return Make(den, num);
        }

        // Integer exponent by binary powering; the value stays exact.
        public Rat PowInt(long e) {
            if (e < 0) {
                return Inv().PowInt(-e);
            }
            BigInteger resN = one();
            BigInteger resD = one();
            BigInteger baseN = num;
            BigInteger baseD = den;
            long k = e;
            while (k > 0) {
                if ((k & 1L) == 1L) {
                    resN = resN * baseN;
                    resD = resD * baseD;
                }
                baseN = baseN * baseN;
                baseD = baseD * baseD;
                k = k >> 1;
            }
            return new Rat(resN, resD);
        }

        public static BigInteger PowBig(BigInteger base_, long e_) {
            BigInteger r = one();
            BigInteger b = base_;
            long k = e_;
            while (k > 0) {
                if ((k & 1L) == 1L) r = r * b;
                b = b * b;
                k = k >> 1;
            }
            return r;
        }

        // Parse a numeric literal: optional sign, decimal point, base-10 'e'
        // exponent, and 0x / 0b / 0o prefixed integers.
        public static Rat FromAuto(string text) {
            int radix = 10;
            string body = text;
            bool negative = false;
            if (body.Length > 0 && (body[0] == '-' || body[0] == '+')) {
                negative = body[0] == '-';
                body = body.Substring(1);
            }
            if (body.Length > 2 && body[0] == '0') {
                char p = body[1];
                if (p == 'x' || p == 'X') { radix = 16; body = body.Substring(2); }
                else if (p == 'b' || p == 'B') { radix = 2; body = body.Substring(2); }
                else if (p == 'o' || p == 'O') { radix = 8; body = body.Substring(2); }
            }
            BigInteger n_ = BigInteger.Zero;
            BigInteger d_ = one();
            bool seenDot = false;
            long fracLen = 0;
            for (int i = 0; i < body.Length; i = i + 1) {
                char c = body[i];
                if (c == '.') {
                    if (seenDot) {
                        throw ErrorType.SyntaxError("Number has two decimal points: '" + text + "'.");
                    }
                    seenDot = true;
                    continue;
                }
                if (c == 'e' || c == 'E') {
                    if (radix == 10 && i + 1 < body.Length) {
                        int j = i + 1;
                        if (body[j] == '+' || body[j] == '-') j = j + 1;
                        if (j < body.Length && body[j] >= '0' && body[j] <= '9') {
                            long exp = long.Parse(body.Substring(j));
                            BigInteger scale = PowBig(new BigInteger(10), exp < 0 ? -exp : exp);
                            if (exp >= 0) n_ = n_ * scale;
                            else d_ = d_ * scale;
                            return Make(negative ? -n_ : n_, d_);
                        }
                    }
                    throw ErrorType.SyntaxError("Invalid number: '" + text + "'.");
                }
                int dig = DigitValue(c);
                if (dig < 0 || dig >= radix) {
                    throw ErrorType.SyntaxError("Invalid digit '" + c + "' in number: '" + text + "'.");
                }
                n_ = n_ * new BigInteger(radix) + new BigInteger(dig);
                if (seenDot) fracLen = fracLen + 1;
            }
            if (fracLen > 0) {
                d_ = PowBig(new BigInteger(radix), fracLen);
            }
            return Make(negative ? -n_ : n_, d_);
        }

        public static int DigitValue(char c) {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        // True when den = 2^a * 5^b, i.e. the exact decimal expansion is finite.
        public bool CleanDecimal() {
            BigInteger t = den;
            BigInteger two = new BigInteger(2);
            while ((t % two) == BigInteger.Zero) t = t / two;
            BigInteger five = new BigInteger(5);
            while (t != BigInteger.Zero && (t % five) == BigInteger.Zero) t = t / five;
            return t.Equals(one());
        }

        // Exact k-th root when num and den are both perfect k-th powers.
        public static BigInteger? IntegerRoot(BigInteger v, int k) {
            if (v < BigInteger.Zero) return null;
            if (v == BigInteger.Zero) return BigInteger.Zero;
            if (k == 1) return v;
            BigInteger lo = one();
            BigInteger hi = new BigInteger(10);
            while (PowBig(hi, k) < v) {
                lo = hi;
                hi = hi * new BigInteger(10);
            }
            while (true) {
                BigInteger mid = (lo + hi) / new BigInteger(2);
                BigInteger p = PowBig(mid, k);
                int cmp = (p < v) ? -1 : (p > v ? 1 : 0);
                if (cmp == 0) return mid;
                if (cmp < 0) lo = mid + one();
                else hi = mid - one();
                if (lo > hi) return null;
            }
        }

        public Rat? ExactRoot(int k) {
            if (k <= 0) return null;
            BigInteger? rN = IntegerRoot(num, k);
            if (rN == null) return null;
            BigInteger? rD = IntegerRoot(den, k);
            if (rD == null) return null;
            if (num < BigInteger.Zero && (k % 2) == 1) {
                return new Rat(-rN.Value, rD.Value);
            }
            return new Rat(rN.Value, rD.Value);
        }

        // Floor of the rational as an integer (toward negative infinity).
        public static Rat FloorRat(Rat r) {
            BigInteger q = r.num / r.den;
            BigInteger rem = r.num % r.den;
            if (rem < BigInteger.Zero) q = q - one();
            return new Rat(q, one());
        }
    }
}
