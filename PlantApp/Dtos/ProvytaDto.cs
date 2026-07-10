namespace PlantApp.Dtos;

/// <summary>
/// En provyta så som den returneras till klienten (t.ex. i rapporteringspanelen).
/// </summary>
public class ProvytaDto
{
	public int Id { get; set; }

	public int AntalPlantor { get; set; }

	/// <summary>Latitud (WGS84) för provytans plats. Null om ingen markering gjorts.</summary>
	public double? Latitud { get; set; }

	/// <summary>Longitud (WGS84) för provytans plats. Null om ingen markering gjorts.</summary>
	public double? Longitud { get; set; }
}
