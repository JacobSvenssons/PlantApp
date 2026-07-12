namespace PlantApp.Models;

/// <summary>
/// En veckovis tidrapport för en plantsättare. En rad representerar en klump timmar
/// under en vecka med en viss ersättningsform, t.ex. 32 h helackord eller 8 h timpenning
/// (sämre mark/hjälpplantering). Flera rader per vecka är normalt – en per ersättningsform
/// (och eventuellt trakt). Underlag för ekonomi/lön.
/// OBS: användaren identifieras via användarnamn tills riktig användarhantering införs.
/// </summary>
public class Tidrapport
{
	public int Id { get; set; }

	/// <summary>Användarnamn för plantsättaren som rapporterat tiden.</summary>
	public string Anvandare { get; set; } = "";

	/// <summary>
	/// Måndagen (datum, lokal tid) i den vecka rapporten gäller. Används för att gruppera
	/// timmar per vecka.
	/// </summary>
	public DateTime VeckoStart { get; set; }

	/// <summary>Antal arbetade timmar för denna rad.</summary>
	public double AntalTimmar { get; set; }

	/// <summary>Ersättningsform för timmarna (helackord som standard, timpenning vid sämre mark).</summary>
	public ErsattningsTyp ErsattningsTyp { get; set; }

	/// <summary>Valfri koppling till trakt som timmarna avser. Null = ingen koppling.</summary>
	public int? TraktId { get; set; }

	/// <summary>Fritext-kommentar, t.ex. "hjälpplantering" eller "stenig mark". Valfri.</summary>
	public string? Kommentar { get; set; }

	/// <summary>När raden skapades (för spårbarhet).</summary>
	public DateTime SkapadDatum { get; set; }

	public Trakt? Trakt { get; set; }
}
