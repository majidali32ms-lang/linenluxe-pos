using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Data;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// One row in the Customers list.
///
/// The screen shows two different things side by side: customers the POS owns
/// (walk-ins and business accounts in <c>pos_customers</c>) and people with a
/// website account (the shared <c>profiles</c> table). This wraps both so the list
/// can bind to one shape.
///
/// Website accounts are deliberately read-only here. <c>profiles</c> belongs to the
/// live web app, and the first rule of this project is to leave the website's
/// database alone — a name edited here would change what the customer sees when
/// they sign in.
/// </summary>
public sealed class CustomerListItem
{
    private CustomerListItem() { }

    /// <summary>Set when this row is a POS-owned customer.</summary>
    public PosCustomer? Pos { get; private init; }

    /// <summary>Set when this row is a website account.</summary>
    public Profile? Website { get; private init; }

    public string Name { get; private init; } = "";
    public string? Phone { get; private init; }
    public string Badge { get; private init; } = "";

    /// <summary>Only POS-owned customers can be changed from this screen.</summary>
    public bool IsEditable => Pos is not null;

    public bool IsWebsite => Website is not null;
    public bool IsBusiness => Pos?.IsBusiness == true;

    public string PhoneDisplay => string.IsNullOrWhiteSpace(Phone) ? "no phone" : Phone!;

    // ----- Filled in from the orders once they are loaded -----

    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal Unpaid { get; set; }

    /// <summary>NEW · RETURNING · REGULAR, from the orders themselves.</summary>
    public string StandingBadge => CustomerStanding.Badge(OrderCount);

    /// <summary>Worth pointing out in the list; "returning" is the ordinary case.</summary>
    public bool StandingWorthShowing => OrderCount == 0 || OrderCount >= CustomerStanding.RegularFrom;

    /// <summary>
    /// Digits only, so 050 900 0000 and 0509000000 are recognised as the same
    /// person. The country code is dropped for the same reason: staff type
    /// +971 50 … and 050 … for one phone without thinking about it.
    /// </summary>
    public string PhoneKey
    {
        get
        {
            var digits = new string((Phone ?? "").Where(char.IsDigit).ToArray());
            if (digits.StartsWith("971")) digits = "0" + digits[3..];
            return digits;
        }
    }

    public string? Email { get; private init; }

    /// <summary>
    /// The second line of a row. For the shop's own customers that is where they
    /// are; for a website account it is the email they sign in with, which is the
    /// only thing that tells two accounts apart when the same person has opened
    /// more than one. "Held on the website account" said nothing and made three
    /// separate logins look like one name entered three times.
    /// </summary>
    public string WhereDisplay => Pos is not null
        ? Pos.WhereDisplay
        : string.IsNullOrWhiteSpace(Email) ? "website account" : Email!;

    /// <summary>Matches an order to this customer, whichever kind it is.</summary>
    public bool Owns(PosOrder order) => Pos is not null
        ? order.PosCustomerId == Pos.Id
        : Website is not null && order.ProfileId?.ToString() == Website.Id;

    public static CustomerListItem FromPos(PosCustomer c) => new()
    {
        Pos = c,
        Name = c.FullName,
        Phone = c.Phone,
        Email = c.Email,
        Badge = c.TypeBadge,
    };

    public static CustomerListItem FromWebsite(Profile p) => new()
    {
        Website = p,
        Name = string.IsNullOrWhiteSpace(p.FullName) ? (p.Email ?? "Customer") : p.FullName!,
        Phone = p.Phone,
        Email = p.Email,
        Badge = "WEBSITE",
    };
}
