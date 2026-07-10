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

    /// <summary>
    /// Provytor för delområdet. Listans längd = antal provytor. Skickas som en
    /// fullständig lista och ersätter alltid de befintliga raderna för delområdet.
    /// Varje provyta har sitt antal plantor och en valfri kartmarkering (lat/long).
    /// </summary>
    public List<ProvytaInputDto> Provytor { get; set; } = [];
}

/// <summary>
/// En provyta så som den skickas in från kartvyn: antal plantor och en valfri
/// geografisk markering (WGS84) för var provytan togs.
/// </summary>
public class ProvytaInputDto
{
    public int AntalPlantor { get; set; }

    /// <summary>Latitud (WGS84) för provytans plats. Null om ingen markering gjorts.</summary>
    public double? Latitud { get; set; }

    /// <summary>Longitud (WGS84) för provytans plats. Null om ingen markering gjorts.</summary>
    public double? Longitud { get; set; }
}
