using NetTopologySuite;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace PlantApp.Services;

/// <summary>
/// ProjNET-baserad reprojicering från SWEREF99 TM (3006) till WGS84 (4326).
/// Transformen är trådsäker och återanvändbar, så tjänsten registreras som singleton.
/// </summary>
public sealed class CoordinateTransformer : ICoordinateTransformer
{
    // SWEREF99 TM (EPSG:3006). Datumet (SWEREF99/ETRS89) ligger < 1 m från WGS84,
    // vilket är gott och väl inom noggrannheten för kartvisning i Leaflet.
    private const string Sweref99TmWkt =
        "PROJCS[\"SWEREF99 TM\"," +
            "GEOGCS[\"SWEREF99\"," +
                "DATUM[\"SWEREF99\"," +
                    "SPHEROID[\"GRS 1980\",6378137,298.257222101]," +
                    "TOWGS84[0,0,0,0,0,0,0]]," +
                "PRIMEM[\"Greenwich\",0]," +
                "UNIT[\"degree\",0.0174532925199433]]," +
            "PROJECTION[\"Transverse_Mercator\"]," +
            "PARAMETER[\"latitude_of_origin\",0]," +
            "PARAMETER[\"central_meridian\",15]," +
            "PARAMETER[\"scale_factor\",0.9996]," +
            "PARAMETER[\"false_easting\",500000]," +
            "PARAMETER[\"false_northing\",0]," +
            "UNIT[\"metre\",1]," +
            "AXIS[\"Easting\",EAST]," +
            "AXIS[\"Northing\",NORTH]]";

    private const int Wgs84Srid = 4326;
    private const int Sweref99TmSrid = 3006;

    private readonly MathTransform _toWgs84;
    private readonly MathTransform _toSweref99Tm;
    private readonly GeometryFactory _wgs84Factory;
    private readonly GeometryFactory _sweref99TmFactory;

    public CoordinateTransformer()
    {
        var csFactory = new CoordinateSystemFactory();
        var sweref99Tm = csFactory.CreateFromWkt(Sweref99TmWkt);
        var wgs84 = GeographicCoordinateSystem.WGS84;

        var ctFactory = new CoordinateTransformationFactory();
        _toWgs84 = ctFactory.CreateFromCoordinateSystems(sweref99Tm, wgs84).MathTransform;
        _toSweref99Tm = ctFactory.CreateFromCoordinateSystems(wgs84, sweref99Tm).MathTransform;

        _wgs84Factory = NtsGeometryServices.Instance.CreateGeometryFactory(Wgs84Srid);
        _sweref99TmFactory = NtsGeometryServices.Instance.CreateGeometryFactory(Sweref99TmSrid);
    }

    public Polygon ToWgs84(Polygon source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var shell = TransformRing(source.ExteriorRing);
        var holes = new LinearRing[source.NumInteriorRings];
        for (int i = 0; i < source.NumInteriorRings; i++)
        {
            holes[i] = TransformRing(source.GetInteriorRingN(i));
        }

        return _wgs84Factory.CreatePolygon(shell, holes);
    }

    public Point ToWgs84(Point source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // SWEREF99 TM: X = Easting, Y = Northing  ->  WGS84: X = longitud, Y = latitud.
        var (lon, lat) = _toWgs84.Transform(source.X, source.Y);
        return _wgs84Factory.CreatePoint(new Coordinate(lon, lat));
    }

    public Point ToSweref99Tm(double longitude, double latitude)
    {
        // WGS84: X = longitud, Y = latitud  ->  SWEREF99 TM: X = Easting, Y = Northing.
        var (easting, northing) = _toSweref99Tm.Transform(longitude, latitude);
        return _sweref99TmFactory.CreatePoint(new Coordinate(easting, northing));
    }

    private LinearRing TransformRing(LineString ring)
    {
        var source = ring.Coordinates;
        var transformed = new Coordinate[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            // SWEREF99 TM: X = Easting, Y = Northing  ->  WGS84: X = longitud, Y = latitud.
            var (lon, lat) = _toWgs84.Transform(source[i].X, source[i].Y);
            transformed[i] = new Coordinate(lon, lat);
        }

        return _wgs84Factory.CreateLinearRing(transformed);
    }
}
