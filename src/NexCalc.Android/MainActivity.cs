using Android.App;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using NexCalc.Core;

namespace NexCalc.Mobile;

[Activity(Label = "NexCalc", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private static readonly string[][] KeyRows =
    [
        ["sin", "cos", "tan", "sqrt"],
        ["log", "ln", "π", "^"],
        ["(", ")", "Ans", "DEL"],
        ["7", "8", "9", "/"],
        ["4", "5", "6", "*"],
        ["1", "2", "3", "-"],
        ["0", ".", "=", "+"]
    ];

    private EditText expression = null!;
    private TextView result = null!;
    private AngleMode angleMode = AngleMode.Deg;
    private Val? answer;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var scroll = new ScrollView(this);
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = Dp(16);
        content.SetPadding(0, padding, 0, padding);
        scroll.SetPadding(padding, 0, padding, 0);
        scroll.AddView(content);

        var insetPadding = new InsetPadding(padding);
        scroll.SetOnApplyWindowInsetsListener(insetPadding);

        expression = new EditText(this)
        {
            Hint = "Enter an expression",
            ContentDescription = "Expression",
            InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions,
            ImeOptions = ImeAction.Done,
            TextSize = 24
        };
        expression.SetSingleLine(true);
        expression.EditorAction += (_, args) =>
        {
            if (args.ActionId == ImeAction.Done)
            {
                Calculate();
                args.Handled = true;
            }
        };
        content.AddView(expression);

        result = new TextView(this)
        {
            TextSize = 28,
            AccessibilityLiveRegion = AccessibilityLiveRegion.Polite
        };
        result.SetMinHeight(Dp(60));
        result.SetTextIsSelectable(true);
        content.AddView(result);

        var controls = new LinearLayout(this);
        var toggle = new Button(this) { Text = "Degrees" };
        toggle.Click += (_, _) =>
        {
            angleMode = angleMode == AngleMode.Deg ? AngleMode.Rad : AngleMode.Deg;
            toggle.Text = angleMode == AngleMode.Deg ? "Degrees" : "Radians";
        };
        AddButton(controls, toggle);

        var clear = new Button(this) { Text = "Clear" };
        clear.Click += (_, _) =>
        {
            expression.Text = "";
            result.Text = "";
        };
        AddButton(controls, clear);
        content.AddView(controls);

        foreach (var keys in KeyRows)
        {
            var row = new LinearLayout(this);
            foreach (var key in keys)
            {
                var button = new Button(this) { Text = key };
                button.SetAllCaps(false);
                button.Click += (_, _) => PressKey(key);
                AddButton(row, button);
            }
            content.AddView(row);
        }

        SetContentView(scroll);
    }

    private void PressKey(string key)
    {
        if (key == "=")
        {
            Calculate();
            return;
        }

        var text = expression.Text ?? "";
        var start = Math.Clamp(Math.Min(expression.SelectionStart, expression.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(expression.SelectionStart, expression.SelectionEnd), start, text.Length);
        if (key == "DEL" && start == end)
        {
            start = Math.Max(0, start - 1);
            if (start > 0 && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1]))
                start--;
        }

        var insertion = key switch
        {
            "DEL" => "",
            "sin" or "cos" or "tan" or "sqrt" or "log" or "ln" => key + "(",
            "π" => "pi",
            "Ans" => "ans",
            _ => key
        };
        expression.Text = text[..start] + insertion + text[end..];
        expression.SetSelection(start + insertion.Length);
    }

    private void Calculate()
    {
        var input = expression.Text ?? "";
        if (string.IsNullOrWhiteSpace(input))
        {
            result.Text = "Enter an expression";
            return;
        }

        var calculation = Expression.Calculate(input, angleMode, answer);
        if (calculation.ok && calculation.value is { } value)
        {
            answer = value;
            result.Text = Format.Display(value);
            return;
        }

        result.Text = calculation.message ?? "Unable to calculate";
    }

    private static void AddButton(LinearLayout row, Button button)
    {
        row.AddView(button, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
    }

    private int Dp(int value) => (int)Math.Round(value * (Resources?.DisplayMetrics?.Density ?? 1));

    private sealed class InsetPadding(int padding) : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var edges = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                view.SetPadding(edges.Left + padding, edges.Top, edges.Right + padding, edges.Bottom);
            }
            return insets;
        }
    }
}
