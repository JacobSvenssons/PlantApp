namespace PlantApp.Models;

/// <summary>
/// En trakt (skogsområde) som planeras för plantering och som består av ett eller flera delområden.
/// </summary>
public class Trakt
{
    public int Id { get; set; }

    public required string Namn { get; set; }

    public DateTime SkapadDatum { get; set; }

    /// <summary>
    /// Traktens status i planterings-/inskickningsflödet.
    /// </summary>
    public TraktStatus Status { get; set; }

    /// <summary>
    /// Standardträdsort för trakten. Används som förvalt värde för delområden som
    /// saknar en egen trädsort. Kan vara null om admin inte satt någon standard ännu.
    /// </summary>
    public string? StandardTradsort { get; set; }

    /// <summary>
    /// Traktens mål för antal plantor per hektar. Används som förvalt värde för
    /// delområden som saknar ett eget mål. Kan vara null om admin/plantbas inte
    /// satt något mål ännu.
    /// </summary>
    public int? MalPlantorPerHektar { get; set; }

    /// <summary>
    /// Anger om trakten är öppnad för inventering. När den är true kan en inventerare
    /// starta/öppna en inventeringsomgång för trakten från startsidan. Standard är false.
    /// </summary>
    public bool InventeringMojlig { get; set; }

    /// <summary>
    /// Åsidosätter den globala ersättningsformen för denna trakt. Null innebär att den
    /// globala standarden (<see cref="EkonomiInstallning.StandardErsattningsTyp"/>) gäller,
    /// om inte ett delområde i sin tur satt en egen. Underlag för ekonomi/lön.
    /// </summary>
    public ErsattningsTyp? ErsattningsTyp { get; set; }

    public ICollection<DelOmrade> DelOmraden { get; set; } = new List<DelOmrade>();
}
