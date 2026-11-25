namespace NexCalc.Core
{
    // Token stream element. kind: number | ident | op | lparen | rparen | comma
    public class Token {
        public string kind;
        public string text;

        public Token(string kind_, string text_) {
            kind = kind_;
            text = text_;
        }
    }

    public class Lexer {
        // Tokenize an expression. Token count is bounded by the character
        // length, so a plain array avoids any dynamic collection here.
        public static Token[] Tokenize(string text) {
            int len = text.Length;
            Token[] buf = new Token[len + 1];
            int count = 0;
            int i = 0;
            while (i < len) {
                char c = text[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') {
                    i = i + 1;
                    continue;
                }
                if ((c >= '0' && c <= '9') ||
                    (c == '.' && i + 1 < len && text[i + 1] >= '0' && text[i + 1] <= '9')) {
                    int start = i;
                    if (c == '0' && i + 1 < len &&
                        (text[i + 1] == 'x' || text[i + 1] == 'X' ||
                         text[i + 1] == 'b' || text[i + 1] == 'B' ||
                         text[i + 1] == 'o' || text[i + 1] == 'O')) {
                        char p = text[i + 1];
                        int radix = (p == 'x' || p == 'X') ? 16 : ((p == 'b' || p == 'B') ? 2 : 8);
                        i = i + 2;
                        int digitsStart = i;
                        while (i < len && DigitValue(text[i]) >= 0 && DigitValue(text[i]) < radix) {
                            i = i + 1;
                        }
                        if (i == digitsStart) {
                            throw ErrorType.SyntaxError("Missing digits after base prefix '" + text.Substring(start, i - start) + "'.");
                        }
                    } else {
                        int dots = 0;
                        while (i < len) {
                            char d = text[i];
                            if (d >= '0' && d <= '9') {
                                i = i + 1;
                            } else if (d == '.' && dots == 0) {
                                dots = dots + 1;
                                i = i + 1;
                            } else if ((d == 'e' || d == 'E') && i + 1 < len) {
                                int j = i + 1;
                                if (text[j] == '+' || text[j] == '-') j = j + 1;
                                if (j < len && text[j] >= '0' && text[j] <= '9') {
                                    i = j;
                                    while (i < len && text[i] >= '0' && text[i] <= '9') i = i + 1;
                                    break;
                                } else {
                                    break;
                                }
                            } else {
                                break;
                            }
                        }
                    }
                    buf[count] = new Token("number", text.Substring(start, i - start));
                    count = count + 1;
                    continue;
                }
                if (IsIdentStart(c)) {
                    int start = i;
                    while (i < len && IsIdentPart(text[i])) {
                        i = i + 1;
                    }
                    buf[count] = new Token("ident", text.Substring(start, i - start));
                    count = count + 1;
                    continue;
                }
                if (c == '×' || c == '·' || c == '*') {
                    buf[count] = new Token("op", "*");
                    count = count + 1;
                    i = i + 1;
                    continue;
                }
                if (c == '÷' || c == '/') {
                    buf[count] = new Token("op", "/");
                    count = count + 1;
                    i = i + 1;
                    continue;
                }
                if (c == '+' || c == '-' || c == '−' || c == '–' || c == '^' ||
                    c == '%' || c == '!' || c == '~' || c == '¬') {
                    string s = "" + c;
                    if (c == '−' || c == '–') s = "-";
                    if (c == '¬') s = "~";
                    buf[count] = new Token("op", s);
                    count = count + 1;
                    i = i + 1;
                    continue;
                }
                if (c == '(') {
                    buf[count] = new Token("lparen", "(");
                    count = count + 1;
                    i = i + 1;
                    continue;
                }
                if (c == ')') {
                    buf[count] = new Token("rparen", ")");
                    count = count + 1;
                    i = i + 1;
                    continue;
                }
                if (c == ',') {
                    buf[count] = new Token("comma", ",");
                    count = count + 1;
                    i = i + 1;
                    continue;
                }
                throw ErrorType.SyntaxError("Unexpected character '" + c + "'.");
            }
            Token[] res = new Token[count];
            for (int k = 0; k < count; k = k + 1) res[k] = buf[k];
            return res;
        }

        public static int DigitValue(char c) {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        // Identifier start: ASCII letters, '_', and the Greek symbols users
        // type for pi/theta (π, θ) so no locale-dependent char API is needed.
        public static bool IsIdentStart(char c) {
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c == '_') return true;
            if (c == 'π' || c == 'θ' || c == 'Π' || c == 'Θ') return true;
            return false;
        }

        public static bool IsIdentPart(char c) {
            if (IsIdentStart(c)) return true;
            if (c >= '0' && c <= '9') return true;
            return false;
        }
    }
}
