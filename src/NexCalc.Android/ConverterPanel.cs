using Android.Views;
using Android.Widget;
using NexCalc.Core;

namespace NexCalc.Mobile;

public sealed partial class MainActivity
{
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
            if (!Measurements.TryParse(input.Text, out var value))
            {
                output.Text = string.IsNullOrWhiteSpace(input.Text) ? "" : "Enter a valid value";
                return;
            }

            var units = UnitConversion.Categories[category].Units;
            var source = units[from];
            var target = units[to];
            double converted = UnitConversion.Convert(value, source, target);
            output.Text = Measurements.Number(converted) + " " + target.Name;
        }

        void RefreshUnits()
        {
            var units = UnitConversion.Categories[category].Units;
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
            dialog.SetSingleChoiceItems(UnitConversion.Categories[category].Units.Select(unit => unit.Name).ToArray(),
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
        panel.AddView(Choice("Measurement", UnitConversion.Categories.Select(item => item.Name).ToArray(), 0, index =>
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
