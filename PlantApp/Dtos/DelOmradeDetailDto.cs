using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>
/// Fullständig detaljvy för ett delområde, inklusive dess provytor. Hämtas av
/// kartvyns rapporteringspanel när ett delområde klickas.
/// </summary>
public class DelOmradeDetailDto
{
	public int Id { get; set; }

	public int TraktId { get; set; }

	public DelOmradeStatus Status { get; set; }

	public double Areal { get; set; }

	public string? Tradsort { get; set; }

	/// <summary>Traktens standardträdsort, visas som förval när delområdet saknar egen trädsort.</summary>
	public string? TraktStandardTradsort { get; set; }

	public int? AntalPlantor { get; set; }

	/// <summary>Fritext-avvikelse/kommentar om delområdet.</summary>
	public string? Kommentar { get; set; }

	/// <summary>Användarnamn för den som delområdet är tilldelat (appointerat) till.</summary>
	public string? TilldeladAnvandare { get; set; }

	public List<ProvytaDto> Provytor { get; set; } = [];
}
