namespace PlantApp.Dtos;

/// <summary>
/// Data som skickas när en inventeringsomgångs provytor sparas. Provytor skickas som
/// en fullständig lista och ersätter alltid de befintliga raderna för omgången.
/// </summary>
public class InventeringUpdateDto
{
	public List<InventeringsProvytaInputDto> Provytor { get; set; } = [];
}

/// <summary>
/// En inventeringsprovyta så som den skickas in från inventerarvyn: antal räknade
/// plantor, valfri kartmarkering (WGS84), trädslag och kommentar/skadenotering.
/// </summary>
public class InventeringsProvytaInputDto
{
	public int? DelOmradeId { get; set; }

	public int AntalPlantor { get; set; }

	public string? Tradsort { get; set; }

	public string? Kommentar { get; set; }

	/// <summary>Latitud (WGS84) för provytans plats. Null om ingen markering gjorts.</summary>
	public double? Latitud { get; set; }

	/// <summary>Longitud (WGS84) för provytans plats. Null om ingen markering gjorts.</summary>
	public double? Longitud { get; set; }
}

/// <summary>
/// Begäran om att skapa en ny inventeringsomgång för en trakt.
/// </summary>
public class InventeringCreateDto
{
	public int TraktId { get; set; }

	/// <summary>Användarnamn för den inventerare omgången tilldelas. Null = ingen tilldelning.</summary>
	public string? TilldeladAnvandare { get; set; }
}
