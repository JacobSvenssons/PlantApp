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

    public ICollection<DelOmrade> DelOmraden { get; set; } = new List<DelOmrade>();
}
