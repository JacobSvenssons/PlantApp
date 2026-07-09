using System.Globalization;
using System.IO.Compression;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using PlantApp.Data;
using PlantApp.Models;

namespace PlantApp.Services;

/// <summary>
/// Packar upp en zippad shapefil, läser polygoner (SRID 3006) och attributet "Area",
/// och sparar en ny <see cref="Trakt"/> med tillhörande <see cref="DelOmrade"/>-rader.
/// </summary>
public sealed class ShapefileService : IShapefileService
{
    private const int Sweref99TmSrid = 3006;
    private const string AreaFieldName = "Area";

    // Möjliga namn på trädsort-attributet i DBF:en (olika leverantörer använder olika namn).
    private static readonly string[] TradsortFieldNames =
        ["Tradsort", "Tradslag", "Tradslag_1", "TREESPEC", "Species", "Tradart"];

    private readonly PlantAppDbContext _db;
    private readonly ILogger<ShapefileService> _logger;

    public ShapefileService(PlantAppDbContext db, ILogger<ShapefileService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Trakt> ImportZippedShapefileAsync(
        IFormFile zipFile,
        string? traktNamn,
        string? standardTradsort = null,
        int? malPlantorPerHektar = null,
        CancellationToken cancellationToken = default)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            throw new ArgumentException("Ingen fil laddades upp eller filen är tom.", nameof(zipFile));
        }

        var normaliseradTradsort = string.IsNullOrWhiteSpace(standardTradsort) ? null : standardTradsort.Trim();
        var normaliseratMal = malPlantorPerHektar is > 0 ? malPlantorPerHektar : null;

        var tempDir = Path.Combine(Path.GetTempPath(), "plantapp_shp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            await ExtractZipAsync(zipFile, tempDir, cancellationToken);

            var shpPath = Directory
                .EnumerateFiles(tempDir, "*.shp", SearchOption.AllDirectories)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Zip-arkivet innehåller ingen .shp-fil.");

            // ShapefileDataReader förutsätter att .dbf ligger bredvid .shp.
            if (!File.Exists(Path.ChangeExtension(shpPath, ".dbf")))
            {
                throw new InvalidOperationException("Zip-arkivet saknar tillhörande .dbf-fil.");
            }

            var delOmraden = ReadDelOmraden(shpPath);
            if (delOmraden.Count == 0)
            {
                throw new InvalidOperationException("Ingen giltig polygon kunde läsas ur shapefilen.");
            }

            var trakt = new Trakt
            {
                Namn = string.IsNullOrWhiteSpace(traktNamn)
                    ? Path.GetFileNameWithoutExtension(zipFile.FileName)
                    : traktNamn.Trim(),
                SkapadDatum = DateTime.UtcNow,
                Status = TraktStatus.Planerad,
                StandardTradsort = normaliseradTradsort,
                MalPlantorPerHektar = normaliseratMal,
                DelOmraden = delOmraden
            };

            _db.Trakter.Add(trakt);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Importerade trakt {TraktId} ({Namn}) med {Antal} delområden.",
                trakt.Id, trakt.Namn, delOmraden.Count);

            return trakt;
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static async Task ExtractZipAsync(IFormFile zipFile, string targetDir, CancellationToken cancellationToken)
    {
        // Spara uppladdningen till en fil först (robust även om strömmen inte är seekbar),
        // packa sedan upp den. ExtractToDirectory skyddar mot zip-slip i moderna .NET.
        var zipPath = Path.Combine(targetDir, "upload.zip");
        await using (var fileStream = File.Create(zipPath))
        {
            await zipFile.CopyToAsync(fileStream, cancellationToken);
        }

        ZipFile.ExtractToDirectory(zipPath, targetDir);
        File.Delete(zipPath);
    }

    private List<DelOmrade> ReadDelOmraden(string shpPath)
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(Sweref99TmSrid);
        var result = new List<DelOmrade>();

        using var reader = new ShapefileDataReader(shpPath, factory);
        int areaOrdinal = FindFieldOrdinal(reader, AreaFieldName);
        if (areaOrdinal < 0)
        {
            _logger.LogWarning(
                "Attributet \"{Field}\" hittades inte i .dbf; sätter Areal till 0 för alla delområden.",
                AreaFieldName);
        }

        int tradsortOrdinal = FindFirstFieldOrdinal(reader, TradsortFieldNames);
        if (tradsortOrdinal < 0)
        {
            _logger.LogInformation(
                "Inget trädsort-attribut hittades i .dbf (sökte: {Fields}); Tradsort lämnas tomt.",
                string.Join(", ", TradsortFieldNames));
        }

        while (reader.Read())
        {
            var polygon = AsPolygon(reader.Geometry);
            if (polygon is null)
            {
                continue;
            }

            polygon.SRID = Sweref99TmSrid;

            result.Add(new DelOmrade
            {
                Areal = ReadArea(reader, areaOrdinal),
                Tradsort = ReadString(reader, tradsortOrdinal),
                Status = DelOmradeStatus.EjPaborjad,
                Geometri = polygon
            });
        }

        return FilterOutEnclosingBoundary(result);
    }

    /// <summary>
    /// Om shapefilen innehåller en omslutande yttergräns-polygon (en polygon som täcker
    /// alla övriga) exkluderas den så att den inte blir ett eget delområde. Detta hanterar
    /// filer där yttergräns och rutor ligger som separata, överlappande polygoner.
    /// </summary>
    private List<DelOmrade> FilterOutEnclosingBoundary(List<DelOmrade> delOmraden)
    {
        if (delOmraden.Count < 2)
        {
            return delOmraden;
        }

        // En yttergräns omsluter alla rutor och är därför störst till ytan.
        var candidate = delOmraden
            .Where(d => d.Geometri is not null)
            .OrderByDescending(d => d.Geometri!.Area)
            .FirstOrDefault();

        if (candidate?.Geometri is null)
        {
            return delOmraden;
        }

        var outer = candidate.Geometri;
        bool enclosesAll = delOmraden
            .Where(d => !ReferenceEquals(d, candidate) && d.Geometri is not null)
            .All(d => Encloses(outer, d.Geometri!));

        if (enclosesAll)
        {
            _logger.LogInformation(
                "Ignorerar en omslutande yttergräns-polygon (areal {Area:N0}); behåller {Antal} delområden.",
                outer.Area, delOmraden.Count - 1);
            delOmraden.Remove(candidate);
        }

        return delOmraden;
    }

    // Andel av en rutas yta som måste ligga inne i yttergränsen för att den ska
    // räknas som omsluten. Rutor är ofta separat digitaliserade och kan sticka ut
    // någon meter utanför yttergränsens exakta kant, därför krävs inte 100 %.
    private const double EnclosesMinCoverage = 0.9;

    // Täcker "outer" hela "inner"? Godtar små digitaliseringsglapp genom att även
    // betrakta "inner" som omsluten om merparten av dess yta ligger inne i "outer".
    // Faller tillbaka på bounding box om geometrin är ogiltig.
    private static bool Encloses(Geometry outer, Geometry inner)
    {
        try
        {
            if (outer.Covers(inner))
            {
                return true;
            }

            var innerArea = inner.Area;
            if (innerArea <= 0)
            {
                return false;
            }

            var overlapArea = outer.Intersection(inner).Area;
            return overlapArea / innerArea >= EnclosesMinCoverage;
        }
        catch (Exception)
        {
            return outer.EnvelopeInternal.Covers(inner.EnvelopeInternal);
        }
    }

    private static double ReadArea(ShapefileDataReader reader, int areaOrdinal)
    {
        if (areaOrdinal < 0)
        {
            return 0d;
        }

        var value = reader.GetValue(areaOrdinal);
        if (value is null || value is DBNull)
        {
            return 0d;
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static int FindFieldOrdinal(ShapefileDataReader reader, string fieldName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i)?.Trim(), fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindFirstFieldOrdinal(ShapefileDataReader reader, IEnumerable<string> fieldNames)
    {
        foreach (var name in fieldNames)
        {
            int ordinal = FindFieldOrdinal(reader, name);
            if (ordinal >= 0)
            {
                return ordinal;
            }
        }

        return -1;
    }

    private static string? ReadString(ShapefileDataReader reader, int ordinal)
    {
        if (ordinal < 0)
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        if (value is null || value is DBNull)
        {
            return null;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private Polygon? AsPolygon(Geometry geometry)
    {
        switch (geometry)
        {
            case Polygon polygon:
                return polygon;

            case MultiPolygon { NumGeometries: 1 } single:
                return (Polygon)single.GetGeometryN(0);

            case MultiPolygon multi:
                // Modellen är Polygon: ta den största delen och logga i stället för att förlora data tyst.
                _logger.LogWarning(
                    "MultiPolygon med {Antal} delar hittades; använder den största delen.",
                    multi.NumGeometries);
                return LargestPolygon(multi);

            default:
                _logger.LogWarning(
                    "Hoppar över geometri av typ {Typ} (endast polygoner stöds).",
                    geometry?.GeometryType ?? "null");
                return null;
        }
    }

    private static Polygon LargestPolygon(MultiPolygon multi)
    {
        var largest = (Polygon)multi.GetGeometryN(0);
        for (int i = 1; i < multi.NumGeometries; i++)
        {
            var candidate = (Polygon)multi.GetGeometryN(i);
            if (candidate.Area > largest.Area)
            {
                largest = candidate;
            }
        }

        return largest;
    }

    private void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kunde inte städa tempmappen {Dir}.", dir);
        }
    }
}
