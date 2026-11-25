namespace NexCalc.Core;

public static class ScientificKeys
{
    public static IReadOnlyDictionary<string, string> Alternates { get; } = new Dictionary<string, string>
    {
        ["sin"] = "sinh", ["cos"] = "cosh", ["tan"] = "tanh",
        ["log"] = "logb", ["exp"] = "EXP", ["^"] = "sqrt",
        ["x²"] = "x³", ["d/c"] = "dms", ["pol"] = "rec", ["X"] = "Y",
        ["conj"] = "arg", ["i"] = "cmplx", ["π"] = "e", ["A"] = "B",
        ["MR"] = "MC", ["STO"] = "RCL", ["∫"] = "d/dx", ["nCr"] = "nPr",
        ["poly2"] = "poly3", ["poly4"] = "solve"
    };

    public static string Insertion(string key, bool empty) => key switch
    {
        "sin" or "cos" or "tan" or "ln" or "log" or "exp" or "sqrt" or "dms" or "logb" or
        "pol" or "rec" or "conj" or "arg" or "cmplx" or "sinh" or "cosh" or "tanh" or
        "poly2" or "poly3" or "poly4" or "solve" => key + "(",
        "X" or "Y" => key + "()",
        "M+" => "madd(ANS)",
        "MR" => "m(",
        "MC" => "mc(",
        "STO" => "sto(",
        "RCL" => "rcl(",
        "A" or "B" or "D" => key.ToLowerInvariant(),
        "d/c" => empty ? "1/2" : "/",
        "SPC" => " ",
        "x²" => "square(",
        "x³" => "cube(",
        "1/x" => "recip(",
        "EXP" => "tenpow(",
        "(-)" => "-",
        "Rand#" => "rand()",
        "∫" => "integrate(",
        "d/dx" => "deriv(",
        "nCr" => "combin(",
        "nPr" => "perm(",
        _ => key
    };
}
