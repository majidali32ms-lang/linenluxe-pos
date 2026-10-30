using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LinenLuxe.POS.Models;

/// <summary>
/// A customer the POS owns (table "pos_customers") — as opposed to a website
/// account, which lives in the shared "profiles" table and is not ours to edit.
///
/// Two kinds, told apart by <see cref="CustomerType"/>:
///   * walk_in  — someone at the counter. A name, usually a phone, that's it.
///   * business — a hotel, salon or gym. A company with someone to ring, credit
///                terms and often its own TRN, because it needs a tax invoice.
///
/// Display helpers below are all [JsonIgnore]: this class is written back to the
/// database, and the Supabase client sends every public property, so an unmarked
/// one goes out as a column name and PostgREST rejects the whole write.
/// </summary>
[Table("pos_customers")]
public class PosCustomer : BaseModel
{
    public const string WalkIn = "walk_in";
    public const string Business = "business";

    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    /// <summary>Person's name, or the company's name for a business account.</summary>
    [Column("full_name")]
    public string FullName { get; set; } = "";

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    /// <summary>'walk_in' | 'business'.</summary>
    [Column("customer_type")]
    public string CustomerType { get; set; } = WalkIn;

    /// <summary>Who to ask for when ringing a company. Empty for a walk-in.</summary>
    [Column("contact_person")]
    public string? ContactPerson { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    /// <summary>Kept apart from the address so deliveries can be grouped by round.</summary>
    [Column("area")]
    public string? Area { get; set; }

    /// <summary>The customer's own tax number — not the shop's.</summary>
    [Column("trn")]
    public string? Trn { get; set; }

    /// <summary>Days to pay. 0 means due on collection, as a walk-in pays.</summary>
    [Column("credit_days")]
    public int CreditDays { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }

    // ----- Display helpers -----

    [JsonIgnore]
    public bool IsBusiness => CustomerType == Business;

    [JsonIgnore]
    public bool IsWalkIn => !IsBusiness;

    [JsonIgnore]
    public string TypeBadge => IsBusiness ? "BUSINESS" : "WALK-IN";

    [JsonIgnore]
    public string PhoneDisplay => string.IsNullOrWhiteSpace(Phone) ? "no phone" : Phone!;

    /// <summary>"Al Furjan · Villa 5" — whichever parts exist, in one line.</summary>
    [JsonIgnore]
    public string WhereDisplay
    {
        get
        {
            var parts = new[] { Area, Address }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim());
            var joined = string.Join("  ·  ", parts);
            return joined.Length > 0 ? joined : "no address";
        }
    }

    /// <summary>"Due on collection" or "30 days" — how this customer pays.</summary>
    [JsonIgnore]
    public string TermsDisplay => CreditDays <= 0 ? "Due on collection" : $"{CreditDays} days";
}
