using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>
/// Data som skickas från kartvyn när ett delområde uppdateras (status, plantantal
/// och provytor). Provytor skickas som en fullständig lista och ersätter alltid
/// de befintliga raderna för delområdet.
/// </summary>
public class DelOmradeUpdateDto
{
    public DelOmradeStatus Status { get; set; }

    public int? AntalPlantor { get; set; }

    /// <summary>Trädsort för delområdet (override av traktens standard). Null = använd traktens standard.</summary>
    public string? Tradsort { get; set; }

    /// <summary>Mål plantor per hektar för delområdet (override av traktens mål). Null = använd traktens mål.</summary>
    public int? MalPlantorPerHektar { get; set; }

    /// <summary>Fritext-avvikelse/kommentar om delområdet. Valfri.</summary>
    public string? Kommentar { get; set; }

    /// <summary>Användarnamn för den demo-användare delområdet tilldelas. Null = ingen tilldelning.</summary>
    public string? TilldeladAnvandare { get; set; }

    /// <summary>Antal plantor per provyta. Listans längd = antal provytor.</summary>
    public List<int> ProvytaAntal { get; set; } = [];
}
