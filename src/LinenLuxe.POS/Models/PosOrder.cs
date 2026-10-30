using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LinenLuxe.POS.Models;

/// <summary>
/// A counter order (table "pos_orders"), kept separate from the website's orders
/// and numbered P00001, P00002 …
///
/// order_number, created_at and updated_at are filled by the database, so they
/// are ignored on insert — sending our own empty values would override the
/// defaults and, for order_number, break the P-number sequence outright.
///
/// Every money figure is stored, not recomputed, so an invoice reprinted months
/// later still shows exactly what the customer agreed to at the counter.
/// </summary>
[Table("pos_orders")]
public class PosOrder : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    // Never written, in either direction: the database's P-sequence produces it on
    // insert, and an order must keep the number its invoice already carries.
    [Column("order_number", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public string OrderNumber { get; set; } = "";

    // Exactly one of these two is set — the database enforces it.
    [Column("profile_id")]
    public Guid? ProfileId { get; set; }

    [Column("pos_customer_id")]
    public Guid? PosCustomerId { get; set; }

    // Snapshot of who the order was for, so later edits to the customer record
    // can't rewrite a printed invoice.
    [Column("customer_name")]
    public string CustomerName { get; set; } = "";

    [Column("customer_phone")]
    public string? CustomerPhone { get; set; }

    /// <summary>Reuses the website's status names: 'booked' to start.</summary>
    [Column("status")]
    public string Status { get; set; } = "booked";

    [Column("is_express")]
    public bool IsExpress { get; set; }

    /// <summary>'cod' | 'online' | 'card' — see supabase/03_payment_method_card.sql.</summary>
    [Column("payment_method")]
    public string PaymentMethod { get; set; } = "cod";

    [Column("is_paid")]
    public bool IsPaid { get; set; }

    [Column("vat_enabled")]
    public bool VatEnabled { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    // ----- Money (AED) -----
    [Column("subtotal")]
    public decimal Subtotal { get; set; }

    [Column("express_surcharge")]
    public decimal ExpressSurcharge { get; set; }

    [Column("discount")]
    public decimal Discount { get; set; }

    [Column("vat_amount")]
    public decimal VatAmount { get; set; }

    [Column("total")]
    public decimal Total { get; set; }

    /// <summary>Which staff member took the order.</summary>
    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    // Also never written: the database sets created_at once and keeps updated_at
    // current with a trigger. Sending them on update would push our own copy back
    // and could rewrite when the order was taken.
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }

    // ----- Display helpers for the orders list -----
    //
    // Every one is [JsonIgnore]. This class is written back to the database, and
    // the Supabase client serialises ALL public properties, not just the mapped
    // ones — an unmarked helper is sent as a column name and PostgREST rejects the
    // whole write with "Could not find the 'CreatedDisplay' column".

    /// <summary>Postgres hands timestamps back in UTC; staff read the shop's clock.</summary>
    [JsonIgnore]
    public DateTime CreatedLocal => DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToLocalTime();

    [JsonIgnore]
    public string CreatedDisplay => CreatedLocal.ToString("d MMM yyyy  ·  HH:mm");

    [JsonIgnore]
    public string TotalDisplay => $"AED {Total:0.00}";

    [JsonIgnore]
    public string PhoneDisplay => string.IsNullOrWhiteSpace(CustomerPhone) ? "no phone" : CustomerPhone!;

    [JsonIgnore]
    public string PaymentDisplay => PaymentMethod switch
    {
        "online" => "Online",
        "card" => "Card",
        _ => "COD",
    };

    [JsonIgnore]
    public string PaidBadge => IsPaid ? "PAID" : "PENDING";

    /// <summary>"Out for delivery" rather than "out_for_delivery".</summary>
    [JsonIgnore]
    public string StatusLabel => OrderStatus.Label(Status);

    [JsonIgnore]
    public bool IsDelivered => Status == OrderStatus.Delivered;

    [JsonIgnore]
    public bool IsPending => !IsPaid;

    [JsonIgnore]
    public bool IsStandard => !IsExpress;
}
