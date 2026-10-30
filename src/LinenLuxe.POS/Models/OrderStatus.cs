namespace LinenLuxe.POS.Models;

/// <summary>
/// The fixed six-step order lifecycle, ported from the website's
/// src/lib/orderStatus.js so both apps speak the same language.
///
/// These keys are the values of the shared <c>order_status</c> enum in Postgres —
/// they are not ours to rename or extend. Anything not on this list is rejected
/// by the database, which is why "cancelled" is absent: voiding an order needs a
/// change to the enum the website also uses.
/// </summary>
public static class OrderStatus
{
    public const string Booked = "booked";
    public const string PickedUp = "picked_up";
    public const string Processing = "processing";
    public const string Ready = "ready";
    public const string OutForDelivery = "out_for_delivery";
    public const string Delivered = "delivered";

    /// <summary>In order. A counter order starts at Booked and ends at Delivered.</summary>
    public static readonly (string Key, string Label)[] Steps =
    [
        (Booked, "Booked"),
        (PickedUp, "Picked up"),
        (Processing, "Processing"),
        (Ready, "Ready"),
        (OutForDelivery, "Out for delivery"),
        (Delivered, "Delivered"),
    ];

    public static string Label(string key)
    {
        foreach (var step in Steps)
            if (step.Key == key) return step.Label;
        return key;   // an unknown value still shows something rather than nothing
    }

    public static int IndexOf(string key)
    {
        for (var i = 0; i < Steps.Length; i++)
            if (Steps[i].Key == key) return i;
        return -1;
    }

    /// <summary>The next step, or null at the end of the line.</summary>
    public static string? Next(string key)
    {
        var i = IndexOf(key);
        return i >= 0 && i < Steps.Length - 1 ? Steps[i + 1].Key : null;
    }

    /// <summary>The previous step, for undoing a mis-click.</summary>
    public static string? Previous(string key)
    {
        var i = IndexOf(key);
        return i > 0 ? Steps[i - 1].Key : null;
    }
}
