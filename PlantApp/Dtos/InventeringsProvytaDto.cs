namespace PlantApp.Dtos;

/// <summary>
/// En inventeringsprovyta så som den returneras till klienten, inklusive dess
/// kartmarkering (WGS84), trädslag och kommentar/skadenotering.
/// </summary>
public class InventeringsProvytaDto
{
	public int Id { get; set; }

	public int? DelOmradeId { get; set; }

	public int AntalPlantor { get; set; }

	public string? Tradsort { get; set; }

	public string? Kommentar { get; set; }

	/// <summary>Latitud (WGS84) för provytans plats. Null om ingen markering gjorts.</summary>
	public double? Latitud { get; set; }

	/// <summary>Longitud (WGS84) för provytans plats. Null om ingen markering gjorts.</summary>
	public double? Longitud { get; set; }
}
