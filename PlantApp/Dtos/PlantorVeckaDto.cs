namespace PlantApp.Dtos;

/// <summary>
/// Sammanställning av en plantsättares registrerade plantor under en vecka.
/// Används som underlag i tidrapporten.
/// </summary>
public class PlantorVeckaDto
{
	/// <summary>Totalt antal plantor registrerade under veckan.</summary>
	public int TotaltAntalPlantor { get; set; }

	/// <summary>Totalt antal plantor registrerade hittills under säsongen (kalenderåret).</summary>
	public int SasongAntalPlantor { get; set; }

	/// <summary>Vilket år säsongstotalen avser.</summary>
	public int SasongAr { get; set; }

	/// <summary>Fördelning per trakt.</summary>
	public List<PlantorPerTraktDto> PerTrakt { get; set; } = new();
}

/// <summary>
/// Antal plantor en plantsättare registrerat på en enskild trakt under veckan.
/// </summary>
public class PlantorPerTraktDto
{
	public int TraktId { get; set; }
	public string TraktNamn { get; set; } = "";
	public int AntalPlantor { get; set; }
}
