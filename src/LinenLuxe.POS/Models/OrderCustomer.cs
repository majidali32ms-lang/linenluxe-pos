namespace LinenLuxe.POS.Models;

/// <summary>
/// The customer attached to an order being built. It is either a registered
/// website customer or a walk-in — this wraps both so the rest of the POS
/// doesn't care which.
/// </summary>
public sealed class OrderCustomer
{
    /// <summary>Set when the customer has a website account.</summary>
    public Guid? ProfileId { get; init; }

    /// <summary>Set when the customer is a saved walk-in.</summary>
    public Guid? PosCustomerId { get; init; }

    public required string Name { get; init; }
    public string? Phone { get; init; }

    public bool IsRegistered => ProfileId is not null;

    // ----- Display -----
    public string Badge => IsRegistered ? "WEBSITE" : "WALK-IN";
    public string PhoneDisplay => string.IsNullOrWhiteSpace(Phone) ? "no phone" : Phone!;

    public static OrderCustomer FromProfile(Profile p) => new()
    {
        ProfileId = Guid.Parse(p.Id),
        Name = string.IsNullOrWhiteSpace(p.FullName) ? (p.Email ?? "Customer") : p.FullName!,
        Phone = p.Phone,
    };

    public static OrderCustomer FromWalkIn(PosCustomer c) => new()
    {
        PosCustomerId = c.Id,
        Name = c.FullName,
        Phone = c.Phone,
    };
}
