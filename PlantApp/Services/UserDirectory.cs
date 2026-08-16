using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlantApp.Data;
using PlantApp.Models;

namespace PlantApp.Services;

/// <summary>Rollnamn som används i hela appen (matchar [Authorize(Roles = ...)]).</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Plantbas = "Plantbas";
    public const string Plantsattare = "Plantsattare";
    public const string Inventerare = "Inventerare";

    public static readonly IReadOnlyList<string> Alla =
    [
        SuperAdmin, Admin, Plantbas, Plantsattare, Inventerare
    ];

    /// <summary>Roller som en organisations-admin får skapa (inte SuperAdmin).</summary>
    public static readonly IReadOnlyList<string> OrganisationsRoller =
    [
        Admin, Plantbas, Plantsattare, Inventerare
    ];
}

/// <summary>
/// Databasbaserad användarkatalog ovanpå ASP.NET Core Identity. Ersätter den tidigare
/// hårdkodade demo-lagringen. Lösenord hashas av Identity. Borttagning sker som
/// soft-delete (inaktivering) för att bevara historik (löner/inventeringar/tilldelningar
/// kopplas via användarnamn).
///
/// Registreras som singleton och håller en lättviktig in-memory-cache av visningsnamn och
/// tilldelningsbara användare så att Razor-markup kan läsa synkront. Cachen laddas vid
/// uppstart och uppdateras efter varje skrivning.
/// </summary>
public sealed class UserDirectory
{
    private readonly IServiceScopeFactory _scopeFactory;

    // Cache för synkrona läsningar från markup.
    private readonly ConcurrentDictionary<string, string> _displayNames =
        new(StringComparer.OrdinalIgnoreCase);
    private volatile IReadOnlyList<AppUserInfo> _appointable = [];
    private volatile IReadOnlyList<AppUserInfo> _inventerare = [];

    public UserDirectory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>Lättviktig vy av en användare för UI/tilldelning.</summary>
    public sealed record AppUserInfo(
        string UserName, string DisplayName, string Email, string Role, bool ArAktiv, int? OrganisationId);

    /// <summary>
    /// Laddar om cachen från databasen. Anropas vid uppstart och efter skrivningar.
    /// </summary>
    public async Task RefreshCacheAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantAppDbContext>();

        var users = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(u => new AppUserInfo(u.UserName!, u.DisplayName, u.Email ?? "", u.Roll, u.ArAktiv, u.OrganisationId))
            .ToListAsync(ct);

        _displayNames.Clear();
        foreach (var u in users)
        {
            _displayNames[u.UserName] = u.DisplayName;
        }

        _appointable = users
            .Where(u => u.ArAktiv && u.Role is AppRoles.Plantbas or AppRoles.Plantsattare)
            .OrderBy(u => u.DisplayName)
            .ToList();

        _inventerare = users
            .Where(u => u.ArAktiv && u.Role == AppRoles.Inventerare)
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    // --- Synkrona läsningar (från cache) ---

    /// <summary>Visningsnamn för ett användarnamn, eller själva användarnamnet om okänt.</summary>
    public string GetDisplayName(string userName) =>
        userName is not null && _displayNames.TryGetValue(userName, out var namn) ? namn : userName ?? string.Empty;

    /// <summary>Aktiva plantbasar och plantsättare som kan tilldelas ett delområde.</summary>
    public IReadOnlyList<AppUserInfo> GetAppointableUsers() => _appointable;

    /// <summary>Aktiva inventerare som kan tilldelas en inventeringsomgång.</summary>
    public IReadOnlyList<AppUserInfo> GetInventerare() => _inventerare;

    // --- Autentisering ---

    /// <summary>
    /// Validerar e-post och lösenord mot Identity. Returnerar användaren om
    /// uppgifterna stämmer och kontot är aktivt, annars null.
    /// </summary>
    public async Task<AppUser?> ValidateAsync(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || !user.ArAktiv)
        {
            return null;
        }

        return await userManager.CheckPasswordAsync(user, password) ? user : null;
    }

    // --- Admin-operationer ---

    /// <summary>Lista alla användare (inkl. inaktiva) för adminöversikten.</summary>
    /// <summary>
    /// Lista användare för adminöversikten. När <paramref name="organisationId"/> anges
    /// (organisations-admin) returneras endast den organisationens användare; null
    /// (super-admin) returnerar alla.
    /// </summary>
    public async Task<IReadOnlyList<AppUserInfo>> ListaAllaAsync(
        int? organisationId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantAppDbContext>();

        var query = db.Users.IgnoreQueryFilters().AsNoTracking();
        if (organisationId is not null)
        {
            query = query.Where(u => u.OrganisationId == organisationId);
        }

        return await query
            .OrderBy(u => u.ArAktiv ? 0 : 1)
            .ThenBy(u => u.Roll)
            .ThenBy(u => u.DisplayName)
            .Select(u => new AppUserInfo(u.UserName!, u.DisplayName, u.Email ?? "", u.Roll, u.ArAktiv, u.OrganisationId))
            .ToListAsync(ct);
    }

    /// <summary>Skapar en ny användare med given roll, e-post och lösenord. Returnerar felmeddelanden eller tomt vid framgång.</summary>
    public async Task<IReadOnlyList<string>> SkapaAnvandareAsync(
        string email, string displayName, string roll, string password, int? organisationId)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return ["Ange en e-postadress."];
        }
        if (!AppRoles.Alla.Contains(roll))
        {
            return ["Ogiltig roll."];
        }
        if (roll != AppRoles.SuperAdmin && organisationId is null)
        {
            return ["En organisation måste anges för den här rollen."];
        }

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Trim() : displayName.Trim(),
            Roll = roll,
            OrganisationId = roll == AppRoles.SuperAdmin ? null : organisationId,
            ArAktiv = true,
            SkapadDatum = DateTime.UtcNow,
        };

        // En användare med samma e-post kan finnas i en annan organisation (eller vara
        // bortfiltrerad av tenant-query-filtret), vilket gör att Identity inte upptäcker
        // dubbletten och databasens unika index i stället kastar. Kontrollera explicit och
        // fånga eventuellt kvarstående databasfel så att sidan inte kraschar.
        if (await userManager.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.NormalizedEmail == userManager.NormalizeEmail(email.Trim())))
        {
            return ["En användare med den e-postadressen finns redan."];
        }

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException)
        {
            return ["En användare med den e-postadressen finns redan."];
        }

        if (!result.Succeeded)
        {
            return result.Errors.Select(e => e.Description).ToList();
        }

        await userManager.AddToRoleAsync(user, roll);
        await RefreshCacheAsync();
        return [];
    }

    /// <summary>Aktiverar eller inaktiverar (soft-delete) ett konto.</summary>
    public async Task<bool> SattAktivAsync(string userName, bool aktiv)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return false;
        }

        user.ArAktiv = aktiv;
        user.InaktiveradDatum = aktiv ? null : DateTime.UtcNow;
        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await RefreshCacheAsync();
        }
        return result.Succeeded;
    }

    /// <summary>Ändrar rollen på en befintlig användare (organisations-admin).</summary>
    public async Task<IReadOnlyList<string>> AndraRollAsync(string userName, string nyRoll)
    {
        if (!AppRoles.OrganisationsRoller.Contains(nyRoll))
        {
            return ["Ogiltig roll."];
        }

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return ["Användaren hittades inte."];
        }

        if (user.Roll == nyRoll)
        {
            return [];
        }

        // Uppdatera både domänfältet (Roll) och Identity-rollmedlemskapet så att
        // [Authorize(Roles = ...)] och cachen hålls i synk.
        var befintligaRoller = await userManager.GetRolesAsync(user);
        if (befintligaRoller.Count > 0)
        {
            var borttag = await userManager.RemoveFromRolesAsync(user, befintligaRoller);
            if (!borttag.Succeeded)
            {
                return borttag.Errors.Select(e => e.Description).ToList();
            }
        }

        var tillagg = await userManager.AddToRoleAsync(user, nyRoll);
        if (!tillagg.Succeeded)
        {
            return tillagg.Errors.Select(e => e.Description).ToList();
        }

        user.Roll = nyRoll;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return result.Errors.Select(e => e.Description).ToList();
        }

        await RefreshCacheAsync();
        return [];
    }

    /// <summary>Sätter ett nytt lösenord för en användare (admin-återställning).</summary>
    public async Task<IReadOnlyList<string>> AndraLosenordAsync(string userName, string nyttLosenord)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return ["Användaren hittades inte."];
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, nyttLosenord);
        return result.Succeeded ? [] : result.Errors.Select(e => e.Description).ToList();
    }

    // --- Organisationer (super-admin) ---

    /// <summary>Lättviktig vy av en organisation för super-admin.</summary>
    public sealed record OrganisationInfo(int Id, string Namn, bool ArAktiv, DateTime SkapadDatum, int AntalAnvandare);

    /// <summary>Lista alla organisationer med antal användare (super-admin).</summary>
    public async Task<IReadOnlyList<OrganisationInfo>> ListaOrganisationerAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantAppDbContext>();

        return await db.Organisationer
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(o => o.ArAktiv ? 0 : 1)
            .ThenBy(o => o.Namn)
            .Select(o => new OrganisationInfo(
                o.Id, o.Namn, o.ArAktiv, o.SkapadDatum,
                db.Users.IgnoreQueryFilters().Count(u => u.OrganisationId == o.Id)))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Skapar en ny organisation med en tillhörande ekonomiinställning och en första
    /// organisations-admin. Returnerar felmeddelanden eller tomt vid framgång.
    /// </summary>
    public async Task<IReadOnlyList<string>> SkapaOrganisationAsync(
        string organisationsNamn, string adminEpost, string adminNamn, string adminLosenord)
    {
        if (string.IsNullOrWhiteSpace(organisationsNamn))
        {
            return ["Ange ett organisationsnamn."];
        }
        if (string.IsNullOrWhiteSpace(adminEpost))
        {
            return ["Ange en e-postadress för organisationens admin."];
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantAppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (await db.Organisationer.IgnoreQueryFilters().AnyAsync(o => o.Namn == organisationsNamn.Trim()))
        {
            return ["En organisation med det namnet finns redan."];
        }

        var org = new Organisation
        {
            Namn = organisationsNamn.Trim(),
            ArAktiv = true,
            SkapadDatum = DateTime.UtcNow,
        };
        db.Organisationer.Add(org);
        await db.SaveChangesAsync();

        db.EkonomiInstallningar.Add(new EkonomiInstallning
        {
            OrganisationId = org.Id,
            StandardErsattningsTyp = ErsattningsTyp.HelAckord,
        });
        await db.SaveChangesAsync();

        var admin = new AppUser
        {
            UserName = adminEpost.Trim(),
            Email = adminEpost.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(adminNamn) ? adminEpost.Trim() : adminNamn.Trim(),
            Roll = AppRoles.Admin,
            OrganisationId = org.Id,
            ArAktiv = true,
            SkapadDatum = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(admin, adminLosenord);
        if (!result.Succeeded)
        {
            // Rulla tillbaka organisationen om admin inte kunde skapas.
            db.Organisationer.Remove(org);
            await db.SaveChangesAsync();
            return result.Errors.Select(e => e.Description).ToList();
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        await RefreshCacheAsync();
        return [];
    }

    /// <summary>Aktiverar/inaktiverar en organisation (super-admin).</summary>
    public async Task<bool> SattOrganisationAktivAsync(int organisationId, bool aktiv)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantAppDbContext>();

        var org = await db.Organisationer.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == organisationId);
        if (org is null)
        {
            return false;
        }

        org.ArAktiv = aktiv;
        await db.SaveChangesAsync();
        return true;
    }
}
