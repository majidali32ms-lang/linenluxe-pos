using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Data;
using Supabase.Gotrue.Exceptions;

namespace LinenLuxe.POS.Services.Auth;

/// <summary>
/// Staff sign-in against the SAME Supabase Auth the web app uses.
/// Enforces the POS access rule: only 'staff' and 'admin' roles may enter.
/// </summary>
public sealed class AuthService
{
    /// <summary>The signed-in staff profile (null when signed out).</summary>
    public Profile? CurrentProfile { get; private set; }

    /// <summary>
    /// Sign in with email/password, load the profile, and verify staff access.
    /// Throws with a friendly message on any failure.
    /// </summary>
    public async Task<Profile> SignInStaffAsync(string email, string password)
    {
        var db = SupabaseService.Instance;
        await db.InitializeAsync();

        Supabase.Gotrue.Session? session;
        try
        {
            session = await db.Client.Auth.SignIn(email, password);
        }
        catch (GotrueException)
        {
            // Wrong credentials (or unconfirmed account) — keep the message simple for staff.
            throw new InvalidOperationException("Incorrect email or password.");
        }

        var userId = session?.User?.Id;
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("Sign-in failed. Please try again.");

        // RLS: a signed-in user can always read their own profile row.
        var profile = await db.Client
            .From<Profile>()
            .Where(p => p.Id == userId)
            .Single();

        if (profile is null)
        {
            await db.Client.Auth.SignOut();
            throw new InvalidOperationException("No profile found for this account.");
        }

        if (!profile.IsStaff)
        {
            // Customers use the web app / PWA — the POS is staff-only.
            await db.Client.Auth.SignOut();
            throw new UnauthorizedAccessException(
                "This account does not have staff access. Please use a staff or admin account.");
        }

        CurrentProfile = profile;
        return profile;
    }

    /// <summary>Sign out and clear the current profile.</summary>
    public async Task SignOutAsync()
    {
        CurrentProfile = null;
        try { await SupabaseService.Instance.Client.Auth.SignOut(); }
        catch { /* already signed out / offline — safe to ignore */ }
    }
}
