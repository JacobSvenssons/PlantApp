namespace PlantApp.Services;

/// <summary>
/// Ger aktuell organisation (tenant) för den inloggade användaren. Används av
/// <see cref="PlantApp.Data.PlantAppDbContext"/> för global query-filtrering och
/// automatisk stämpling av OrganisationId på nya rader.
///
/// Ett null-värde betyder super-admin (ingen organisationsbegränsning) – query-filtren
/// släpper då igenom alla organisationers data.
/// </summary>
public interface ITenantProvider
{
    /// <summary>Aktuell organisations-id, eller null för super-admin/oautentiserad.</summary>
    int? OrganisationId { get; }

    /// <summary>Sätt aktuell organisation explicit (t.ex. vid seed eller super-admin-skrivning).</summary>
    void SetOrganisation(int? organisationId);
}
