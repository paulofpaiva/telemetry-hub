using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TelemetryHub.Domain.Readings;

/// <summary>
/// Size of an aggregation window, written as a number plus a unit: "30s", "5m", "1h", "1d".
/// Parsing is strict because the value ends up inside a SAMPLE BY clause, which cannot be
/// sent as a query parameter.
/// </summary>
public readonly record struct SampleInterval
{
    private SampleInterval(int amount, char unit)
    {
        Amount = amount;
        Unit = unit;
    }

    public int Amount { get; }

    public char Unit { get; }

    public TimeSpan Duration => Unit switch
    {
        's' => TimeSpan.FromSeconds(Amount),
        'm' => TimeSpan.FromMinutes(Amount),
        'h' => TimeSpan.FromHours(Amount),
        _ => TimeSpan.FromDays(Amount),
    };

    public static bool TryParse([NotNullWhen(true)] string? value, out SampleInterval interval)
    {
        interval = default;

        if (value is null || value.Length is < 2 or > 5)
        {
            return false;
        }

        var unit = value[^1];
        if (unit is not ('s' or 'm' or 'h' or 'd'))
        {
            return false;
        }

        var digits = value.AsSpan(0, value.Length - 1);
        foreach (var character in digits)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        var amount = int.Parse(digits, CultureInfo.InvariantCulture);
        if (amount == 0)
        {
            return false;
        }

        interval = new SampleInterval(amount, unit);
        return true;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Amount}{Unit}");
}
