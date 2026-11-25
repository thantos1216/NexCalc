using System.Globalization;

namespace NexCalc.Core;

public enum CarpenterTool { Length, Area, Volume, BoardFeet, Diagonal, MaterialQuantity }

public static class Measurements
{
    private static readonly string[][] ImperialPrompts =
    [
        ["Feet", "Inches (fraction or decimal)"], ["Length (ft)", "Width (ft)"],
        ["Length (ft)", "Width (ft)", "Depth (ft)"], ["Thickness (in)", "Width (in)", "Length (ft)"],
        ["Length (ft)", "Width (ft)"], ["Area to cover (ft²)", "Coverage per item (ft²)", "Waste (%)"]
    ];
    private static readonly string[][] MetricPrompts =
    [
        ["Length"], ["Length (m)", "Width (m)"], ["Length (m)", "Width (m)", "Depth (m)"],
        ["Thickness (mm)", "Width (mm)", "Length (m)"], ["Length (m)", "Width (m)"],
        ["Area to cover (m²)", "Coverage per item (m²)", "Waste (%)"]
    ];

    public static IReadOnlyList<string> Prompts(CarpenterTool tool, bool metric) =>
        (metric ? MetricPrompts : ImperialPrompts)[(int)tool];

    public static string Carpenter(CarpenterTool tool, bool metric, int lengthUnit, double a, double b, double c)
    {
        double[] metresPerUnit = [0.001, 0.01, 1];
        const double metresPerFoot = 0.3048;
        const double metresPerInch = 0.0254;
        double lengthMetres = a * metresPerUnit[lengthUnit];
        double diagonal = double.Hypot(a, b);
        return (tool, metric) switch
        {
            (CarpenterTool.Length, true) => $"{Number(lengthMetres * 1000)} mm · {Number(lengthMetres * 100)} cm · {Number(lengthMetres)} m\n{FeetAndInches(lengthMetres / metresPerInch)}",
            (CarpenterTool.Length, false) => $"{FeetAndInches(a * 12 + b)}\n{Number(a * 12 + b)} in · {Number((a * 12 + b) * 2.54)} cm",
            (CarpenterTool.Area, true) => $"{Number(a * b)} m²\n{Number(a * b / 0.09290304)} ft²",
            (CarpenterTool.Area, false) => $"{Number(a * b)} ft²\n{Number(a * b * 0.09290304)} m²",
            (CarpenterTool.Volume, true) => $"{Number(a * b * c)} m³\n{Number(a * b * c / 0.028316846592)} ft³",
            (CarpenterTool.Volume, false) => $"{Number(a * b * c)} ft³\n{Number(a * b * c / 27)} yd³",
            (CarpenterTool.BoardFeet, true) => $"{Number(a * b * c / 1_000_000)} m³\n{Number(a * b * c / 2359.737216)} board ft",
            (CarpenterTool.BoardFeet, false) => $"{Number(a * b * c / 12)} board ft",
            (CarpenterTool.Diagonal, true) => $"{Number(diagonal)} m\n{FeetAndInches(diagonal / metresPerInch)}",
            (CarpenterTool.Diagonal, false) => $"{Number(diagonal)} ft · {Number(diagonal * metresPerFoot)} m\n{FeetAndInches(diagonal * 12)}",
            _ when b > 0 => $"{Number(Math.Ceiling(a / b * (1 + c / 100)))} items",
            _ => "Coverage per item must be greater than zero."
        };
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

    public static bool TryParse(string? text, out double value)
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

    public static string Number(double value) => double.IsFinite(value)
        ? value.ToString("G12", CultureInfo.CurrentCulture) : "Out of range";
}
