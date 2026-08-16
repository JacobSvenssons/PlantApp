using System.Security.Claims;

namespace PlantApp.Services;

/// <summary>
/// Standardimplementation som läser aktuell organisation från den inloggade användarens
/// claim (<see cref="OrganisationClaimType"/>) via <see cref="IHttpContextAccessor"/>.
/// I controllers och under SSR finns en autentiserad <c>HttpContext</c> med cookie-claims.
/// Värdet kan även sättas explicit (seed/super-admin-skrivning) och tar då företräde.
/// </summary>
public sealed class TenantProvider : ITenantProvider
{
    /// <summary>Claim-typ som bär användarens organisations-id i auth-cookien.</summary>
    public const string OrganisationClaimType = "OrganisationId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private int? _explicitOrganisationId;
    private bool _harExplicitVarde;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? OrganisationId
    {
        get
        {
            if (_harExplicitVarde)
            {
                return _explicitOrganisationId;
            }

            var user = _httpContextAccessor.HttpContext?.User;
            var varde = user?.FindFirst(OrganisationClaimType)?.Value;
            return int.TryParse(varde, out var id) ? id : null;
        }
    }

    public void SetOrganisation(int? organisationId)
    {
        _explicitOrganisationId = organisationId;
        _harExplicitVarde = true;
    }
}
