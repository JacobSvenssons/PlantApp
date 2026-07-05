using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>
/// Data som skickas från kartvyn när ett delområde uppdateras (status och plantantal).
/// </summary>
public class DelOmradeUpdateDto
{
    public DelOmradeStatus Status { get; set; }

    public int? AntalPlantor { get; set; }

    public int? AntalPlantorProvyta { get; set; }
}
