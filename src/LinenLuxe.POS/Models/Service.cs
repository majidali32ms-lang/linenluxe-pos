using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LinenLuxe.POS.Models;

/// <summary>
/// Maps the "services" table (the web app's 22-item catalog).
/// Used as the database connection test; the full POS price list
/// lives in pos_items.
/// </summary>
[Table("services")]
public class Service : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("slug")]
    public string Slug { get; set; } = "";

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("unit")]
    public string? Unit { get; set; }

    [Column("base_price")]
    public decimal? BasePrice { get; set; }

    [Column("price_label")]
    public string? PriceLabel { get; set; }

    [Column("is_quote_only")]
    public bool IsQuoteOnly { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }
}
