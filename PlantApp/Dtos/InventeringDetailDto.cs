using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>
/// Fullständig detaljvy för en inventeringsomgång, inklusive dess provytor.
/// Hämtas av inventerarvyn när en omgång öppnas.
/// </summary>
public class InventeringDetailDto
{
	public int Id { get; set; }

	public int TraktId { get; set; }

	public string? TraktNamn { get; set; }

	public DateTime SkapadDatum { get; set; }

	public string? TilldeladAnvandare { get; set; }

	public InventeringStatus Status { get; set; }

	public List<InventeringsProvytaDto> Provytor { get; set; } = [];
}
