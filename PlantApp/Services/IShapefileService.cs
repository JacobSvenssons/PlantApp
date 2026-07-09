using PlantApp.Models;

namespace PlantApp.Services;

/// <summary>
/// Läser in zippade shapefiler och skapar trakter med delområden i databasen.
/// </summary>
public interface IShapefileService
{
    /// <summary>
    /// Läser en zippad shapefil (.shp/.shx/.dbf), skapar en ny <see cref="Trakt"/> med
    /// <see cref="DelOmrade"/>-rader (status <see cref="DelOmradeStatus.EjPaborjad"/>, geometri i SRID 3006)
    /// och sparar allt i databasen.
    /// </summary>
    /// <param name="zipFile">Uppladdat zip-arkiv som innehåller .shp, .shx och .dbf.</param>
    /// <param name="traktNamn">Valfritt namn på trakten; annars används zip-filens namn.</param>
    /// <param name="standardTradsort">Valfri standardträdsort för trakten som delområdena ärver.</param>
    /// <param name="malPlantorPerHektar">Valfritt mål plantor per hektar för trakten som delområdena ärver.</param>
    /// <param name="cancellationToken">Avbrytningstoken.</param>
    /// <returns>Den sparade trakten inklusive genererade id:n.</returns>
    Task<Trakt> ImportZippedShapefileAsync(
        IFormFile zipFile,
        string? traktNamn,
        string? standardTradsort = null,
        int? malPlantorPerHektar = null,
        CancellationToken cancellationToken = default);
}
