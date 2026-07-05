using NetTopologySuite.Geometries;

namespace PlantApp.Models;

/// <summary>
/// Ett delområde inom en <see cref="Trakt"/>. Den geografiska rutan lagras nativt
/// som spatial data i SQL Server via <see cref="Geometri"/>.
/// </summary>
public class DelOmrade
{
    public int Id { get; set; }

    public int TraktId { get; set; }

    /// <summary>
    /// Areal, t.ex. i hektar.
    /// </summary>
    public double Areal { get; set; }

    public DelOmradeStatus Status { get; set; }

    /// <summary>
    /// Trädsort som planteras på delområdet, t.ex. "Contorta" eller "Gran".
    /// Kan saknas om attributet inte finns i den importerade shapefilen.
    /// </summary>
    public string? Tradsort { get; set; }

    public int? AntalPlantor { get; set; }

    public int? AntalPlantorProvyta { get; set; }

    /// <summary>
    /// Den geografiska rutan som en polygon. Mappas till SQL Server-typen 'geometry'
    /// (SWEREF99 TM, SRID 3006) i <c>OnModelCreating</c>.
    /// </summary>
    public Polygon? Geometri { get; set; }

    public Trakt Trakt { get; set; } = null!;
}
