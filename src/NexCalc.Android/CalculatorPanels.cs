using Android.Views;
using Android.Widget;
using NexCalc.Core;

namespace NexCalc.Mobile;

public sealed partial class MainActivity
{
    private View ScientificPanel()
    {
        var panel = Column();
        var input = DisplayInput("Expression");
        var result = Label("0", 38);
        result.SetMinHeight(Dp(72));
        result.Gravity = GravityFlags.End | GravityFlags.CenterVertical;
        result.SetTextIsSelectable(true);
        result.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        var exactResult = Label("", 17);
        ApplyThemeTo(() => exactResult.SetTextColor(Accent));
        bool resultHasError = false;
        ApplyThemeTo(() => result.SetTextColor(resultHasError ? ErrorColor : Foreground));
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
                var emptyDialog = Dialog();
                emptyDialog.SetMessage("No calculations yet.");
                emptyDialog.SetPositiveButton("OK", (_, _) => { });
                emptyDialog.Show();
                return;
            }

            var historyDialog = Dialog();
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
            string? exact = Format.ExactCompanion(value, displayFormat);
            exactResult.Visibility = exact is not null ? ViewStates.Visible : ViewStates.Gone;
            if (exact is not null) exactResult.Text = "Exact: " + exact;
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
                resultHasError = false;
                result.SetTextColor(Foreground);
                history.Insert(0, (text, result.Text ?? ""));
            }
            else
            {
                visibleValue = null;
                RenderResult();
                result.Text = calculation.message ?? "Unable to calculate";
                resultHasError = true;
                result.SetTextColor(ErrorColor);
            }
        }

        panel.AddView(CalculatorDisplay(input, result, exactResult, print));
        Row(panel,
            Choice("Angle", ["DEG", "RAD", "GRAD"], (int)angleMode, selected => { angleMode = (AngleMode)selected; SaveSettings(); }),
            Choice("Format", ["Norm", "Fix", "Sci", "DMS", "Frac", "Mixed"], (int)displayFormat,
                selected => { displayFormat = (DisplayFormat)selected; RenderResult(); SaveSettings(); }),
            Choice("Digits", Enumerable.Range(0, 11).Select(value => value.ToString()).ToArray(), digits,
                selected => { digits = selected; RenderResult(); SaveSettings(); }));
        var toggle = new CheckBox(this) { Text = "MathPrint", Checked = mathPrint };
        ApplyThemeTo(() => toggle.SetTextColor(Foreground));
        toggle.CheckedChange += (_, args) => { mathPrint = args.IsChecked; UpdatePrint(); SaveSettings(); };
        var keyPanel = Column();
        UpdatePrint();
        input.TextChanged += (_, _) => UpdatePrint();
        OnDone(input, Evaluate);

        string[][] scientificKeys =
        [
            ["sin", "cos", "tan", "ln", "log", "^"],
            ["exp", "x²", "1/x", "d/c", "Rand#", "%"],
            ["pol", "X", "i", "conj", "π", "A"],
            ["M+", "MR", "STO", "ANS", "∫", "nCr"],
            ["poly2", "poly4", "D", "(", ")", ","]
        ];
        string[][] numberKeys =
        [
            ["7", "8", "9", "DEL", "AC"],
            ["4", "5", "6", "*", "/"],
            ["1", "2", "3", "+", "-"],
            ["0", ".", "(-)", "SPC", "="]
        ];

        var shiftedButtons = new List<(Button Button, string Primary, string Alternate)>();
        bool shifted = false;
        Button shiftButton = null!;
        shiftButton = Key("Shift", () => SetShift(!shifted));

        void SetShift(bool active)
        {
            shifted = active;
            shiftButton.Text = active ? "Shift On" : "Shift";
            shiftButton.ContentDescription = active ? "Shift on" : "Shift off";
            shiftButton.SetTextColor(active ? Accent : Foreground);

            foreach (var (button, primary, alternate) in shiftedButtons)
            {
                button.Text = active ? alternate : primary;
                button.ContentDescription = active ? alternate : $"{primary}, Shift {alternate}";
                button.SetTextColor(active ? Accent : Foreground);
            }
        }

        void BindShiftKey(string key, Button button)
        {
            StyleKey(button, key switch
            {
                "M+" or "MR" or "STO" or "ANS" or "A" or "D" => 0x49395B,
                "(" or ")" or "," => 0x34424F,
                _ => 0x263F53
            });
            if (ScientificKeys.Alternates.TryGetValue(key, out var alternate))
                shiftedButtons.Add((button, key, alternate));
        }

        void PressKey(string key)
        {
            if (shifted && ScientificKeys.Alternates.TryGetValue(key, out var alternate))
            {
                SetShift(false);
                Press(alternate);
            }
            else
            {
                if (key == "AC") SetShift(false);
                Press(key);
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
                    resultHasError = false;
                    result.SetTextColor(Foreground);
                    return;
                case "DEL": Insert(input, "", delete: true); return;
            }

            Insert(input, ScientificKeys.Insertion(key, string.IsNullOrEmpty(input.Text)));
        }

        StyleKey(shiftButton, 0x135764);
        Row(panel, toggle, historyButton, shiftButton);
        KeyRows(keyPanel, scientificKeys, PressKey, BindShiftKey, stretch: true);
        KeyRows(keyPanel, numberKeys, PressKey, (key, button) =>
        {
            StyleKey(button, key switch
            {
                "AC" or "DEL" => 0x683943,
                "=" => 0x185C6B,
                "*" or "/" or "+" or "-" => 0x494365,
                _ => 0x34424F
            });
            button.TextSize = 17;
        }, stretch: true);
        ApplyThemeTo(() => SetShift(shifted));
        panel.AddView(keyPanel, new LinearLayout.LayoutParams(-1, -2, 1));
        return panel;
    }

    private View ProgrammerPanel()
    {
        var panel = Column();
        var input = Field("Programmer expression");
        var output = Label("");
        output.SetTextIsSelectable(true);
        var message = Label("");
        ApplyThemeTo(() => message.SetTextColor(ErrorColor));
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
        var keypad = Column();
        KeyRows(keypad,
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
        }, stretch: true);
        panel.AddView(keypad, new LinearLayout.LayoutParams(-1, -2, 1));
        return panel;
    }

    private View StatisticsPanel()
    {
        var panel = Column();
        var input = Field("Statistics expression");
        var output = Label("0", 28);
        var message = Label("");
        ApplyThemeTo(() => message.SetTextColor(ErrorColor));
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
        var statKeys = Column();
        KeyRows(statKeys, labels, key => Insert(input, shortcuts[key]), stretch: true);
        panel.AddView(statKeys, new LinearLayout.LayoutParams(-1, -2, 1));
        panel.AddView(NumberPad(() => input, Evaluate, [input], expression: true), new LinearLayout.LayoutParams(-1, -2, 1));
        return panel;
    }
}
