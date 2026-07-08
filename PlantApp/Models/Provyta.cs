namespace PlantApp.Models;

/// <summary>
/// En enskild provyta som tagits inom ett <see cref="DelOmrade"/>, med sitt eget
/// antal plantor. Antalet provytor på ett delområde är helt enkelt antalet rader
/// i denna tabell för delområdet.
/// </summary>
public class Provyta
{
	public int Id { get; set; }

	public int DelOmradeId { get; set; }

	public int AntalPlantor { get; set; }

	public DelOmrade DelOmrade { get; set; } = null!;
}
