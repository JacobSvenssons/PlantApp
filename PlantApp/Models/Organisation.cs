namespace PlantApp.Models;

/// <summary>
/// En organisation (företag) i det fleranvändar-baserade systemet (multi-tenant). Varje
/// organisation är en egen isolerad instans: dess admins och team ser endast sin egen
/// organisations trakter, inventeringar, tidrapporter och ekonomi. En super-admin ovanför
/// alla organisationer skapar och hanterar dem.
/// </summary>
public class Organisation
{
    public int Id { get; set; }

    /// <summary>Företagets/organisationens namn.</summary>
    public required string Namn { get; set; }

    /// <summary>När organisationen skapades.</summary>
    public DateTime SkapadDatum { get; set; } = DateTime.UtcNow;

    /// <summary>Aktiv organisation kan användas/logga in. Inaktiverade behålls för historik.</summary>
    public bool ArAktiv { get; set; } = true;
}
