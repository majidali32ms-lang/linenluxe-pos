using System.Text;

namespace LinenLuxe.POS.Services.Invoicing;

/// <summary>
/// Writes an AED amount out in words for the invoice, e.g. 254.10 becomes
/// "Two Hundred Fifty-Four Dirhams and Ten Fils Only."
///
/// This is the line a customer checks the figures against, so it is spelled from
/// the stored total rather than anything re-derived.
/// </summary>
public static class AmountInWords
{
    private static readonly string[] Ones =
    [
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
        "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
        "Seventeen", "Eighteen", "Nineteen",
    ];

    private static readonly string[] Tens =
    [
        "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety",
    ];

    public static string Aed(decimal amount)
    {
        if (amount < 0) return "Zero Dirhams Only.";

        // Round first: the fils shown must match the fils charged.
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        var dirhams = (long)decimal.Truncate(amount);
        var fils = (int)((amount - dirhams) * 100);

        var text = new StringBuilder();
        text.Append(Spell(dirhams));
        text.Append(dirhams == 1 ? " Dirham" : " Dirhams");

        if (fils > 0)
        {
            text.Append(" and ");
            text.Append(Spell(fils));
            text.Append(fils == 1 ? " Fil" : " Fils");
        }

        text.Append(" Only.");
        return text.ToString();
    }

    private static string Spell(long value)
    {
        if (value < 20) return Ones[value];

        if (value < 100)
        {
            var tens = Tens[value / 10];
            var rest = value % 10;
            // Hyphenated, as amounts in words conventionally are: "Fifty-Four".
            return rest == 0 ? tens : $"{tens}-{Ones[rest]}";
        }

        if (value < 1_000) return Compose(value, 100, "Hundred");
        if (value < 1_000_000) return Compose(value, 1_000, "Thousand");
        if (value < 1_000_000_000) return Compose(value, 1_000_000, "Million");
        return Compose(value, 1_000_000_000, "Billion");
    }

    private static string Compose(long value, long unit, string name)
    {
        var whole = Spell(value / unit) + " " + name;
        var rest = value % unit;
        return rest == 0 ? whole : whole + " " + Spell(rest);
    }
}
