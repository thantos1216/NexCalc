using Android.App;
using ColorStateList = Android.Content.Res.ColorStateList;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using NexCalc.Core;

namespace NexCalc.Mobile;

[Activity(Label = "NexCalc", Icon = "@drawable/nexcalc", Theme = "@android:style/Theme.Material.NoActionBar", MainLauncher = true, Exported = true,
    ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation |
        global::Android.Content.PM.ConfigChanges.ScreenSize | global::Android.Content.PM.ConfigChanges.KeyboardHidden)]
public sealed partial class MainActivity : Activity
{
    private bool lightTheme;
    private readonly List<Action> themeUpdates = [];
    private Color Background => lightTheme ? Color.Rgb(244, 246, 250) : Color.Rgb(22, 22, 34);
    private Color Foreground => lightTheme ? Color.Rgb(28, 38, 55) : Color.Rgb(238, 240, 248);
    private Color Accent => lightTheme ? Color.Rgb(0, 80, 111) : Color.Rgb(143, 216, 255);
    private Color ErrorColor => lightTheme ? Color.Rgb(129, 33, 45) : Color.Rgb(255, 158, 158);

    private void ApplyThemeTo(Action update)
    {
        themeUpdates.Add(update);
        update();
    }

    private AlertDialog.Builder Dialog() => new(this, lightTheme
        ? global::Android.Resource.Style.ThemeMaterialLightDialogAlert
        : global::Android.Resource.Style.ThemeMaterialDialogAlert);

    private void ChooseTheme()
    {
        var dialog = Dialog();
        dialog.SetTitle("Theme");
        dialog.SetSingleChoiceItems(["Light", "Dark"], lightTheme ? 0 : 1, (sender, args) =>
        {
            lightTheme = args.Which == 0;
            using var editor = GetPreferences(global::Android.Content.FileCreationMode.Private)?.Edit();
            editor?.PutBoolean("lightTheme", lightTheme)?.Apply();
            foreach (var update in themeUpdates) update();
            (sender as global::Android.Content.IDialogInterface)?.Dismiss();
        });
        dialog.Show();
    }

    private readonly string[] modeNames = ["Standard", "Scientific", "Programmer", "Graph", "Python", "Stat", "Carpenter", "Unit Converter"];
    private View[] panels = [];
    private int activeMode = 1;
    private Val? answer;
    private AngleMode angleMode;
    private DisplayFormat displayFormat;
    private int digits;
    private bool mathPrint;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetSoftInputMode(SoftInput.AdjustResize);

        var preferences = GetPreferences(global::Android.Content.FileCreationMode.Private)!;
        lightTheme = preferences.GetBoolean("lightTheme", false);
        angleMode = (AngleMode)preferences.GetInt("angle", 0);
        displayFormat = (DisplayFormat)preferences.GetInt("format", 0);
        digits = preferences.GetInt("digits", 0);
        mathPrint = preferences.GetBoolean("mathPrint", false);

        var root = Column();
        ApplyThemeTo(() => root.SetBackgroundColor(Background));
        root.FocusableInTouchMode = true;
        var insetPadding = new InsetPadding(Dp(12));
        root.SetOnApplyWindowInsetsListener(insetPadding);
        root.SetPadding(Dp(12), 0, Dp(12), 0);

        var header = new LinearLayout(this);
        header.SetGravity(GravityFlags.CenterVertical);
        var title = Label(modeNames[activeMode], 20);
        ApplyThemeTo(() => title.SetTextColor(Accent));
        header.AddView(Key("☰ Menu", () =>
        {
            var menu = Dialog();
            menu.SetTitle("NexCalc");
            menu.SetSingleChoiceItems([.. modeNames, "Theme…"], activeMode, (dialog, selection) =>
                {
                    (dialog as global::Android.Content.IDialogInterface)?.Dismiss();
                    if (selection.Which == modeNames.Length)
                    {
                        ChooseTheme();
                        return;
                    }

                    panels[activeMode].Visibility = ViewStates.Gone;
                    activeMode = selection.Which;
                    panels[activeMode].Visibility = ViewStates.Visible;
                    title.Text = modeNames[activeMode];
                });
            menu.Show();
        }));
        title.SetPadding(Dp(12), 0, 0, 0);
        header.AddView(title);
        root.AddView(header);

        var frame = new FrameLayout(this);
        root.AddView(frame, new LinearLayout.LayoutParams(-1, 0, 1));
        panels = [StandardPanel(), ScientificPanel(), ProgrammerPanel(), GraphPanel(), PythonPanel(), StatisticsPanel(), CarpenterPanel(), ConverterPanel()];
        foreach (var (panel, index) in panels.Select((panel, index) => (panel, index)))
        {
            View page = panel;
            if (modeNames[index] != "Python")
            {
                var scroll = new ScrollView(this) { FillViewport = true };
                scroll.AddView(panel);
                page = scroll;
            }
            page.Visibility = index == activeMode ? ViewStates.Visible : ViewStates.Gone;
            panels[index] = page;
            frame.AddView(page, new FrameLayout.LayoutParams(-1, -1));
        }

        SetContentView(root);
        root.RequestFocus();
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && Window?.InsetsController is { } insets)
        {
            insets.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            insets.Hide(WindowInsets.Type.SystemBars());
        }
    }

    private LinearLayout Column() => new(this) { Orientation = Orientation.Vertical };

    private TextView Label(string text, int size = 16)
    {
        var label = new TextView(this) { Text = text, TextSize = size };
        ApplyThemeTo(() => label.SetTextColor(Foreground));
        label.SetPadding(Dp(4), Dp(4), Dp(4), Dp(4));
        return label;
    }

    private EditText Field(string hint, string text = "", bool multiline = false)
    {
        var field = new EditText(this)
        {
            Hint = hint,
            ContentDescription = hint,
            Text = text,
            TextSize = 18,
            Typeface = Typeface.Monospace,
            InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions |
                (multiline ? InputTypes.TextFlagMultiLine : 0)
        };
        ApplyThemeTo(() =>
        {
            field.SetTextColor(Foreground);
            field.SetHintTextColor(lightTheme ? Color.Rgb(74, 88, 105) : Color.Rgb(159, 180, 200));
            field.SetBackgroundColor(lightTheme ? Color.Rgb(232, 238, 245) : Color.Rgb(34, 34, 52));
        });
        field.SetPadding(Dp(8), Dp(6), Dp(8), Dp(6));
        field.SetMinHeight(Dp(44));
        if (!multiline)
        {
            field.SetSingleLine(true);
            field.ImeOptions = ImeAction.Done;
        }
        return field;
    }

    private void OnDone(EditText input, Action action)
    {
        input.EditorAction += (_, args) =>
        {
            if (args.ActionId == ImeAction.Done ||
                (args.Event?.KeyCode == Keycode.Enter && args.Event.Action == KeyEventActions.Down))
            {
                action();
                args.Handled = true;
            }
        };
    }

    private Button Key(string text, Action action)
    {
        var button = new Button(this) { Text = text, TextSize = 13 };
        button.SetAllCaps(false);
        StyleKey(button, text == "=" ? 0x3A3A6A : 0x2A2A3E);
        ApplyThemeTo(() => button.SetTextColor(text is "AC" or "DEL" ? ErrorColor : Foreground));
        button.SetPadding(Dp(2), 0, Dp(2), 0);
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        button.Click += (_, _) => action();
        return button;
    }

    private void KeyRows(LinearLayout parent, string[][] rows, Action<string> pressed,
        Action<string, Button>? bind = null, bool stretch = false)
    {
        foreach (var keys in rows)
        {
            var row = new LinearLayout(this);
            foreach (var key in keys)
            {
                View view = string.IsNullOrEmpty(key) ? new Space(this) : Key(key, () => pressed(key));
                if (view is Button button) bind?.Invoke(key, button);
                if (stretch) view.SetMinimumHeight(Dp(43));
                var layout = new LinearLayout.LayoutParams(0, stretch ? -1 : Dp(46), 1);
                layout.SetMargins(Dp(2), 0, Dp(2), 0);
                row.AddView(view, layout);
            }
            parent.AddView(row, new LinearLayout.LayoutParams(-1, -2, stretch ? 1 : 0));
        }
    }

    private void StyleKey(Button button, int rgb)
    {
        if (button.Tag is GradientDrawable existing)
        {
            existing.SetColor(KeyColor(rgb));
            ApplyThemeTo(() => existing.SetColor(KeyColor(rgb)));
            return;
        }

        var shape = new GradientDrawable();
        shape.SetCornerRadius(Dp(7));
        button.Tag = shape;
        button.Background = new InsetDrawable(
            new RippleDrawable(ColorStateList.ValueOf(Color.Argb(55, 128, 145, 165)), shape, null),
            Dp(4), Dp(5), Dp(4), Dp(5));
        ApplyThemeTo(() => shape.SetColor(KeyColor(rgb)));
    }

    private Color KeyColor(int rgb)
    {
        int r = (rgb >> 16) & 255, g = (rgb >> 8) & 255, b = rgb & 255;
        return lightTheme ? Color.Rgb(175 + r / 2, 175 + g / 2, 175 + b / 2) : Color.Rgb(r, g, b);
    }

    private Button Choice(string label, string[] values, int initial, Action<int> changed)
    {
        var selected = initial;
        Button button = null!;
        button = Key(values[initial], () =>
        {
            var choices = Dialog();
            choices.SetTitle(label);
            choices.SetSingleChoiceItems(values, selected, (dialog, args) =>
                {
                    selected = args.Which;
                    button.Text = values[selected];
                    changed(selected);
                    (dialog as global::Android.Content.IDialogInterface)?.Dismiss();
                });
            choices.Show();
        });
        button.ContentDescription = label + ": " + values[initial];
        button.TextChanged += (_, _) => button.ContentDescription = label + ": " + button.Text;
        return button;
    }

    private void Row(LinearLayout parent, params View[] items)
    {
        var row = new LinearLayout(this);
        row.SetGravity(GravityFlags.CenterVertical);
        foreach (var item in items)
            row.AddView(item, new LinearLayout.LayoutParams(0, -2, 1));
        parent.AddView(row);
    }

    private static void Insert(EditText field, string insertion, bool delete = false)
    {
        var text = field.Text ?? "";
        var start = Math.Clamp(Math.Min(field.SelectionStart, field.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(field.SelectionStart, field.SelectionEnd), start, text.Length);
        if (delete && start == end && start > 0)
        {
            start--;
            if (start > 0 && char.IsSurrogatePair(text[start - 1], text[start]))
                start--;
        }
        field.Text = text[..start] + insertion + text[end..];
        field.SetSelection(start + insertion.Length);
    }

    private void SaveSettings()
    {
        using var edit = GetPreferences(global::Android.Content.FileCreationMode.Private)?.Edit();
        edit?.PutInt("angle", (int)angleMode);
        edit?.PutInt("format", (int)displayFormat);
        edit?.PutInt("digits", digits);
        edit?.PutBoolean("mathPrint", mathPrint);
        edit?.Apply();
    }

    private int Dp(int value) => (int)Math.Round(value * (Resources?.DisplayMetrics?.Density ?? 1));

    private sealed class InsetPadding(int padding) : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var edges = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                var keyboard = insets.GetInsets(WindowInsets.Type.Ime());
                view.SetPadding(edges.Left + padding, edges.Top, edges.Right + padding, Math.Max(keyboard.Bottom, edges.Bottom));
            }
            return insets;
        }
    }
}
