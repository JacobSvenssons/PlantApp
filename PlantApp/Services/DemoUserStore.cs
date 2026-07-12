namespace PlantApp.Services;

/// <summary>
/// Lättviktig demo-användarlagring för cookie-autentisering. Innehåller seedade
/// demo-användare (admin, plantbas, två plantsättare och inventerare) med kända lösenord.
/// OBS: detta är medvetet enkelt för demo – ersätt med ASP.NET Core Identity mot databas
/// för produktion (riktig lösenordshashning, användarhantering m.m.).
/// </summary>
public sealed class DemoUserStore
{
    /// <summary>Rollnamn som används i hela appen (matchar [Authorize(Roles = ...)]).</summary>
    public const string RoleAdmin = "Admin";
    public const string RolePlantbas = "Plantbas";
    public const string RolePlantsattare = "Plantsattare";
    public const string RoleInventerare = "Inventerare";

    public sealed record DemoUser(string UserName, string Password, string DisplayName, string Role);

    private readonly IReadOnlyList<DemoUser> _users =
    [
        new("admin", "admin123", "Jonas Admin", RoleAdmin),
        new("plantbas", "plantbas123", "Bengt Bas", RolePlantbas),
        new("peter", "plantor123", "Peter Plant", RolePlantsattare),
        new("tina", "plantor123", "Tina Träd", RolePlantsattare),
        new("inventerare", "invent123", "Inger Inventerare", RoleInventerare),
    ];

    /// <summary>
    /// Validerar användarnamn och lösenord (skiftlägesokänsligt användarnamn).
    /// Returnerar användaren om uppgifterna stämmer, annars null.
    /// </summary>
    public DemoUser? Validate(string? userName, string? password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var user = _users.FirstOrDefault(u =>
            string.Equals(u.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (user is null || !string.Equals(user.Password, password, StringComparison.Ordinal))
        {
            return null;
        }

        return user;
    }

    /// <summary>
    /// Användare som kan tilldelas (appointeras) ett delområde i fält, dvs. plantbasar
    /// och plantsättare (admin utför tilldelningen men tilldelas inte själv rutor).
    /// </summary>
    public IReadOnlyList<DemoUser> GetAppointableUsers() =>
        _users
            .Where(u => u.Role is RolePlantbas or RolePlantsattare)
            .ToList();

    /// <summary>
    /// Användare som kan tilldelas en inventeringsomgång, dvs. inventerare (admin
    /// utför tilldelningen men inventerar inte själv).
    /// </summary>
    public IReadOnlyList<DemoUser> GetInventerare() =>
        _users
            .Where(u => u.Role is RoleInventerare)
            .ToList();

    /// <summary>
    /// Visningsnamn för ett användarnamn, eller själva användarnamnet om det inte hittas.
    /// </summary>
    public string GetDisplayName(string userName) =>
        _users.FirstOrDefault(u =>
            string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase))?.DisplayName
        ?? userName;
}
