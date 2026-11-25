using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace NexCalc.Core;

// Programmer (Base-N) mode: bare digit/letter tokens are interpreted in the
// active radix, results must be integers, and values are wrapped into a
// signed word width with per-base display strings.
public static class Programmer {

    // Active radix: 10 (DEC), 16 (HEX), 8 (OCT), 2 (BIN).
    // bits is the word width (8, 16, 32, 64).
    // Returns { signed decimal, hex, octal, binary } display strings.
    public static string[] Run(string expr, int radix, int bits) {
        string pre = Preprocess(expr, radix, bits);
        Result r = Expression.Calculate(pre, AngleMode.Rad);
        if (!r.ok || r.value == null) {
            throw ErrorType.Create("CalcError", r.message ?? "Calculation failed.");
        }
        Val v = r.value!;
        if (!v.exact || v.r == null) {
            throw ErrorType.Create("NotAnInteger",
                "The result is not an integer - Programmer mode needs integer values.");
        }
        Rat rr = v.r!;
        if (rr.den != BigInteger.One) {
            throw ErrorType.Create("NotAnInteger",
                "The result is not an integer - Programmer mode needs integer values.");
        }
        BigInteger w = SignedValue(rr.num, bits);
        return new string[] {
            w.ToString(),
            Display(w, 16, bits),
            Display(w, 8, bits),
            Display(w, 2, bits),
        };
    }

    // When the active radix changes, convert the current value: evaluate the
    // expression in the old radix, then render the resulting value as digits
    // in the new radix (Windows programmer-mode conversion semantics). The
    // digit count must fit the target base's limit for the word width.
    public static string ConvertBase(string expr, int fromRadix, int toRadix, int bits) {
        string[] r = Run(expr, fromRadix, bits);
        BigInteger v = BigInteger.Parse(r[0]);
        string d = Format.ToBase(v, toRadix, 0);
        string digitsOnly = v < BigInteger.Zero ? d.Substring(1) : d;
        int max;
        if (toRadix == 10) {
            max = (Rat.PowBig(new BigInteger(2), bits) - 1).ToString().Length;
        } else {
            max = MaxDigits(toRadix, bits);
        }
        if (digitsOnly.Length > max) {
            throw ErrorType.Create("RangeError",
                "Number '" + digitsOnly + "' digits exceeds " + ModeName(toRadix) + " limit of " +
                max + " digits for a " + bits + "-bit word.");
        }
        return d;
    }

    // Converts digit/letter runs that are not engine literals (0x/0b/0o,
    // function names, constants) into literals for the active radix.
    // radix 10 leaves plain numbers untouched and rejects stray letters.
    public static string Preprocess(string expr, int radix, int bits) {
        var sb = new StringBuilder();
        int i = 0;
        int len = expr.Length;
        while (i < len) {
            char c = expr[i];
            if (char.IsLetter(c) || char.IsDigit(c)) {
                int j = i;
                while (j < len && (char.IsLetterOrDigit(expr[j]) || expr[j] == '_')) { j = j + 1; }
                string tok = expr.Substring(i, j - i);
                if (tok.Length > 2 && tok[0] == '0' &&
                    (tok[1] == 'x' || tok[1] == 'X' || tok[1] == 'b' || tok[1] == 'B' || tok[1] == 'o' || tok[1] == 'O')) {
                    // engine literal, copy verbatim
                    sb.Append(tok);
                    i = j;
                    continue;
                }
                if (AllDigits(tok)) {
                    if (radix == 10) {
                        sb.Append(tok);
                    } else {
                        CheckDigits(tok, radix);
                        CheckLength(tok.Length, radix, bits);
                        sb.Append(PrefixOf(radix) + tok);
                    }
                    i = j;
                    continue;
                }
                // token contains letters
                if (tok == "e") { sb.Append(tok); i = j; continue; }   // Euler constant
                if (AllHexChars(tok)) {
                    if (radix != 16) {
                        throw ErrorType.Create("InvalidDigit",
                            "Letter '" + tok + "' is not valid in " + ModeName(radix) + " mode.");
                    }
                    CheckLength(tok.Length, 16, bits);
                    sb.Append("0x" + tok);
                    i = j;
                    continue;
                }
                // function names / identifiers (abs, sin, pi, tau, ans...)
                sb.Append(tok);
                i = j;
                continue;
            }
            sb.Append(c);
            i = i + 1;
        }
        return sb.ToString();
    }

    // A value is accepted when it is representable as a word of the given
    // width (0..width-1 as unsigned, -(width-1)..-1 as signed). The returned
    // value is the signed two's-complement interpretation of that word.
    public static BigInteger SignedValue(BigInteger v, int bits) {
        BigInteger width = Rat.PowBig(new BigInteger(2), bits);
        BigInteger hi = width - 1;
        BigInteger lo = -(width - 1);
        if (v > hi || v < lo) {
            throw ErrorType.Create("RangeError",
                "Result " + v + " is not representable in a " + bits + "-bit word (max " + hi + ").");
        }
        BigInteger u = v % width;
        if (u < BigInteger.Zero) { u = u + width; }
        if (u >= width / 2) { u = u - width; }
        return u;
    }

    // Unsigned word representation, zero-padded to the radix digit width.
    public static string Display(BigInteger v, int radix, int bits) {
        BigInteger width = Rat.PowBig(new BigInteger(2), bits);
        BigInteger u = v % width;
        if (u < BigInteger.Zero) { u = u + width; }
        string s = Format.ToBase(u, radix, 0);
        int digits = MaxDigits(radix, bits);
        while (s.Length < digits) { s = "0" + s; }
        return s;
    }

    public static int MaxDigits(int radix, int bits) {
        if (radix == 16) return bits / 4;
        if (radix == 8) return (bits + 2) / 3;
        return bits;
    }

    static string PrefixOf(int radix) {
        if (radix == 16) return "0x";
        if (radix == 8) return "0o";
        return "0b";
    }

    static string ModeName(int radix) {
        if (radix == 10) return "DEC";
        if (radix == 16) return "HEX";
        if (radix == 8) return "OCT";
        return "BIN";
    }

    static bool AllDigits(string s) {
        foreach (char c in s) {
            if (c < '0' || c > '9') return false;
        }
        return s.Length > 0;
    }

    static bool AllHexChars(string s) {
        foreach (char c in s) {
            bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!ok) return false;
        }
        return s.Length > 0;
    }

    static void CheckDigits(string tok, int radix) {
        foreach (char c in tok) {
            int d = DigitValue(c);
            if (d >= radix) {
                throw ErrorType.Create("InvalidDigit",
                    "Digit '" + c + "' is not valid in " + ModeName(radix) + " mode.");
            }
        }
    }

    static void CheckLength(int n, int radix, int bits) {
        int max = MaxDigits(radix, bits);
        if (n > max) {
            throw ErrorType.Create("RangeError",
                "Number '" + n + "' digits exceeds " + ModeName(radix) + " limit of " + max +
                " digits for a " + bits + "-bit word.");
        }
    }

    static int DigitValue(char c) {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }
}
