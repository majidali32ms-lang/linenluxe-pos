namespace LinenLuxe.POS.Services.Configuration;

/// <summary>
/// Strongly-typed application configuration, loaded from the .env file at startup.
/// The Supabase publishable key is safe for client apps — the database is protected
/// by Row Level Security policies. Secrets are never hard-coded or committed.
/// </summary>
public sealed class AppConfig
{
    public string SupabaseUrl { get; init; } = "";
    public string SupabaseKey { get; init; } = "";

    /// <summary>True only when both Supabase values are present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SupabaseUrl) && !string.IsNullOrWhiteSpace(SupabaseKey);

    /// <summary>An empty config used before startup / when no .env is found.</summary>
    public static AppConfig Empty { get; } = new();

    /// <summary>Load configuration from the nearest .env file.</summary>
    public static AppConfig Load()
    {
        var values = EnvLoader.Load();
        return new AppConfig
        {
            SupabaseUrl = values.GetValueOrDefault("SUPABASE_URL", ""),
            SupabaseKey = values.GetValueOrDefault("SUPABASE_KEY", ""),
        };
    }
}
