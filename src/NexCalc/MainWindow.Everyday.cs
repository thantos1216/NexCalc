using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NexCalc.Core;

namespace NexCalc;

public partial class MainWindow
{
    void InitializeEverydayModes()
    {
        BuildStandard();
        BuildCarpenter();
        BuildConverter();
        AddNumberPad(graphPad, Plot, true, graphExpr, graphExpr2, graphMinText, graphMaxText,
            graphYminText, graphYmaxText, graphSamplesText, graphXsclText, graphYsclText);
    }

    void BuildStandard()
    {
        var input = AddInput(panelStandard, "Expression");
        var output = AddResult(panelStandard);
        output.Text = "0";
        Val? answer = null;
        bool evaluated = false;
        input.TextChanged += (_, _) => evaluated = false;

        void Calculate()
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            var result = NexCalc.Core.Expression.Calculate(input.Text, AngleMode.Deg, answer);
            if (result.ok && result.value is { } value)
            {
                answer = value;
                evaluated = true;
                output.Text = Format.Display(value);
            }
            else output.Text = result.message ?? "Unable to calculate";
        }

        OnEnter(input, Calculate);
        AddKeys(panelStandard, 4, ["AC", "DEL", "%", "/", "7", "8", "9", "*",
            "4", "5", "6", "-", "1", "2", "3", "+", "±", "0", ".", "="], key =>
        {
            if (key == "=") { Calculate(); return; }
            if (key == "AC")
            {
                input.Clear();
                output.Text = "0";
                answer = null;
                return;
            }
            if (evaluated && key != "DEL")
            {
                input.Text = key is "+" or "-" or "*" or "/" or "%" or "±" ? "ANS" : "";
                input.CaretIndex = input.Text.Length;
            }
            evaluated = false;
            if (key == "±")
            {
                input.Text = input.Text.Length == 0 ? "-" : $"-({input.Text})";
                input.CaretIndex = input.Text.Length;
            }
            else EditInput(input, key);
        });
    }

    void BuildCarpenter()
    {
        var tool = AddChoice(panelCarpenter, "Calculation",
            ["Length", "Area", "Volume", "Board feet", "Diagonal", "Material quantity"]);
        var system = AddChoice(panelCarpenter, "Measurement system", ["Imperial", "Metric"]);
        var lengthUnit = AddChoice(panelCarpenter, "Metric length unit", ["mm", "cm", "m"]);
        var fields = new TextBox[3];
        var labels = new Label[3];
        for (int i = 0; i < fields.Length; i++)
        {
            fields[i] = AddInput(panelCarpenter, "", out labels[i]);
        }
        var calculateRow = new StackPanel();
        panelCarpenter.Children.Add(calculateRow);
        var output = AddResult(panelCarpenter);
        AddKeys(calculateRow, 1, ["Calculate"], _ => Calculate());
        foreach (var field in fields) OnEnter(field, Calculate);
        AddNumberPad(panelCarpenter, Calculate, false, fields);

        CarpenterTool Tool() => (CarpenterTool)tool.SelectedIndex;
        bool Metric() => system.SelectedIndex == 1;

        void Refresh()
        {
            bool metricLength = Metric() && Tool() == CarpenterTool.Length;
            ((FrameworkElement)lengthUnit.Parent).Visibility = metricLength ? Visibility.Visible : Visibility.Collapsed;
            var prompts = Measurements.Prompts(Tool(), Metric());
            for (int i = 0; i < fields.Length; i++)
            {
                bool shown = i < prompts.Count;
                fields[i].Visibility = labels[i].Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
                if (shown)
                {
                    string caption = metricLength ? $"Length ({lengthUnit.SelectedItem})" : prompts[i];
                    labels[i].Content = caption;
                    AutomationProperties.SetName(fields[i], caption);
                }
                fields[i].Text = Tool() == CarpenterTool.MaterialQuantity && i == 2 ? "10" : "";
            }
            output.Clear();
        }

        void Calculate()
        {
            Span<double> values = stackalloc double[3];
            values.Clear();
            int count = Measurements.Prompts(Tool(), Metric()).Count;
            for (int i = 0; i < count; i++)
            {
                if (Tool() == CarpenterTool.Length && !Metric() &&
                    string.IsNullOrWhiteSpace(fields[i].Text) && !string.IsNullOrWhiteSpace(fields[1 - i].Text))
                    continue;
                if (!Measurements.TryParse(fields[i].Text, out values[i]) || values[i] < 0)
                {
                    output.Text = $"Enter a nonnegative value for {labels[i].Content}.";
                    return;
                }
            }
            output.Text = Measurements.Carpenter(Tool(), Metric(), lengthUnit.SelectedIndex, values[0], values[1], values[2]);
        }

        tool.SelectionChanged += (_, _) => Refresh();
        system.SelectionChanged += (_, _) => Refresh();
        lengthUnit.SelectionChanged += (_, _) => Refresh();
        Refresh();
    }

    void BuildConverter()
    {
        var catalog = UnitConversion.Categories;
        var category = AddChoice(panelConverter, "Measurement", catalog.Select(item => item.Name));
        var from = AddChoice(panelConverter, "From", []);
        var to = AddChoice(panelConverter, "To", []);
        var input = AddInput(panelConverter, "Value to convert");
        input.Text = "1";
        AddKeys(panelConverter, 1, ["Swap units"], _ => (from.SelectedIndex, to.SelectedIndex) = (to.SelectedIndex, from.SelectedIndex));
        var output = AddResult(panelConverter);
        AddNumberPad(panelConverter, Calculate, false, input);
        OnEnter(input, Calculate);

        void Calculate()
        {
            if (from.SelectedIndex < 0 || to.SelectedIndex < 0) return;
            if (!Measurements.TryParse(input.Text, out double value))
            {
                output.Text = string.IsNullOrWhiteSpace(input.Text) ? "" : "Enter a valid value.";
                return;
            }
            var units = catalog[category.SelectedIndex].Units;
            var target = units[to.SelectedIndex];
            output.Text = $"{Measurements.Number(UnitConversion.Convert(value, units[from.SelectedIndex], target))} {target.Name}";
        }

        void RefreshUnits()
        {
            var names = catalog[category.SelectedIndex].Units.Select(unit => unit.Name).ToArray();
            from.ItemsSource = to.ItemsSource = names;
            from.SelectedIndex = 0;
            to.SelectedIndex = 1;
        }

        category.SelectionChanged += (_, _) => RefreshUnits();
        from.SelectionChanged += (_, _) => Calculate();
        to.SelectionChanged += (_, _) => Calculate();
        input.TextChanged += (_, _) => Calculate();
        RefreshUnits();
    }

    ComboBox AddChoice(Panel panel, string caption, IEnumerable<string> items)
    {
        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var choice = new ComboBox { ItemsSource = items, SelectedIndex = 0, Padding = new Thickness(5) };
        row.Children.Add(new Label { Content = caption, Target = choice });
        AutomationProperties.SetName(choice, caption);
        row.Children.Add(choice);
        panel.Children.Add(row);
        return choice;
    }

    TextBox AddInput(Panel panel, string caption) => AddInput(panel, caption, out _);

    TextBox AddInput(Panel panel, string caption, out Label label)
    {
        var input = new TextBox { FontSize = 17, Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 8) };
        input.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        input.SetResourceReference(ForegroundProperty, "TextBrush");
        label = new Label { Content = caption, Target = input };
        AutomationProperties.SetName(input, caption);
        panel.Children.Add(label);
        panel.Children.Add(input);
        return input;
    }

    TextBox AddResult(Panel panel)
    {
        var result = new TextBox { FontSize = 21, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0), Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 8), MinHeight = 42 };
        result.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        result.SetResourceReference(ForegroundProperty, "TextBrush");
        AutomationProperties.SetName(result, "Result");
        panel.Children.Add(result);
        return result;
    }

    void AddKeys(Panel panel, int columns, IEnumerable<string> keys, Action<string> press)
    {
        var grid = new UniformGrid { Columns = columns };
        foreach (string key in keys)
        {
            var button = new Button { Content = key, MinHeight = 34 };
            button.Click += (_, _) => press(key);
            grid.Children.Add(button);
        }
        panel.Children.Add(grid);
    }

    void AddNumberPad(Panel panel, Action calculate, bool expression, params TextBox[] fields)
    {
        TextBox? active = null;
        foreach (var field in fields)
            field.GotKeyboardFocus += (_, _) => active = field;
        void Press(string key)
        {
            if (key == "=") { calculate(); return; }
            var target = active is { IsVisible: true } ? active : fields[0];
            if (target.Text == "auto" && key is not ("DEL" or "Clear")) target.SelectAll();
            EditInput(target, key);
        }
        AddKeys(panel, 5, ["7", "8", "9", "/", "DEL", "4", "5", "6", "+", "Clear",
            "1", "2", "3", "-", "*", "0", ".", "SPC", "%", "="], Press);
        if (expression) AddKeys(panel, 5, ["(", ")", ",", "^", "x"], Press);
    }

    static void EditInput(TextBox input, string key, bool literal = false)
    {
        if (!literal && key == "Clear") { input.Clear(); return; }
        int start = input.SelectionStart;
        if (!literal && key == "DEL" && input.SelectionLength == 0 && start > 0)
            input.Select(--start, 1);
        string insertion = literal ? key : key switch { "DEL" => "", "SPC" => " ", _ => key };
        input.SelectedText = insertion;
        input.Select(start + insertion.Length, 0);
    }

    static void OnEnter(TextBox input, Action calculate)
    {
        input.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            calculate();
            e.Handled = true;
        };
    }

    void GraphPad_Click(object sender, RoutedEventArgs e) =>
        graphPad.Visibility = graphPad.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
}
