using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LinenLuxe.POS.Models;

/// <summary>
/// Maps the "profiles" table (1 row per auth user). Role decides POS access:
/// only 'staff' and 'admin' may sign in to the desktop app.
/// </summary>
[Table("profiles")]
public class Profile : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = "";

    [Column("email")]
    public string? Email { get; set; }

    [Column("full_name")]
    public string? FullName { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    /// <summary>'customer' | 'staff' | 'admin' (postgres enum user_role).</summary>
    [Column("role")]
    public string Role { get; set; } = "customer";

    public bool IsStaff => Role is "staff" or "admin";
    public bool IsAdmin => Role is "admin";
}
