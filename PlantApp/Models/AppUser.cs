using Microsoft.AspNetCore.Identity;

namespace PlantApp.Models;

/// <summary>
/// Applikationsanvändare ovanpå ASP.NET Core Identity. Lösenord hashas av Identity.
/// Roll speglas även på Identity-roller, men lagras här för enkel visning/filtrering.
/// Borttagning sker som soft-delete via <see cref="ArAktiv"/> för att bevara historik
/// (löner, inventeringar, tilldelningar kopplas via användarnamn).
/// </summary>
public class AppUser : IdentityUser
{
    /// <summary>Visningsnamn som visas i appen (t.ex. "Peter Plant").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Användarens roll (SuperAdmin/Admin/Plantbas/Plantsattare/Inventerare).</summary>
    public string Roll { get; set; } = string.Empty;

    /// <summary>
    /// Organisationen (tenant) som användaren tillhör. Null för super-admin som står
    /// ovanför alla organisationer.
    /// </summary>
    public int? OrganisationId { get; set; }

    /// <summary>Aktiv användare kan logga in. Inaktiverade konton behålls för historik.</summary>
    public bool ArAktiv { get; set; } = true;

    /// <summary>När kontot skapades.</summary>
    public DateTime SkapadDatum { get; set; } = DateTime.UtcNow;

    /// <summary>När kontot senast inaktiverades (null om aktivt).</summary>
    public DateTime? InaktiveradDatum { get; set; }
}
