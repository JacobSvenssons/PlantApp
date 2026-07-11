namespace PlantApp.Models;

/// <summary>
/// En inventeringsomgång för en <see cref="Trakt"/>. Utförs typiskt några år efter
/// planteringen för att följa upp resultatet. En trakt kan ha flera omgångar över
/// åren – planteringsdatan (<see cref="DelOmrade"/>/<see cref="Provyta"/>) rörs aldrig,
/// så mål vid plantering kan jämföras mot utfall vid inventering.
/// </summary>
public class Inventering
{
	public int Id { get; set; }

	public int TraktId { get; set; }

	/// <summary>När omgången skapades/påbörjades.</summary>
	public DateTime SkapadDatum { get; set; }

	/// <summary>
	/// Användarnamn för den inventerare omgången är tilldelad till. Null om ingen
	/// är tilldelad. Blir en riktig användarreferens när användarhantering införs.
	/// </summary>
	public string? TilldeladAnvandare { get; set; }

	public InventeringStatus Status { get; set; }

	/// <summary>Provytor som tagits i denna inventeringsomgång.</summary>
	public ICollection<InventeringsProvyta> Provytor { get; set; } = new List<InventeringsProvyta>();

	public Trakt Trakt { get; set; } = null!;
}
