using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>
/// Global ekonomiinställning i läs-/skrivform för admin-vyn.
/// </summary>
public class EkonomiInstallningDto
{
	/// <summary>Standardersättningsform som gäller när trakt/delområde inte satt en egen.</summary>
	public ErsattningsTyp StandardErsattningsTyp { get; set; } = ErsattningsTyp.HelAckord;
}
