using NetTopologySuite.Geometries;

namespace PlantApp.Models;

/// <summary>
/// En provyta som tagits under en <see cref="Inventering"/>. Registrerar antal räknade
/// plantor, en valfri kartmarkering för var provytan togs samt trädslag och en
/// kommentar/skadenotering. Antalet provytor i en omgång är helt enkelt antalet rader.
/// </summary>
public class InventeringsProvyta
{
	public int Id { get; set; }

	public int InventeringId { get; set; }

	/// <summary>
	/// Valfri koppling till det delområde provytan ligger i (för jämförelse mot
	/// planteringen). Null om provytan inte knutits till ett specifikt delområde.
	/// </summary>
	public int? DelOmradeId { get; set; }

	/// <summary>Antal räknade plantor i provytan.</summary>
	public int AntalPlantor { get; set; }

	/// <summary>Trädslag som räknats i provytan, t.ex. "Contorta" eller "Gran". Valfritt.</summary>
	public string? Tradsort { get; set; }

	/// <summary>Fritext-kommentar eller skadenotering för provytan. Valfri.</summary>
	public string? Kommentar { get; set; }

	/// <summary>
	/// Valfri geografisk plats där provytan togs, lagrad som en punkt i SWEREF99 TM
	/// (SRID 3006, kolumntyp 'geometry'). Null om ingen markering gjorts på kartan.
	/// Reprojiceras till WGS84 för visning i Leaflet.
	/// </summary>
	public Point? Plats { get; set; }

	public Inventering Inventering { get; set; } = null!;
}
