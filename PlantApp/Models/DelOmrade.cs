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

    /// <summary>
    /// Delområdets mål för antal plantor per hektar. Null innebär att traktens
    /// mål (<see cref="Trakt.MalPlantorPerHektar"/>) ärvs. Sätts av admin/plantbas;
    /// plantsättare ser det bara.
    /// </summary>
    public int? MalPlantorPerHektar { get; set; }

    public int? AntalPlantor { get; set; }

    /// <summary>
    /// Datum då plantor först registrerades på delområdet (sätts automatiskt när
    /// <see cref="AntalPlantor"/> går från null till ett värde). Används som period/datum
    /// i ekonomisammanställningen. Null tills registrering skett.
    /// </summary>
    public DateTime? RegistreradDatum { get; set; }

    /// <summary>
    /// Åsidosätter trakten/globala ersättningsformen för detta delområde. Null innebär
    /// att traktens (eller den globala) ersättningsformen gäller. Underlag för ekonomi/lön.
    /// </summary>
    public ErsattningsTyp? ErsattningsTyp { get; set; }

    /// <summary>
    /// Fritext-avvikelse eller kommentar om delområdet, t.ex. hinder i terrängen
    /// eller noteringar från planteringen. Valfri.
    /// </summary>
    public string? Kommentar { get; set; }

    /// <summary>
    /// Användarnamn för den demo-användare som delområdet är tilldelat (appointerat) till.
    /// Null om ingen är tilldelad. Blir en riktig användarreferens när användarhantering införs.
    /// </summary>
    public string? TilldeladAnvandare { get; set; }

    /// <summary>
    /// Provytor tagna inom delområdet. Antalet provytor är listans längd; varje
    /// provyta har sitt eget antal plantor.
    /// </summary>
    public ICollection<Provyta> Provytor { get; set; } = new List<Provyta>();

    /// <summary>
    /// Den geografiska rutan som en polygon. Mappas till SQL Server-typen 'geometry'
    /// (SWEREF99 TM, SRID 3006) i <c>OnModelCreating</c>.
    /// </summary>
    public Polygon? Geometri { get; set; }

    public Trakt Trakt { get; set; } = null!;
}
