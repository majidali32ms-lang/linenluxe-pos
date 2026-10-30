namespace LinenLuxe.POS.Services.Configuration;

/// <summary>
/// Rules the shop has decided on, as opposed to rules the tax authority has.
///
/// They live in one named place rather than scattered through the screens so that
/// changing one is a single edit, and so that a Settings screen has one file to
/// lift them out of and put in front of the owner. Until there is one, changing a
/// figure here and rebuilding is the way to change it.
/// </summary>
public static class ShopPolicy
{
    /// <summary>
    /// Taken off a customer's very first order, automatically. The point of it is
    /// the second visit, so it is applied by the till rather than left to whoever
    /// is at the counter to remember and offer.
    /// </summary>
    public const decimal FirstOrderDiscountRate = 0.10m;

    /// <summary>"10%" — for the line that tells the customer why the price dropped.</summary>
    public static string FirstOrderDiscountPercent =>
        (FirstOrderDiscountRate * 100).ToString("0.##") + "%";
}
