using PlantApp.Models;

namespace PlantApp.Services;

/// <summary>
/// Läser in georefererade PDF-filer (GeoPDF) och skapar trakter med delområden i databasen.
/// </summary>
public interface IGeoPdfService
{
    /// <summary>
    /// Läser en GeoPDF (vektorlager i SWEREF99 TM), skapar en ny <see cref="Trakt"/> med
    /// <see cref="DelOmrade"/>-rader (status <see cref="DelOmradeStatus.EjPaborjad"/>, geometri i SRID 3006)
    /// och sparar allt i databasen. Areal beräknas från geometrin eftersom GeoPDF saknar attributtabell.
    /// </summary>
    /// <param name="pdfFile">Uppladdad GeoPDF-fil som innehåller ett eller flera polygon-vektorlager.</param>
    /// <param name="traktNamn">Valfritt namn på trakten; annars används filens namn.</param>
    /// <param name="cancellationToken">Avbrytningstoken.</param>
    /// <returns>Den sparade trakten inklusive genererade id:n.</returns>
    Task<Trakt> ImportGeoPdfAsync(IFormFile pdfFile, string? traktNamn, CancellationToken cancellationToken = default);
}
