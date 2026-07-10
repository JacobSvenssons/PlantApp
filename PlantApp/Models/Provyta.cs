using NetTopologySuite.Geometries;

namespace PlantApp.Models;

/// <summary>
/// En enskild provyta som tagits inom ett <see cref="DelOmrade"/>, med sitt eget
/// antal plantor. Antalet provytor på ett delområde är helt enkelt antalet rader
/// i denna tabell för delområdet.
/// </summary>
public class Provyta
{
	public int Id { get; set; }

	public int DelOmradeId { get; set; }

	public int AntalPlantor { get; set; }

	/// <summary>
	/// Valfri geografisk plats där provytan togs, lagrad som en punkt i SWEREF99 TM
	/// (SRID 3006, kolumntyp 'geometry'). Null om ingen markering gjorts på kartan.
	/// Reprojiceras till WGS84 för visning i Leaflet. Ett första steg mot inventering,
	/// där markering av provytans plats kommer att vara ett krav.
	/// </summary>
	public Point? Plats { get; set; }

	public DelOmrade DelOmrade { get; set; } = null!;
}
