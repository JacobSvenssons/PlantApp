using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using PlantApp.Data;
using PlantApp.Dtos;
using PlantApp.Services;

namespace PlantApp.Controllers;

[ApiController]
[Route("api/trakt")]
public class TraktController : ControllerBase
{
    private const long MaxUploadBytes = 100L * 1024 * 1024; // 100 MB

    private readonly IShapefileService _shapefileService;
    private readonly IGeoPdfService _geoPdfService;
    private readonly ICoordinateTransformer _transformer;
    private readonly PlantAppDbContext _db;

    public TraktController(
        IShapefileService shapefileService,
        IGeoPdfService geoPdfService,
        ICoordinateTransformer transformer,
        PlantAppDbContext db)
    {
        _shapefileService = shapefileService;
        _geoPdfService = geoPdfService;
        _transformer = transformer;
        _db = db;
    }

    /// <summary>
    /// Laddar upp en zippad shapefil (.shp/.shx/.dbf) eller en GeoPDF (.pdf) och skapar en ny trakt med delområden.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string? namn,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Ingen fil laddades upp.");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();

        try
        {
            var trakt = extension switch
            {
                ".zip" => await _shapefileService.ImportZippedShapefileAsync(file, namn, cancellationToken),
                ".pdf" => await _geoPdfService.ImportGeoPdfAsync(file, namn, cancellationToken),
                _ => throw new ArgumentException(
                    "Filformatet stöds inte. Ladda upp en zippad shapefil (.zip) eller en GeoPDF (.pdf).")
            };

            return CreatedAtAction(
                nameof(GetGeoJson),
                new { id = trakt.Id },
                new
                {
                    trakt.Id,
                    trakt.Namn,
                    Status = trakt.Status.ToString(),
                    AntalDelomraden = trakt.DelOmraden.Count
                });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Listar alla trakter (för startsidans trakt-väljare).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTrakter(CancellationToken cancellationToken)
    {
        var trakter = await _db.Trakter
            .AsNoTracking()
            .OrderByDescending(t => t.SkapadDatum)
            .Select(t => new
            {
                t.Id,
                t.Namn,
                Status = t.Status.ToString(),
                t.SkapadDatum,
                AntalDelomraden = t.DelOmraden.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(trakter);
    }

    /// <summary>
    /// Returnerar traktens delområden som GeoJSON (WGS84) för webbkartan (Leaflet).
    /// </summary>
    [HttpGet("{id:int}/geojson")]
    public async Task<IActionResult> GetGeoJson(int id, CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter
            .AsNoTracking()
            .Include(t => t.DelOmraden)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        var features = new FeatureCollection();
        foreach (var delOmrade in trakt.DelOmraden)
        {
            if (delOmrade.Geometri is null)
            {
                continue;
            }

            // Lagrat i SWEREF99 TM (3006) -> reprojicera till WGS84 (4326) för kartan.
            var wgs84 = _transformer.ToWgs84(delOmrade.Geometri);

            var attributes = new AttributesTable
            {
                { "id", delOmrade.Id },
                { "traktId", delOmrade.TraktId },
                { "status", delOmrade.Status.ToString() },
                { "statusVarde", (int)delOmrade.Status },
                { "areal", delOmrade.Areal },
                { "tradsort", delOmrade.Tradsort },
                { "antalPlantor", delOmrade.AntalPlantor },
                { "antalPlantorProvyta", delOmrade.AntalPlantorProvyta }
            };

            features.Add(new Feature(wgs84, attributes));
        }

        var geoJson = new GeoJsonWriter().Write(features);
        return Content(geoJson, "application/json");
    }

    /// <summary>
    /// Returnerar aggregerad statistik för en trakt (för startsidans dashboard):
    /// total areal, klar areal, procent klar och totalt antal satta plantor.
    /// </summary>
    [HttpGet("{id:int}/statistik")]
    public async Task<IActionResult> GetStatistik(int id, CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter
            .AsNoTracking()
            .Include(t => t.DelOmraden)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        var delomraden = trakt.DelOmraden;
        var totalAreal = delomraden.Sum(d => d.Areal);
        var klarAreal = delomraden
            .Where(d => d.Status == PlantApp.Models.DelOmradeStatus.Klar)
            .Sum(d => d.Areal);
        var procentKlar = totalAreal > 0 ? klarAreal / totalAreal * 100d : 0d;

        var statistik = new TraktStatistikDto
        {
            TraktId = trakt.Id,
            Namn = trakt.Namn,
            AntalDelomraden = delomraden.Count,
            TotalAreal = totalAreal,
            KlarAreal = klarAreal,
            ProcentKlar = procentKlar,
            AntalKlara = delomraden.Count(d => d.Status == PlantApp.Models.DelOmradeStatus.Klar),
            TotaltAntalPlantor = delomraden.Sum(d => d.AntalPlantor ?? 0)
        };

        return Ok(statistik);
    }

    /// <summary>
    /// Uppdaterar ett delområdes status och plantantal (anropas från kartvyns rapporteringspanel).
    /// </summary>
    [HttpPut("delomrade/{id:int}")]
    public async Task<IActionResult> UpdateDelOmrade(
        int id,
        [FromBody] DelOmradeUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var delOmrade = await _db.DelOmraden
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (delOmrade is null)
        {
            return NotFound();
        }

        delOmrade.Status = dto.Status;
        delOmrade.AntalPlantor = dto.AntalPlantor;
        delOmrade.AntalPlantorProvyta = dto.AntalPlantorProvyta;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            delOmrade.Id,
            Status = delOmrade.Status.ToString(),
            StatusVarde = (int)delOmrade.Status,
            delOmrade.AntalPlantor,
            delOmrade.AntalPlantorProvyta
        });
    }
}
