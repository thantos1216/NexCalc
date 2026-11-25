using Android.Graphics;
using Android.Views;
using Android.Widget;
using NexCalc.Core;
using Format = NexCalc.Core.Format;

namespace NexCalc.Mobile;

public sealed partial class MainActivity
{
    private EditText DisplayInput(string hint)
    {
        var input = Field(hint);
        ApplyThemeTo(() => input.SetBackgroundColor(Color.Transparent));
        return input;
    }

    private View CalculatorDisplay(params View[] lines)
    {
        var display = Column();
        display.SetPadding(Dp(10), Dp(5), Dp(10), Dp(5));
        ApplyThemeTo(() => display.SetBackgroundColor(lightTheme ? Color.Rgb(220, 236, 234) : Color.Rgb(28, 44, 48)));
        foreach (var line in lines) display.AddView(line);
        return display;
    }

    private View StandardPanel()
    {
        var panel = Column();
        var input = DisplayInput("Expression");
        var output = Label("0", 38);
        output.Gravity = GravityFlags.End | GravityFlags.CenterVertical;
        output.SetMinHeight(Dp(72));
        output.SetTextIsSelectable(true);
        output.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        Val? lastAnswer = null;
        bool evaluated = false;

        void Calculate()
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            var calculation = Expression.Calculate(input.Text, AngleMode.Deg, lastAnswer);
            if (calculation.ok && calculation.value is { } value)
            {
                lastAnswer = value;
                evaluated = true;
                output.Text = Format.Display(value);
            }
            else output.Text = calculation.message ?? "Unable to calculate";
        }

        panel.AddView(CalculatorDisplay(input, output));
        input.TextChanged += (_, _) => evaluated = false;
        OnDone(input, Calculate);
        var keypad = Column();
        KeyRows(keypad,
        [
            ["AC", "DEL", "%", "/"], ["7", "8", "9", "*"],
            ["4", "5", "6", "-"], ["1", "2", "3", "+"],
            ["±", "0", ".", "="]
        ], key =>
        {
            if (key == "=") { Calculate(); return; }
            if (key == "AC")
            {
                input.Text = "";
                output.Text = "0";
                lastAnswer = null;
                evaluated = false;
                return;
            }
            if (evaluated)
            {
                if (key is "+" or "-" or "*" or "/" or "%" or "±")
                    input.Text = "ANS";
                else if (key != "DEL") input.Text = "";
                input.SetSelection(input.Text?.Length ?? 0);
                evaluated = false;
            }
            if (key == "±")
            {
                var expression = input.Text ?? "";
                input.Text = expression.Length == 0 ? "-" : $"-({expression})";
                input.SetSelection(input.Text?.Length ?? 0);
                return;
            }
            Insert(input, key == "DEL" ? "" : key, delete: key == "DEL");
        }, (key, button) =>
        {
            StyleKey(button, key switch
            {
                "AC" or "DEL" => 0x683943, "=" => 0x185C6B,
                "+" or "-" or "*" or "/" or "%" or "±" => 0x494365, _ => 0x34424F
            });
            button.TextSize = 22;
        }, stretch: true);
        panel.AddView(keypad, new LinearLayout.LayoutParams(-1, -2, 1));
        return panel;
    }

    private View CarpenterPanel()
    {
        var panel = Column();
        var tools = Column();
        int selected = 0;
        bool metric = false;
        int lengthUnit = 0;
        string[] metricUnits = ["mm", "cm", "m"];
        IReadOnlyList<string> Prompts() => Measurements.Prompts((CarpenterTool)selected, metric);

        EditText[] fields = [Field(""), Field(""), Field("")];
        TextView[] captions = [Label(""), Label(""), Label("")];
        var output = Label("", 23);
        output.SetTextIsSelectable(true);
        output.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        Button unitChoice = null!;
        unitChoice = Choice("Metric length unit", metricUnits, 0, index =>
        {
            lengthUnit = index;
            ShowTool();
        });

        void ShowTool()
        {
            unitChoice.Visibility = metric && selected == 0 ? ViewStates.Visible : ViewStates.Gone;
            var prompts = Prompts();
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i].Visibility = i < prompts.Count ? ViewStates.Visible : ViewStates.Gone;
                if (i < prompts.Count)
                {
                    var caption = metric && selected == 0 ? $"Length ({metricUnits[lengthUnit]})" : prompts[i];
                    captions[i].Text = caption;
                    fields[i].Hint = caption;
                    fields[i].ContentDescription = caption;
                }
                captions[i].Visibility = fields[i].Visibility;
                fields[i].Text = selected == 5 && i == 2 ? "10" : "";
            }
            output.Text = "";
        }

        for (int i = 0; i < fields.Length; i++)
        {
            tools.AddView(captions[i]);
            tools.AddView(fields[i]);
            OnDone(fields[i], Calculate);
        }

        void Calculate()
        {
            var dimensions = new double[Prompts().Count];
            for (int i = 0; i < dimensions.Length; i++)
            {
                if (selected == 0 && !metric && string.IsNullOrWhiteSpace(fields[i].Text) &&
                    !string.IsNullOrWhiteSpace(fields[1 - i].Text))
                    continue;
                if (!Measurements.TryParse(fields[i].Text, out dimensions[i]) || dimensions[i] < 0)
                {
                    output.Text = $"Enter a nonnegative value for {fields[i].Hint}.";
                    return;
                }
            }

            double a = dimensions[0], b = dimensions.Length >= 2 ? dimensions[1] : 0;
            double c = dimensions.Length == 3 ? dimensions[2] : 0;
            output.Text = Measurements.Carpenter((CarpenterTool)selected, metric, lengthUnit, a, b, c);
        }

        tools.AddView(Key("Calculate", Calculate));
        tools.AddView(output);
        Row(panel, Choice("Carpenter calculation",
            ["Length", "Area", "Volume", "Board feet", "Diagonal", "Material quantity"], 0,
            index => { selected = index; ShowTool(); }));
        Row(panel, Choice("Measurement system", ["Imperial", "Metric"], 0, index =>
        {
            metric = index == 1;
            ShowTool();
        }), unitChoice);
        panel.AddView(tools);
        panel.AddView(NumberPad(() => fields[0], Calculate, fields, stretch: false));

        ShowTool();
        return panel;
    }

}
