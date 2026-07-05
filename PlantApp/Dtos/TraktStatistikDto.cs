namespace PlantApp.Dtos;

/// <summary>
/// Aggregerad statistik för en trakt som visas i startsidans dashboard.
/// </summary>
public class TraktStatistikDto
{
    public int TraktId { get; set; }

    public string Namn { get; set; } = "";

    public int AntalDelomraden { get; set; }

    /// <summary>Traktens totala areal (hektar), summerad över alla delområden.</summary>
    public double TotalAreal { get; set; }

    /// <summary>Areal (hektar) för delområden med status Klar.</summary>
    public double KlarAreal { get; set; }

    /// <summary>Andel av total areal som är klar, 0–100 (%).</summary>
    public double ProcentKlar { get; set; }

    /// <summary>Antal delområden med status Klar.</summary>
    public int AntalKlara { get; set; }

    /// <summary>Totala antalet satta plantor, summerat över alla delområden.</summary>
    public int TotaltAntalPlantor { get; set; }
}
