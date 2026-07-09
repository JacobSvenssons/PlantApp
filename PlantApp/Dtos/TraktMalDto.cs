namespace PlantApp.Dtos;

/// <summary>
/// Payload för att sätta traktens mål för antal plantor per hektar (admin/plantbas-funktion i kartvyn).
/// </summary>
public class TraktMalDto
{
	/// <summary>Traktens mål för antal plantor per hektar. Null tömmer målet.</summary>
	public int? MalPlantorPerHektar { get; set; }
}
