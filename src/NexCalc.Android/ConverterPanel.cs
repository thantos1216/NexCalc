using Android.Views;
using Android.Widget;

namespace NexCalc.Mobile;

public sealed partial class MainActivity
{
    // Each unit maps to its category's base unit by value * Scale + Offset.
    private sealed record Unit(string Name, double Scale, double Offset = 0);
    private sealed record UnitCategory(string Name, Unit[] Units);

    private static readonly UnitCategory[] UnitCategories =
    [
        new("Length", [new("Inches", .0254), new("Centimeters", .01), new("Millimeters", .001), new("Feet", .3048),
            new("Yards", .9144), new("Meters", 1), new("Miles", 1609.344), new("Kilometers", 1000)]),
        new("Mass", [new("Ounces (weight)", .028349523125), new("Grams", .001), new("Milligrams", .000001),
            new("Pounds", .45359237), new("Kilograms", 1), new("US short tons", 907.18474), new("Metric tonnes", 1000)]),
        new("Volume", [new("US fl oz", .0295735295625), new("Milliliters", .001), new("US cups", .2365882365),
            new("US pints", .473176473), new("US quarts", .946352946), new("US gallons", 3.785411784),
            new("UK gallons", 4.54609), new("Liters", 1), new("Cubic feet", 28.316846592), new("Cubic yards", 764.554857984)]),
        new("Area", [new("Square feet", .09290304), new("Square meters", 1), new("Square inches", .00064516),
            new("Square centimeters", .0001), new("Square yards", .83612736), new("Acres", 4046.8564224), new("Hectares", 10000)]),
        new("Temperature", [new("Fahrenheit", 5.0 / 9, -160.0 / 9), new("Celsius", 1), new("Kelvin", 1, -273.15)]),
        new("Speed", [new("Miles per hour", .44704), new("Kilometers per hour", 1.0 / 3.6), new("Meters per second", 1),
            new("Feet per second", .3048), new("Knots", 1852.0 / 3600)])
    ];

    private View ConverterPanel()
    {
        var panel = Column();
        var input = Field("Value to convert", "1");
        var output = Label("", 31);
        output.SetTextIsSelectable(true);
        output.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        int category = 0, from = 0, to = 1;
        Button fromButton = null!, toButton = null!;

        void ConvertValue()
        {
            if (!TryMeasurement(input.Text, out var value))
            {
                output.Text = string.IsNullOrWhiteSpace(input.Text) ? "" : "Enter a valid value";
                return;
            }

            var units = UnitCategories[category].Units;
            var source = units[from];
            var target = units[to];
            double converted = from == to ? value :
                (value * source.Scale + source.Offset - target.Offset) / target.Scale;
            output.Text = Number(converted) + " " + target.Name;
        }

        void RefreshUnits()
        {
            var units = UnitCategories[category].Units;
            fromButton.Text = units[from].Name;
            toButton.Text = units[to].Name;
            fromButton.ContentDescription = "From " + units[from].Name;
            toButton.ContentDescription = "To " + units[to].Name;
            ConvertValue();
        }

        void ChooseUnit(bool source)
        {
            var dialog = Dialog();
            dialog.SetTitle(source ? "Convert from" : "Convert to");
            dialog.SetSingleChoiceItems(UnitCategories[category].Units.Select(unit => unit.Name).ToArray(),
                source ? from : to, (sender, args) =>
                {
                    if (source) from = args.Which;
                    else to = args.Which;
                    RefreshUnits();
                    (sender as global::Android.Content.IDialogInterface)?.Dismiss();
                });
            dialog.Show();
        }

        fromButton = Key("", () => ChooseUnit(true));
        toButton = Key("", () => ChooseUnit(false));
        panel.AddView(Choice("Measurement", UnitCategories.Select(item => item.Name).ToArray(), 0, index =>
        {
            (category, from, to) = (index, 0, 1);
            RefreshUnits();
        }));
        Row(panel, Label("From"), Label("To"));
        Row(panel, fromButton, toButton);
        panel.AddView(Key("Swap units", () => { (from, to) = (to, from); RefreshUnits(); }));
        panel.AddView(input);
        panel.AddView(output);
        panel.AddView(NumberPad(() => input, ConvertValue, [input]), new LinearLayout.LayoutParams(-1, -2, 1));
        input.TextChanged += (_, _) => ConvertValue();
        RefreshUnits();
        return panel;
    }
}
