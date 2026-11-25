namespace NexCalc.Core
{
    // AST node. kind: number | ident | call | infix | prefix | postfix.
    // number nodes carry a fully-parsed exact Rat in `value`.
    public class ANode {
        public string kind;
        public string text;
        public ANode[] kids;
        public Rat? value;

        public ANode(string kind_, string text_) {
            kind = kind_;
            text = text_;
            kids = new ANode[0];
            value = null;
        }

        public static ANode Number(string text) {
            ANode n = new ANode("number", text);
            n.value = Rat.FromAuto(text);
            return n;
        }

        public static ANode Ident(string text) {
            return new ANode("ident", text);
        }

        public static ANode Call(string name, ANode[] args) {
            ANode n = new ANode("call", name);
            n.kids = args;
            return n;
        }

        public static ANode Infix(string op, ANode left, ANode right) {
            ANode n = new ANode("infix", op);
            n.kids = new ANode[2];
            n.kids[0] = left;
            n.kids[1] = right;
            return n;
        }

        public static ANode Prefix(string op, ANode operand) {
            ANode n = new ANode("prefix", op);
            n.kids = new ANode[1];
            n.kids[0] = operand;
            return n;
        }

        public static ANode Postfix(string op, ANode operand) {
            ANode n = new ANode("postfix", op);
            n.kids = new ANode[1];
            n.kids[0] = operand;
            return n;
        }
    }

    public class Parser {
        Token[] toks = new Token[0];
        public int index;

        // Windows Calculator rule: a unary sign binds to the primary it
        // precedes, and a following '^' applies to the signed value, so
        // "-3^2" evaluates to (-3)^2 = 9, not -(3^2).
        public static ANode Parse(string text) {
            Parser p = new Parser();
            p.toks = Lexer.Tokenize(text);
            p.index = 0;
            if (p.toks.Length == 0) {
                throw ErrorType.EmptyExpression();
            }
            ANode node = p.ParseExpr();
            if (p.index < p.toks.Length) {
                Token t = p.toks[p.index];
                if (t.kind == "rparen") {
                    throw ErrorType.UnexpectedClose();
                }
                throw ErrorType.SyntaxError("Unexpected token '" + t.text + "' - missing operator.");
            }
            return node;
        }

        public ANode ParseExpr() {
            ANode left = ParseTerm();
            while (true) {
                Token? t = Peek();
                if (t == null) break;
                if (t.kind == "op" && (t.text == "+" || t.text == "-")) {
                    Consume();
                    ANode right = ParseTerm();
                    left = ANode.Infix(t.text, left, right);
                } else {
                    break;
                }
            }
            return left;
        }

        public ANode ParseTerm() {
            ANode left = ParseFactor();
            while (true) {
                Token? t = Peek();
                if (t == null) break;
                string? op = null;
                bool consumeToken = false;
                if (t.kind == "op" && (t.text == "*" || t.text == "/")) {
                    op = t.text;
                    consumeToken = true;
                } else if (t.kind == "ident" &&
                    (t.text == "mod" || t.text == "and" || t.text == "or" ||
                     t.text == "xor" || t.text == "shl" || t.text == "shr")) {
                    op = t.text;
                    consumeToken = true;
                } else if (t.kind == "lparen" || t.kind == "ident") {
                    // juxtaposition: "2(3)" and "2pi" imply multiply
                    op = "*";
                    consumeToken = false;
                }
                if (op == null) break;
                if (consumeToken) Consume();
                ANode right = ParseFactor();
                left = ANode.Infix(op, left, right);
            }
            return left;
        }

        // '^' is right-associative and applies to the whole signed atom.
        public ANode ParseFactor() {
            ANode atom = ParseSignedAtom();
            Token? t = Peek();
            if (t != null && t.kind == "op" && t.text == "^") {
                Consume();
                ANode right = ParseFactor();
                return ANode.Infix("^", atom, right);
            }
            return atom;
        }

        public ANode ParseSignedAtom() {
            Token? t = Peek();
            if (t != null && t.kind == "op" && (t.text == "-" || t.text == "+" || t.text == "~")) {
                Consume();
                ANode operand = ParseSignedAtom();
                return ANode.Prefix(t.text, operand);
            }
            return ParseAtomWithPostfix();
        }

        public ANode ParseAtomWithPostfix() {
            ANode a = ParseAtom();
            while (true) {
                Token? t = Peek();
                if (t != null && t.kind == "op" && (t.text == "!" || t.text == "%")) {
                    Consume();
                    a = ANode.Postfix(t.text, a);
                } else {
                    break;
                }
            }
            return a;
        }

        public ANode ParseAtom() {
            Token? t = Peek();
            if (t == null) {
                throw ErrorType.Create("MissingValue", "Expression ended too early: value is missing.");
            }
            if (t.kind == "number") {
                Consume();
                // Mixed number literal: "2 3/4" = 2 + 3/4. Requires a number
                // token, an explicit '/' and one more number immediately
                // after the whole part. "2 3" stays a juxtaposition error.
                Token? n2 = Peek();
                if (n2 != null && n2.kind == "number" &&
                    index + 2 < toks.Length &&
                    toks[index + 1].kind == "op" && toks[index + 1].text == "/" &&
                    toks[index + 2].kind == "number") {
                    Rat whole = Rat.FromAuto(t.text);
                    Rat numer = Rat.FromAuto(n2.text);
                    Rat denom = Rat.FromAuto(toks[index + 2].text);
                    Rat total = whole.Add(numer.Div(denom));
                    ANode mixed = new ANode("number",
                        t.text + " " + n2.text + "/" + toks[index + 2].text);
                    mixed.value = total;
                    index = index + 3; // consume fraction part, '/', denominator
                    return mixed;
                }
                return ANode.Number(t.text);
            }
            if (t.kind == "lparen") {
                Consume();
                ANode e = ParseExpr();
                Token? close = Peek();
                if (close == null || close.kind != "rparen") {
                    throw ErrorType.ParenMismatch();
                }
                Consume();
                return e;
            }
            if (t.kind == "ident") {
                Consume();
                Token? nxt = Peek();
                if (nxt != null && nxt.kind == "lparen") {
                    Consume();
                    ANode[] args = new ANode[8];
                    int argc = 0;
                    Token? first = Peek();
                    if (first != null && first.kind != "rparen") {
                        while (true) {
                            if (argc >= args.Length) {
                                ANode[] bigger = new ANode[args.Length * 2];
                                for (int k = 0; k < args.Length; k = k + 1) bigger[k] = args[k];
                                args = bigger;
                            }
                            args[argc] = ParseExpr();
                            argc = argc + 1;
                            Token? sep = Peek();
                            if (sep != null && sep.kind == "comma") {
                                Consume();
                                continue;
                            }
                            break;
                        }
                    }
                    Token? close = Peek();
                    if (close == null || close.kind != "rparen") {
                        throw ErrorType.ParenMismatch();
                    }
                    Consume();
                    ANode[] trimmed = new ANode[argc];
                    for (int k = 0; k < argc; k = k + 1) trimmed[k] = args[k];
                    return ANode.Call(t.text, trimmed);
                }
                return ANode.Ident(t.text);
            }
            throw ErrorType.Create("MissingValue", "Unexpected operator '" + t.text + "': value is missing.");
        }

        public void Consume() {
            index = index + 1;
        }

        public Token? Peek() {
            if (index < toks.Length) return toks[index];
            return null;
        }
    }
}
