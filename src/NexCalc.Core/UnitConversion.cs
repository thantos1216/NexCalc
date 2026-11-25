namespace NexCalc.Core;

public static class UnitConversion
{
    // Each unit maps to its category's base unit by value * Scale + Offset.
    public sealed record Unit(string Name, double Scale, double Offset = 0);
    public sealed record UnitCategory(string Name, Unit[] Units);

    public static IReadOnlyList<UnitCategory> Categories { get; } =
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

    public static double Convert(double value, Unit source, Unit target) => source == target ? value :
        (value * source.Scale + source.Offset - target.Offset) / target.Scale;
}
