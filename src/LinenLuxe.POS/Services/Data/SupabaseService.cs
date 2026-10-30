using Supabase;
using LinenLuxe.POS.Services.Configuration;

namespace LinenLuxe.POS.Services.Data;

/// <summary>
/// Single shared Supabase client for the whole app (same project/database as the
/// web app). All data access and auth flows go through this service.
/// </summary>
public sealed class SupabaseService
{
    private static SupabaseService? _instance;

    /// <summary>App-wide singleton instance.</summary>
    public static SupabaseService Instance => _instance ??= new SupabaseService(App.Config);

    /// <summary>The underlying Supabase client (Auth, Postgrest, Realtime).</summary>
    public Client Client { get; }

    private bool _initialized;

    private SupabaseService(AppConfig config)
    {
        Client = new Client(config.SupabaseUrl, config.SupabaseKey, new SupabaseOptions
        {
            AutoRefreshToken = true,      // keep the staff session alive during the workday
            AutoConnectRealtime = false,  // realtime subscriptions come later (order queue)
        });
    }

    /// <summary>Initialize once before first use (safe to call repeatedly).</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await Client.InitializeAsync();
        _initialized = true;
    }
}
