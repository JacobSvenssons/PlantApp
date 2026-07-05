using PlantApp.Models;

namespace PlantApp.Services;

/// <summary>
/// Import av georefererad PDF (GeoPDF) är <b>tillfälligt pausad</b>.
/// De röda begränsningslinjerna i skogsbolagens GeoPDF-exporter (utskrifter ur ArcMap) sluter sig
/// inte topologiskt exakt, så polygonisering av linjenätet slår ihop grannytor eller tappar gränser
/// och ger därmed felaktig areal. Eftersom arealen måste stämma exakt ska trakter i stället
/// importeras som zippad shapefil (se <see cref="ShapefileService"/>), vilken ger exakt geometri.
/// Tjänsten behålls registrerad så att PDF-stödet kan återaktiveras om topologiskt korrekta
/// PDF:er blir tillgängliga.
/// </summary>
public sealed class GeoPdfService : IGeoPdfService
{
    private readonly ILogger<GeoPdfService> _logger;

    public GeoPdfService(ILogger<GeoPdfService> logger)
    {
        _logger = logger;
    }

    public Task<Trakt> ImportGeoPdfAsync(
        IFormFile pdfFile,
        string? traktNamn,
        CancellationToken cancellationToken = default)
    {
        // GeoPDF-vektorimport är pausad: de röda begränsningslinjerna sluter sig inte exakt,
        // så polygonisering ger sammanslagna/felaktiga ytor och därmed felaktig areal.
        // Eftersom arealen måste stämma exakt ska källdatan importeras som shapefil (.zip).
        _logger.LogWarning(
            "GeoPDF-import avvisades ({Fil}); PDF-vektorimport är pausad eftersom den inte kan ge exakt areal.",
            pdfFile?.FileName);

        throw new InvalidOperationException(
            "GeoPDF-import är tillfälligt pausad. De röda begränsningslinjerna i PDF:en sluter sig inte " +
            "exakt, vilket ger felaktig areal. Ladda i stället upp trakten som en zippad shapefil " +
            "(.zip med .shp, .shx och .dbf) – den ger exakt geometri och areal.");
    }
}
