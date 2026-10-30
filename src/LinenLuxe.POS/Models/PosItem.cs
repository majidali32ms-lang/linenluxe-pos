using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LinenLuxe.POS.Models;

/// <summary>
/// One line of the POS price list (table "pos_items"), imported from the
/// LinenLuxe price list PDF. A null price means that service isn't offered for
/// this item (shown as a dash in the printed list).
/// </summary>
[Table("pos_items")]
public class PosItem : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    /// <summary>'Apparel' | 'Home' | 'Special'.</summary>
    [Column("part")]
    public string Part { get; set; } = "";

    /// <summary>e.g. "Men's Clothing", "Bedding", "Shoe Care".</summary>
    [Column("category")]
    public string Category { get; set; } = "";

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("clean_press_price")]
    public decimal? CleanPressPrice { get; set; }

    [Column("press_only_price")]
    public decimal? PressOnlyPrice { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    // ----- Display helpers (not stored) -----
    //
    // [JsonIgnore] is not decoration. The Supabase client serialises every public
    // property, so an unmarked one goes out as a column name and PostgREST rejects
    // the whole write with PGRST204. Nothing wrote to this table until the price
    // list became editable, which is why it had never bitten here.

    [JsonIgnore]
    public string CleanPressDisplay => CleanPressPrice is { } p ? $"AED {p:0.##}" : "—";

    [JsonIgnore]
    public string PressOnlyDisplay => PressOnlyPrice is { } p ? $"AED {p:0.##}" : "—";

    [JsonIgnore]
    public bool HasPressOnly => PressOnlyPrice is not null;
}
