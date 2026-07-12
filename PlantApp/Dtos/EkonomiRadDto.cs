using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>
/// En rad i ekonomisammanställningen: ett delområde med registrerade plantor,
/// (framtida) timmar samt gällande ersättningsform. Underlag för ekonomi/lön.
/// </summary>
public class EkonomiRadDto
{
	public int TraktId { get; set; }

	public string TraktNamn { get; set; } = "";

	/// <summary>Plantsättare (tilldelad användare) för delområdet. "Ej tilldelad" om ingen är satt.</summary>
	public string Plantsattare { get; set; } = "";

	public int DelOmradeId { get; set; }

	public string? Tradsort { get; set; }

	public double Areal { get; set; }

	public int AntalPlantor { get; set; }

	/// <summary>Registrerade arbetstimmar. Saknar datakälla än – alltid null tills tidsregistrering finns.</summary>
	public double? AntalTimmar { get; set; }

	public DateTime? RegistreradDatum { get; set; }

	/// <summary>Gällande ersättningsform (delområde &gt; trakt &gt; global standard).</summary>
	public ErsattningsTyp ErsattningsTyp { get; set; }

	/// <summary>True om ersättningsformen kommer från en override (trakt/delområde), inte den globala standarden.</summary>
	public bool ErsattningsTypArOverride { get; set; }

	/// <summary>Delområdets egen override (om satt). Null innebär att trakt/global ärvs.</summary>
	public ErsattningsTyp? DelOmradeErsattningsTyp { get; set; }
}
