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
        [FromForm] string? standardTradsort,
        [FromForm] int? malPlantorPerHektar,
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
                ".zip" => await _shapefileService.ImportZippedShapefileAsync(
                    file, namn, standardTradsort, malPlantorPerHektar, cancellationToken),
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
                t.InventeringMojlig,
                AntalDelomraden = t.DelOmraden.Count,
                AntalProvytor = t.DelOmraden.SelectMany(d => d.Provytor).Count(),
                TotaltAntalPlantor = t.DelOmraden.Sum(d => d.AntalPlantor ?? 0)
            })
            .ToListAsync(cancellationToken);

        return Ok(trakter);
    }

    /// <summary>
    /// Sätter om en trakt är öppnad för inventering. Admin-funktion.
    /// </summary>
    [HttpPut("{id:int}/inventering-mojlig")]
    public async Task<IActionResult> SetInventeringMojlig(
        int id,
        [FromBody] SetInventeringMojligDto dto,
        CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        trakt.InventeringMojlig = dto.Mojlig;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Raderar en trakt och – via cascade – dess delområden och provytor. Admin-funktion.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTrakt(int id, CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        // DelOmraden och deras Provytor tas bort automatiskt via konfigurerad cascade delete.
        _db.Trakter.Remove(trakt);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
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
                { "antalPlantor", delOmrade.AntalPlantor }
            };

            features.Add(new Feature(wgs84, attributes));
        }

        var geoJson = new GeoJsonWriter().Write(features);
        return Content(geoJson, "application/json");
    }

    /// <summary>
    /// Returnerar traktens provytor som GeoJSON-punkter (WGS84) för webbkartan och för
    /// admins nedladdning av kartan med markerade provytor. Endast provytor som har en
    /// registrerad plats tas med.
    /// </summary>
    [HttpGet("{id:int}/provytor/geojson")]
    public async Task<IActionResult> GetProvytorGeoJson(int id, CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter
            .AsNoTracking()
            .Include(t => t.DelOmraden)
                .ThenInclude(d => d.Provytor)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        var features = new FeatureCollection();
        foreach (var delOmrade in trakt.DelOmraden)
        {
            foreach (var provyta in delOmrade.Provytor)
            {
                if (provyta.Plats is null)
                {
                    continue;
                }

                // Lagrat i SWEREF99 TM (3006) -> reprojicera till WGS84 (4326) för kartan.
                var wgs84 = _transformer.ToWgs84(provyta.Plats);

                var attributes = new AttributesTable
                {
                    { "id", provyta.Id },
                    { "delOmradeId", provyta.DelOmradeId },
                    { "traktId", trakt.Id },
                    { "antalPlantor", provyta.AntalPlantor }
                };

                features.Add(new Feature(wgs84, attributes));
            }
        }

        var geoJson = new GeoJsonWriter().Write(features);
        return Content(geoJson, "application/json");
    }
    /// total areal, klar areal, procent klar och totalt antal satta plantor.
    /// </summary>
    [HttpGet("{id:int}/statistik")]
    public async Task<IActionResult> GetStatistik(int id, CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter
            .AsNoTracking()
            .Include(t => t.DelOmraden)
                .ThenInclude(d => d.Provytor)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        return Ok(BuildStatistik(trakt));
    }

    /// <summary>
    /// Returnerar aggregerad statistik för samtliga trakter i ett anrop. Används av
    /// adminens insiktsvy där man kan välja trakter och jämföra nyckeltal.
    /// </summary>
    [HttpGet("statistik")]
    public async Task<IActionResult> GetAllStatistik(CancellationToken cancellationToken)
    {
        var trakter = await _db.Trakter
            .AsNoTracking()
            .Include(t => t.DelOmraden)
                .ThenInclude(d => d.Provytor)
            .OrderBy(t => t.Namn)
            .ToListAsync(cancellationToken);

        var statistik = trakter.Select(BuildStatistik).ToList();

        return Ok(statistik);
    }

    /// <summary>
    /// Bygger en <see cref="TraktStatistikDto"/> från en inläst trakt (med delområden
    /// och provytor). Delas av statistik-endpointen för en enskild trakt och för alla.
    /// </summary>
    private static TraktStatistikDto BuildStatistik(PlantApp.Models.Trakt trakt)
    {
        var delomraden = trakt.DelOmraden;
        var totalAreal = delomraden.Sum(d => d.Areal);
        var klarAreal = delomraden
            .Where(d => d.Status == PlantApp.Models.DelOmradeStatus.Klar)
            .Sum(d => d.Areal);
        var procentKlar = totalAreal > 0 ? klarAreal / totalAreal * 100d : 0d;

        // Målantal plantor: varje delområdes effektiva mål/ha (eget mål i första hand,
        // annars traktens) multiplicerat med arealen. Summeras över trakten.
        var malTotaltAntalPlantor = delomraden.Sum(d =>
        {
            var malPerHektar = d.MalPlantorPerHektar ?? trakt.MalPlantorPerHektar;
            return malPerHektar is > 0 ? malPerHektar.Value * d.Areal : 0d;
        });

        // Samma målberäkning uppdelad per effektiv trädsort (delområdets egen i första
        // hand, annars traktens standard). Används av planeringsvyn för kartong-/pallbehov.
        var malPerTradsort = delomraden
            .GroupBy(d => d.Tradsort ?? trakt.StandardTradsort)
            .Select(g => new MalPerTradsortDto
            {
                Tradsort = string.IsNullOrWhiteSpace(g.Key) ? null : g.Key,
                MalTotaltAntalPlantor = (int)Math.Round(g.Sum(d =>
                {
                    var malPerHektar = d.MalPlantorPerHektar ?? trakt.MalPlantorPerHektar;
                    return malPerHektar is > 0 ? malPerHektar.Value * d.Areal : 0d;
                }))
            })
            .Where(m => m.MalTotaltAntalPlantor > 0)
            .OrderBy(m => m.Tradsort ?? "\uffff")
            .ToList();

        return new TraktStatistikDto
        {
            TraktId = trakt.Id,
            Namn = trakt.Namn,
            AntalDelomraden = delomraden.Count,
            TotalAreal = totalAreal,
            KlarAreal = klarAreal,
            ProcentKlar = procentKlar,
            AntalKlara = delomraden.Count(d => d.Status == PlantApp.Models.DelOmradeStatus.Klar),
            TotaltAntalPlantor = delomraden.Sum(d => d.AntalPlantor ?? 0),
            MalTotaltAntalPlantor = (int)Math.Round(malTotaltAntalPlantor),
            MalPerTradsort = malPerTradsort,
            AntalProvytor = delomraden.Sum(d => d.Provytor.Count),
            Avvikelser = delomraden
                .Where(d => !string.IsNullOrWhiteSpace(d.Kommentar))
                .OrderBy(d => d.Id)
                .Select(d => new DelOmradeAvvikelseDto
                {
                    DelOmradeId = d.Id,
                    Kommentar = d.Kommentar!,
                    Status = d.Status.ToString()
                })
                .ToList()
        };
    }

    /// <summary>
    /// Returnerar fullständig detalj för ett delområde, inklusive dess provytor.
    /// Hämtas av kartvyns rapporteringspanel när ett delområde klickas.
    /// </summary>
    [HttpGet("delomrade/{id:int}")]
    public async Task<IActionResult> GetDelOmradeDetail(int id, CancellationToken cancellationToken)
    {
        var delOmrade = await _db.DelOmraden
            .AsNoTracking()
            .Include(d => d.Trakt)
            .Include(d => d.Provytor)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (delOmrade is null)
        {
            return NotFound();
        }

        return Ok(MapToDetail(delOmrade));
    }

    /// <summary>
    /// Mappar ett delområde (med trakt och provytor inlästa) till detalj-DTO:n som
    /// kartvyns rapporteringspanel använder. Delas av GET- och PUT-endpointen.
    /// </summary>
    private DelOmradeDetailDto MapToDetail(PlantApp.Models.DelOmrade delOmrade) =>
        new()
        {
            Id = delOmrade.Id,
            TraktId = delOmrade.TraktId,
            Status = delOmrade.Status,
            Areal = delOmrade.Areal,
            Tradsort = delOmrade.Tradsort,
            TraktStandardTradsort = delOmrade.Trakt?.StandardTradsort,
            MalPlantorPerHektar = delOmrade.MalPlantorPerHektar,
            TraktMalPlantorPerHektar = delOmrade.Trakt?.MalPlantorPerHektar,
            AntalPlantor = delOmrade.AntalPlantor,
            Kommentar = delOmrade.Kommentar,
            TilldeladAnvandare = delOmrade.TilldeladAnvandare,
            Provytor = delOmrade.Provytor
                .OrderBy(p => p.Id)
                .Select(p =>
                {
                    var wgs84 = p.Plats is not null ? _transformer.ToWgs84(p.Plats) : null;
                    return new ProvytaDto
                    {
                        Id = p.Id,
                        AntalPlantor = p.AntalPlantor,
                        Latitud = wgs84?.Y,
                        Longitud = wgs84?.X
                    };
                })
                .ToList()
        };

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
            .Include(d => d.Trakt)
            .Include(d => d.Provytor)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (delOmrade is null)
        {
            return NotFound();
        }

        // Ett delområde kan inte markeras som klart utan att antal plantor är angivet.
        // Valideras även serversidan så att det inte kan kringgås av ett direkt API-anrop.
        if (dto.Status == PlantApp.Models.DelOmradeStatus.Klar && (dto.AntalPlantor is null or <= 0))
        {
            return BadRequest("Ange antal plantor innan området markeras som klart.");
        }

        delOmrade.Status = dto.Status;
        delOmrade.AntalPlantor = dto.AntalPlantor;
        delOmrade.Tradsort = Normalisera(dto.Tradsort);
        delOmrade.MalPlantorPerHektar = dto.MalPlantorPerHektar;
        delOmrade.Kommentar = Normalisera(dto.Kommentar);
        delOmrade.TilldeladAnvandare = Normalisera(dto.TilldeladAnvandare);

        // Provytor skickas som en fullständig lista från klienten – ersätt de befintliga
        // raderna i stället för att försöka diffa fram vilka som lagts till/tagits bort.
        _db.RemoveRange(delOmrade.Provytor);
        delOmrade.Provytor = dto.Provytor
            .Select(p => new PlantApp.Models.Provyta
            {
                DelOmradeId = delOmrade.Id,
                AntalPlantor = p.AntalPlantor,
                // Kartklicket kommer i WGS84 (long/lat) – lagra som punkt i SWEREF99 TM.
                Plats = p.Latitud is { } lat && p.Longitud is { } lon
                    ? _transformer.ToSweref99Tm(lon, lat)
                    : null
            })
            .ToList();

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(MapToDetail(delOmrade));
    }

    /// <summary>
    /// Returnerar traktens standardträdsort (för kartvyns admin-toolbar).
    /// </summary>
    [HttpGet("{id:int}/tradsort")]
    public async Task<IActionResult> GetTraktTradsort(int id, CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        return Ok(new TraktTradsortDto { StandardTradsort = trakt.StandardTradsort });
    }

    /// <summary>
    /// Sätter (eller tömmer) traktens standardträdsort. Admin-funktion från kartvyn.
    /// </summary>
    [HttpPut("{id:int}/tradsort")]
    public async Task<IActionResult> SetTraktTradsort(
        int id,
        [FromBody] TraktTradsortDto dto,
        CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter
            .Include(t => t.DelOmraden)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        var gammalStandard = trakt.StandardTradsort;
        var nyStandard = Normalisera(dto.StandardTradsort);
        trakt.StandardTradsort = nyStandard;

        // Propagera det nya trädslaget till rutorna. Rutor som ärver (saknar eget
        // trädslag) eller som hade den gamla standarden följer med, medan medvetna
        // per-ruta-överstyrningar (ett annat värde) lämnas orörda.
        foreach (var delOmrade in trakt.DelOmraden)
        {
            bool arver = string.IsNullOrWhiteSpace(delOmrade.Tradsort);
            bool haddeGammalStandard = !string.IsNullOrWhiteSpace(gammalStandard)
                && string.Equals(delOmrade.Tradsort, gammalStandard, StringComparison.OrdinalIgnoreCase);

            if (arver || haddeGammalStandard)
            {
                delOmrade.Tradsort = nyStandard;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new TraktTradsortDto { StandardTradsort = trakt.StandardTradsort });
    }

    /// <summary>
    /// Returnerar traktens mål plantor per hektar (för kartvyns admin-toolbar).
    /// </summary>
    [HttpGet("{id:int}/mal")]
    public async Task<IActionResult> GetTraktMal(int id, CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        return Ok(new TraktMalDto { MalPlantorPerHektar = trakt.MalPlantorPerHektar });
    }

    /// <summary>
    /// Sätter (eller tömmer) traktens mål plantor per hektar. Admin/plantbas-funktion från kartvyn.
    /// </summary>
    [HttpPut("{id:int}/mal")]
    public async Task<IActionResult> SetTraktMal(
        int id,
        [FromBody] TraktMalDto dto,
        CancellationToken cancellationToken)
    {
        var trakt = await _db.Trakter
            .Include(t => t.DelOmraden)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (trakt is null)
        {
            return NotFound();
        }

        var gammaltMal = trakt.MalPlantorPerHektar;
        var nyttMal = dto.MalPlantorPerHektar is > 0 ? dto.MalPlantorPerHektar : null;
        trakt.MalPlantorPerHektar = nyttMal;

        // Propagera det nya målet till rutorna. Rutor som ärver (saknar eget mål)
        // eller som hade det gamla målet följer med, medan medvetna per-ruta-
        // överstyrningar (ett annat värde) lämnas orörda.
        foreach (var delOmrade in trakt.DelOmraden)
        {
            bool arver = delOmrade.MalPlantorPerHektar is null;
            bool haddeGammaltMal = gammaltMal is not null
                && delOmrade.MalPlantorPerHektar == gammaltMal;

            if (arver || haddeGammaltMal)
            {
                delOmrade.MalPlantorPerHektar = nyttMal;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new TraktMalDto { MalPlantorPerHektar = trakt.MalPlantorPerHektar });
    }

    /// <summary>Trimmar en sträng och gör tomt/whitespace till null för konsekvent lagring.</summary>
    private static string? Normalisera(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
