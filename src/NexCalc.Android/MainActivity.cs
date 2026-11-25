using Android.App;
using ColorStateList = Android.Content.Res.ColorStateList;
using Android.Graphics;
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
    private static readonly Color Background = Color.Rgb(22, 22, 34);
    private static readonly Color Surface = Color.Rgb(42, 42, 62);
    private static readonly Color Foreground = Color.Rgb(238, 240, 248);
    private static readonly Color Accent = Color.Rgb(143, 216, 255);
    private static readonly Color ErrorColor = Color.Rgb(255, 158, 158);

    private readonly string[] modeNames = ["Scientific", "Programmer", "Graph", "Python", "Stat"];
    private View[] panels = [];
    private int activeMode;
    private Val? answer;
    private AngleMode angleMode;
    private DisplayFormat displayFormat;
    private int digits;
    private bool mathPrint;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var preferences = GetPreferences(global::Android.Content.FileCreationMode.Private)!;
        angleMode = (AngleMode)preferences.GetInt("angle", 0);
        displayFormat = (DisplayFormat)preferences.GetInt("format", 0);
        digits = preferences.GetInt("digits", 0);
        mathPrint = preferences.GetBoolean("mathPrint", false);

        var root = Column();
        root.SetBackgroundColor(Background);
        root.FocusableInTouchMode = true;
        var insetPadding = new InsetPadding(Dp(12));
        root.SetOnApplyWindowInsetsListener(insetPadding);
        root.SetPadding(Dp(12), 0, Dp(12), 0);

        var header = new LinearLayout(this);
        header.SetGravity(GravityFlags.CenterVertical);
        var title = Label(modeNames[0], 20);
        title.SetTextColor(Accent);
        header.AddView(Key("☰ Menu", () =>
        {
            var menu = new AlertDialog.Builder(this);
            menu.SetTitle("NexCalc");
            menu.SetSingleChoiceItems(modeNames, activeMode, (dialog, selection) =>
                {
                    panels[activeMode].Visibility = ViewStates.Gone;
                    activeMode = selection.Which;
                    panels[activeMode].Visibility = ViewStates.Visible;
                    title.Text = modeNames[activeMode];
                    (dialog as global::Android.Content.IDialogInterface)?.Dismiss();
                });
            menu.Show();
        }));
        title.SetPadding(Dp(12), 0, 0, 0);
        header.AddView(title);
        root.AddView(header);

        var frame = new FrameLayout(this);
        root.AddView(frame, new LinearLayout.LayoutParams(-1, 0, 1));
        panels = [ScientificPanel(), ProgrammerPanel(), GraphPanel(), PythonPanel(), StatisticsPanel()];
        foreach (var (panel, index) in panels.Select((panel, index) => (panel, index)))
        {
            var scroll = new ScrollView(this) { FillViewport = true };
            scroll.AddView(panel);
            scroll.Visibility = index == 0 ? ViewStates.Visible : ViewStates.Gone;
            panels[index] = scroll;
            frame.AddView(scroll, new FrameLayout.LayoutParams(-1, -1));
        }

        SetContentView(root);
        root.RequestFocus();
    }

    private LinearLayout Column() => new(this) { Orientation = Orientation.Vertical };

    private TextView Label(string text, int size = 16)
    {
        var label = new TextView(this) { Text = text, TextSize = size };
        label.SetTextColor(Foreground);
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
        field.SetTextColor(Foreground);
        field.SetHintTextColor(Color.Rgb(159, 180, 200));
        field.SetBackgroundColor(Color.Rgb(34, 34, 52));
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
        button.SetTextColor(text is "AC" or "DEL" ? ErrorColor : Foreground);
        button.BackgroundTintList = ColorStateList.ValueOf(text == "=" ? Color.Rgb(58, 58, 106) : Surface);
        button.SetPadding(Dp(2), 0, Dp(2), 0);
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        button.Click += (_, _) => action();
        return button;
    }

    private void KeyRows(LinearLayout parent, string[][] rows, Action<string> pressed,
        Action<string, Button>? bind = null)
    {
        foreach (var keys in rows)
        {
            var row = new LinearLayout(this);
            foreach (var key in keys)
            {
                View view = string.IsNullOrEmpty(key) ? new Space(this) : Key(key, () => pressed(key));
                if (view is Button button) bind?.Invoke(key, button);
                var layout = new LinearLayout.LayoutParams(0, Dp(46), 1);
                layout.SetMargins(Dp(2), 0, Dp(2), 0);
                row.AddView(view, layout);
            }
            parent.AddView(row);
        }
    }

    private Button Choice(string label, string[] values, int initial, Action<int> changed)
    {
        var selected = initial;
        Button button = null!;
        button = Key(values[initial], () =>
        {
            var choices = new AlertDialog.Builder(this);
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
