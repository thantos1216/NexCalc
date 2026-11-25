using System.Globalization;
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
        double[] metresPerUnit = [0.001, 0.01, 1];
        string[][] imperialPrompts =
        [
            ["Feet", "Inches (fraction or decimal)"], ["Length (ft)", "Width (ft)"],
            ["Length (ft)", "Width (ft)", "Depth (ft)"], ["Thickness (in)", "Width (in)", "Length (ft)"],
            ["Length (ft)", "Width (ft)"], ["Area to cover (ft²)", "Coverage per item (ft²)", "Waste (%)"]
        ];
        string[][] metricPrompts =
        [
            ["Length"], ["Length (m)", "Width (m)"], ["Length (m)", "Width (m)", "Depth (m)"],
            ["Thickness (mm)", "Width (mm)", "Length (m)"], ["Length (m)", "Width (m)"],
            ["Area to cover (m²)", "Coverage per item (m²)", "Waste (%)"]
        ];
        string[] Prompts() => (metric ? metricPrompts : imperialPrompts)[selected];

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
                fields[i].Visibility = i < prompts.Length ? ViewStates.Visible : ViewStates.Gone;
                if (i < prompts.Length)
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
            var dimensions = new double[Prompts().Length];
            for (int i = 0; i < dimensions.Length; i++)
            {
                if (selected == 0 && !metric && string.IsNullOrWhiteSpace(fields[i].Text) &&
                    !string.IsNullOrWhiteSpace(fields[1 - i].Text))
                    continue;
                if (!TryMeasurement(fields[i].Text, out dimensions[i]) || dimensions[i] < 0)
                {
                    output.Text = $"Enter a nonnegative value for {fields[i].Hint}.";
                    return;
                }
            }

            double a = dimensions[0], b = dimensions.Length >= 2 ? dimensions[1] : 0;
            double c = dimensions.Length == 3 ? dimensions[2] : 0;
            const double metresPerFoot = 0.3048;
            const double metresPerInch = 0.0254;
            double lengthMetres = a * metresPerUnit[lengthUnit];
            double diagonal = double.Hypot(a, b);
            output.Text = (selected, metric) switch
            {
                (0, true) => $"{Number(lengthMetres * 1000)} mm · {Number(lengthMetres * 100)} cm · {Number(lengthMetres)} m\n{FeetAndInches(lengthMetres / metresPerInch)}",
                (0, false) => $"{FeetAndInches(a * 12 + b)}\n{Number(a * 12 + b)} in · {Number((a * 12 + b) * 2.54)} cm",
                (1, true) => $"{Number(a * b)} m²\n{Number(a * b / 0.09290304)} ft²",
                (1, false) => $"{Number(a * b)} ft²\n{Number(a * b * 0.09290304)} m²",
                (2, true) => $"{Number(a * b * c)} m³\n{Number(a * b * c / 0.028316846592)} ft³",
                (2, false) => $"{Number(a * b * c)} ft³\n{Number(a * b * c / 27)} yd³",
                (3, true) => $"{Number(a * b * c / 1_000_000)} m³\n{Number(a * b * c / 2359.737216)} board ft",
                (3, false) => $"{Number(a * b * c / 12)} board ft",
                (4, true) => $"{Number(diagonal)} m\n{FeetAndInches(diagonal / metresPerInch)}",
                (4, false) => $"{Number(diagonal)} ft · {Number(diagonal * metresPerFoot)} m\n{FeetAndInches(diagonal * 12)}",
                _ when b > 0 => $"{Number(Math.Ceiling(a / b * (1 + c / 100)))} items",
                _ => "Coverage per item must be greater than zero."
            };
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

    private static string FeetAndInches(double inches)
    {
        double rounded = Math.Round(inches * 16, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(rounded) || rounded >= long.MaxValue) return "Measurement out of range";
        long sixteenths = (long)rounded;
        long feet = sixteenths / 192;
        long wholeInches = sixteenths % 192 / 16;
        long remainder = sixteenths % 16;
        var reduced = Rat.Make(remainder, 16);
        string fraction = remainder == 0 ? "" : $" {reduced.num}/{reduced.den}";
        return $"{feet} ft {wholeInches}{fraction} in (nearest 1/16 in)";
    }

    private static bool TryMeasurement(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return double.IsFinite(value);
        var result = Expression.Calculate(text, AngleMode.Deg);
        if (!result.ok || result.value is not { } real || real.complex ||
            real.mat is not null || real.roots is not null || real.text is not null)
            return false;
        value = real.ToDoubleValue();
        return double.IsFinite(value);
    }

    private static string Number(double value) => double.IsFinite(value)
        ? value.ToString("G12", CultureInfo.CurrentCulture) : "Out of range";
}
