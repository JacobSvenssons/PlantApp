using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>En tidrapportrad så som den visas i fältvyn/listor.</summary>
public class TidrapportDto
{
	public int Id { get; set; }

	public string Anvandare { get; set; } = "";

	public DateTime VeckoStart { get; set; }

	public double AntalTimmar { get; set; }

	public ErsattningsTyp ErsattningsTyp { get; set; }

	public int? TraktId { get; set; }

	public string? TraktNamn { get; set; }

	public string? Kommentar { get; set; }
}

/// <summary>Data som skickas in när en plantsättare lägger till en tidrapportrad.</summary>
public class TidrapportInputDto
{
	/// <summary>Måndagen i veckan rapporten gäller (lokalt datum).</summary>
	public DateTime VeckoStart { get; set; }

	public double AntalTimmar { get; set; }

	public ErsattningsTyp ErsattningsTyp { get; set; }

	public int? TraktId { get; set; }

	public string? Kommentar { get; set; }
}

/// <summary>Sammanställning av timmar per ersättningsform för en person och period.</summary>
public class TidrapportSummeringDto
{
	public string Anvandare { get; set; } = "";

	public double TimmarHelackord { get; set; }

	public double TimmarDelackord { get; set; }

	public double TimmarTimpenning { get; set; }

	public double TimmarTotalt => TimmarHelackord + TimmarDelackord + TimmarTimpenning;
}
