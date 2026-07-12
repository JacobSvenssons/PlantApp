namespace PlantApp.Models;

/// <summary>
/// Global ekonomi-/ackordsinställning. Tabellen innehåller en enda rad (Id = 1) som
/// anger standardersättningsformen. Enskilda trakter och delområden kan åsidosätta
/// standarden via sina egna (nullable) <see cref="ErsattningsTyp"/>-fält.
/// </summary>
public class EkonomiInstallning
{
	public int Id { get; set; }

	/// <summary>Standardersättningsform som gäller när trakt/delområde inte satt en egen.</summary>
	public ErsattningsTyp StandardErsattningsTyp { get; set; } = ErsattningsTyp.HelAckord;
}
