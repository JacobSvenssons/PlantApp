using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>
/// En inventeringsomgång i sammanfattad form (för listor och admin-översikt).
/// </summary>
public class InventeringDto
{
	public int Id { get; set; }

	public int TraktId { get; set; }

	public string? TraktNamn { get; set; }

	public DateTime SkapadDatum { get; set; }

	public string? TilldeladAnvandare { get; set; }

	public InventeringStatus Status { get; set; }

	public int AntalProvytor { get; set; }

	public int TotaltAntalPlantor { get; set; }
}
