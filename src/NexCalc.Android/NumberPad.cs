using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace NexCalc.Mobile;

public sealed partial class MainActivity
{
    private LinearLayout NumberPad(Func<EditText> fallback, Action done, EditText[] fields, bool expression = false, bool stretch = true)
    {
        var pad = Column();
        EditText? active = null;
        foreach (var field in fields)
        {
            field.ShowSoftInputOnFocus = false;
            field.FocusChange += (_, args) =>
            {
                if (args.HasFocus) active = field;
            };
        }

        EditText Target()
        {
            var field = active is { IsShown: true } ? active : fallback();
            if (!field.HasFocus)
            {
                field.RequestFocus();
                field.SetSelection(field.Text?.Length ?? 0);
            }
            return field;
        }

        KeyRows(pad,
        [
            ["7", "8", "9", "/", "DEL"],
            ["4", "5", "6", "+", "Clear"],
            ["1", "2", "3", "-", "*"],
            ["0", ".", "SPC", "%", "="]
        ], key =>
        {
            if (key == "=") { done(); return; }
            var field = Target();
            if (key == "Clear") field.Text = "";
            else Insert(field, key switch { "DEL" => "", "SPC" => " ", _ => key }, delete: key == "DEL");
        }, (key, button) => StyleKey(button, key switch
        {
            "=" => 0x185C6B,
            "DEL" or "Clear" => 0x683943,
            "/" or "+" or "-" or "*" or "%" => 0x494365,
            _ => 0x34424F
        }), stretch: stretch);

        if (expression)
        {
            Row(pad, Key("(", () => Insert(Target(), "(")), Key(")", () => Insert(Target(), ")")),
                Key(",", () => Insert(Target(), ",")), Key("^", () => Insert(Target(), "^")),
                Key("x", () => Insert(Target(), "x")), Key("ABC", () =>
                {
                    var field = Target();
                    var keyboard = GetSystemService(InputMethodService) as InputMethodManager;
                    keyboard?.ShowSoftInput(field, ShowFlags.Implicit);
                }));
        }
        return pad;
    }
}
