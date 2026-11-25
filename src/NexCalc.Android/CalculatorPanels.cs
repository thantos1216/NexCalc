using Android.Views;
using Android.Widget;
using NexCalc.Core;

namespace NexCalc.Mobile;

public sealed partial class MainActivity
{
    private View ScientificPanel()
    {
        var panel = Column();
        var input = Field("Expression");
        var result = Label("0", 30);
        result.SetTextIsSelectable(true);
        result.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        var exactResult = Label("", 17);
        exactResult.SetTextColor(Accent);
        exactResult.SetTextIsSelectable(true);
        exactResult.Visibility = ViewStates.Gone;
        Val? visibleValue = null;
        var print = Label("", 14);
        print.Typeface = global::Android.Graphics.Typeface.Monospace;
        var history = new List<(string Expression, string Display)>();
        var historyButton = Key("History", () =>
        {
            if (history.Count == 0)
            {
                var emptyDialog = new global::Android.App.AlertDialog.Builder(this);
                emptyDialog.SetMessage("No calculations yet.");
                emptyDialog.SetPositiveButton("OK", (_, _) => { });
                emptyDialog.Show();
                return;
            }

            var historyDialog = new global::Android.App.AlertDialog.Builder(this);
            historyDialog.SetTitle("History");
            historyDialog.SetItems(history.Select(entry => entry.Expression + " = " + entry.Display).ToArray(), (_, args) =>
                {
                    input.Text = history[args.Which].Expression;
                    input.SetSelection(input.Text?.Length ?? 0);
                });
            historyDialog.Show();
        });

        void UpdatePrint()
        {
            print.Visibility = mathPrint ? ViewStates.Visible : ViewStates.Gone;
            if (mathPrint) print.Text = Format.MathPrint(input.Text ?? "");
        }

        void RenderResult()
        {
            if (visibleValue is not { } value)
            {
                exactResult.Visibility = ViewStates.Gone;
                return;
            }

            result.Text = Format.Display(value, displayFormat, digits);
            bool showExact = displayFormat == DisplayFormat.Norm && value.exact &&
                value.r is { } fraction && !fraction.CleanDecimal();
            exactResult.Visibility = showExact ? ViewStates.Visible : ViewStates.Gone;
            if (showExact)
                exactResult.Text = "Exact: " + Format.Display(value, DisplayFormat.Frac, digits);
        }

        void Evaluate()
        {
            var text = input.Text ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;
            var calculation = Expression.Calculate(text, angleMode, answer);
            if (calculation.ok && calculation.value is { } value)
            {
                answer = value;
                visibleValue = value;
                RenderResult();
                result.SetTextColor(Foreground);
                history.Insert(0, (text, result.Text ?? ""));
            }
            else
            {
                visibleValue = null;
                RenderResult();
                result.Text = calculation.message ?? "Unable to calculate";
                result.SetTextColor(ErrorColor);
            }
        }

        panel.AddView(result);
        panel.AddView(exactResult);
        panel.AddView(input);
        panel.AddView(print);
        Row(panel,
            Choice("Angle", ["DEG", "RAD", "GRAD"], (int)angleMode, selected => { angleMode = (AngleMode)selected; SaveSettings(); }),
            Choice("Format", ["Norm", "Fix", "Sci", "DMS", "Frac", "Mixed"], (int)displayFormat,
                selected => { displayFormat = (DisplayFormat)selected; RenderResult(); SaveSettings(); }),
            Choice("Digits", Enumerable.Range(0, 11).Select(value => value.ToString()).ToArray(), digits,
                selected => { digits = selected; RenderResult(); SaveSettings(); }));
        var toggle = new CheckBox(this) { Text = "MathPrint", Checked = mathPrint };
        toggle.SetTextColor(Foreground);
        toggle.CheckedChange += (_, args) => { mathPrint = args.IsChecked; UpdatePrint(); SaveSettings(); };
        var keyPanel = Column();
        var preferences = GetPreferences(global::Android.Content.FileCreationMode.Private)!;
        int layout = preferences.GetInt("keypad", 0);
        Row(panel, toggle, historyButton);
        UpdatePrint();
        input.TextChanged += (_, _) => UpdatePrint();
        OnDone(input, Evaluate);

        string[][] scientificKeys =
        [
            ["sin", "cos", "tan", "ln", "log", "^"],
            ["sinh", "cosh", "tanh", "sqrt", "exp", "dms"],
            ["d/c", "x²", "x³", "1/x", "logb", "Rand#"],
            ["pol", "rec", "X", "Y", "i", "conj"],
            ["arg", "cmplx", "π", "e", "A", "B"],
            ["M+", "MR", "MC", "STO", "RCL", "ANS"],
            ["∫", "d/dx", "nCr", "nPr", "(", ")"],
            ["D", "%", "poly2", "poly3", "poly4", "solve"]
        ];
        string[][] windowsNumbers =
        [
            ["7", "8", "9", "DEL", "AC", ","],
            ["4", "5", "6", "*", "/", ""],
            ["1", "2", "3", "+", "-", ""],
            ["0", ".", "EXP", "(-)", "=", "SPC"]
        ];
        string[][] phoneNumbers =
        [
            ["(", ")", ",", "DEL"],
            ["7", "8", "9", "/"],
            ["4", "5", "6", "*"],
            ["1", "2", "3", "-"],
            ["0", ".", "=", "+"],
            ["AC", "ANS", "(-)", "EXP"]
        ];
        var extraKeys = scientificKeys.SelectMany(row => row)
            .Where(key => key is not ("(" or ")" or "ANS")).Concat(["SPC"]).Chunk(4).ToArray();

        void UpdateKeys()
        {
            keyPanel.RemoveAllViews();
            if (layout == 0)
            {
                KeyRows(keyPanel, phoneNumbers, Press);
                KeyRows(keyPanel, extraKeys, Press);
            }
            else
            {
                KeyRows(keyPanel, scientificKeys, Press);
                KeyRows(keyPanel, windowsNumbers, Press);
            }
        }

        void Press(string key)
        {
            switch (key)
            {
                case "=": Evaluate(); return;
                case "AC":
                    input.Text = "";
                    visibleValue = null;
                    RenderResult();
                    result.Text = "0";
                    result.SetTextColor(Foreground);
                    return;
                case "DEL": Insert(input, "", delete: true); return;
            }

            Insert(input, key switch
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
                "d/c" => string.IsNullOrEmpty(input.Text) ? "1/2" : "/",
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
            });
        }

        Row(panel, Choice("Keypad layout", ["Phone keypad", "Windows keypad"], layout, index =>
        {
            layout = index;
            preferences.Edit()?.PutInt("keypad", layout)?.Apply();
            UpdateKeys();
        }));
        UpdateKeys();
        panel.AddView(keyPanel);
        return panel;
    }

    private View ProgrammerPanel()
    {
        var panel = Column();
        var input = Field("Programmer expression");
        var output = Label("");
        output.SetTextIsSelectable(true);
        var message = Label("");
        message.SetTextColor(ErrorColor);
        string[] formats = ["DEC", "HEX", "OCT", "BIN"];
        int[] bases = [10, 16, 8, 2];
        int[] widths = [8, 16, 32, 64];
        var preferences = GetPreferences(global::Android.Content.FileCreationMode.Private)!;
        int radix = preferences.GetInt("radix", 16);
        int width = preferences.GetInt("width", 32);

        void ShowResults(string[] values) =>
            output.Text = string.Join("\n", formats.Zip(values, (format, value) => format + "   " + value));

        void Evaluate()
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            try
            {
                ShowResults(Programmer.Run(input.Text, radix, width));
                message.Text = "";
            }
            catch (ErrorType error) { message.Text = error.userMsg; }
        }

        panel.AddView(input);
        Row(panel, Choice("Input base", formats, Array.IndexOf(bases, radix), index =>
        {
            int oldRadix = radix;
            radix = bases[index];
            preferences.Edit()?.PutInt("radix", radix)?.Apply();
            if (oldRadix == radix || string.IsNullOrWhiteSpace(input.Text)) return;
            try
            {
                input.Text = Programmer.ConvertBase(input.Text, oldRadix, radix, width);
                input.SetSelection(input.Text?.Length ?? 0);
                Evaluate();
            }
            catch (ErrorType error) { message.Text = error.userMsg; }
        }), Choice("Word size", ["8", "16", "32", "64"], Array.IndexOf(widths, width), index =>
        {
            width = widths[index];
            preferences.Edit()?.PutInt("width", width)?.Apply();
            Evaluate();
        }), Key("Clear", () => { input.Text = ""; message.Text = ""; ShowResults(["0", "0", "0", "0"]); }));
        panel.AddView(output);
        panel.AddView(message);
        ShowResults(["0", "0", "0", "0"]);
        OnDone(input, Evaluate);
        KeyRows(panel,
        [
            ["A", "B", "C", "D", "E", "F"],
            ["and", "or", "xor", "shl", "shr", "~"],
            ["mod", "+", "-", "*", "/", "!"],
            ["7", "8", "9", "0x", "0b", "0o"],
            ["4", "5", "6", "(", ")", "DEL"],
            ["1", "2", "3", "0", "", "="]
        ], key =>
        {
            if (key == "=") Evaluate();
            else if (key == "DEL") Insert(input, "", delete: true);
            else Insert(input, key is "and" or "or" or "xor" or "shl" or "shr" or "mod" ? " " + key + " " : key);
        });
        return panel;
    }

    private View StatisticsPanel()
    {
        var panel = Column();
        var input = Field("Statistics expression");
        var output = Label("0", 28);
        var message = Label("");
        message.SetTextColor(ErrorColor);
        void Evaluate()
        {
            var calculation = Expression.Calculate(input.Text ?? "", angleMode);
            message.Text = calculation.message ?? "";
            if (calculation.ok && calculation.value is { } value) output.Text = Format.Display(value);
        }

        panel.AddView(output);
        panel.AddView(input);
        Row(panel, Key("Clear", () => { input.Text = ""; message.Text = ""; }), Key("=", Evaluate));
        panel.AddView(message);
        OnDone(input, Evaluate);
        string[][] labels =
        [
            ["DATA+", "n", "Σx", "Σx²"], ["Avg", "σn", "σn-1", "Var"], ["Min", "Max", "CLR", ")"],
            ["DAT2", "LIN", "LOG", "EXP"], ["POW", "INV", "QUAD", "r"], ["A", "B", "C", "Σxy"],
            ["x̄", "ȳ", "ŷ", "x̂"]
        ];
        string[] functions =
        [
            "data(", "n(", "sumx(", "sumx2(", "mean(", "sd(", "sds(", "var(", "minx(", "maxx(", "clrd(", ")",
            "dat(", "lin(", "logreg(", "expreg(", "powreg(", "invreg(", "quadreg(", "r(", "a(", "b(", "c(", "rsumxy(",
            "rmeanx(", "rmeany(", "yhat(", "xhat("
        ];
        var shortcuts = labels.SelectMany(row => row).Zip(functions).ToDictionary(pair => pair.First, pair => pair.Second);
        KeyRows(panel, labels, key => Insert(input, shortcuts[key]));
        return panel;
    }
}
