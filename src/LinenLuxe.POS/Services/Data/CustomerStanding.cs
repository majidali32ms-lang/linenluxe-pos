namespace LinenLuxe.POS.Services.Data;

/// <summary>
/// How well the shop knows a customer, worked out from the orders they have
/// actually placed rather than a flag someone has to remember to tick. A flag goes
/// stale the first busy Saturday; a count cannot.
///
/// It is here rather than on the customer record for the same reason: the answer
/// changes every time they order, and storing it would mean writing to the customer
/// row on every sale for something the order rows already say.
/// </summary>
public static class CustomerStanding
{
    /// <summary>Orders that earn "regular". Five is a habit, not a coincidence.</summary>
    public const int RegularFrom = 5;

    public static string Badge(int orders) => orders switch
    {
        0 => "NEW",
        < RegularFrom => "RETURNING",
        _ => "REGULAR",
    };

    /// <summary>Said in full where there is room for a sentence.</summary>
    public static string Describe(int orders) => orders switch
    {
        0 => "First order — never ordered before",
        1 => "Has ordered once before",
        < RegularFrom => $"Has ordered {orders} times before",
        _ => $"Regular customer — {orders} orders",
    };

    /// <summary>True when this customer has never ordered, and the first-order discount applies.</summary>
    public static bool IsFirstOrder(int previousOrders) => previousOrders == 0;
}
