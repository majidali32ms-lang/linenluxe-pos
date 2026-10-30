using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LinenLuxe.POS.Models;

/// <summary>
/// One line of a counter order (table "pos_order_items").
///
/// The name, category and rate are snapshots taken when the line was added, so a
/// later price-list change never rewrites what an old invoice says.
/// </summary>
[Table("pos_order_items")]
public class PosOrderItem : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    /// <summary>Null if the price-list item is ever deleted; the snapshot remains.</summary>
    [Column("pos_item_id")]
    public Guid? PosItemId { get; set; }

    [Column("item_name")]
    public string ItemName { get; set; } = "";

    [Column("category")]
    public string? Category { get; set; }

    /// <summary>'clean_press' | 'press_only'.</summary>
    [Column("service_type")]
    public string ServiceType { get; set; } = "clean_press";

    /// <summary>The rate actually charged.</summary>
    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// What the price list said before inspection, or null if the rate was never
    /// changed. A value means this line was repriced at the counter, and the
    /// invoice prints both figures so the customer can see why.
    /// </summary>
    [Column("est_unit_price")]
    public decimal? EstUnitPrice { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("line_total")]
    public decimal LineTotal { get; set; }

    [Column("created_at", ignoreOnInsert: true)]
    public DateTime CreatedAt { get; set; }
}
