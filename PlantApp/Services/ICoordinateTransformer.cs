using NetTopologySuite.Geometries;

namespace PlantApp.Services;

/// <summary>
/// Reprojicerar geometrier från SWEREF99 TM (SRID 3006) till WGS84 (SRID 4326).
/// Används vid GeoJSON-export så att Leaflet får lat/long.
/// </summary>
public interface ICoordinateTransformer
{
    /// <summary>
    /// Reprojicerar en polygon från SWEREF99 TM (3006) till WGS84 (4326).
    /// Resultatet har X = longitud, Y = latitud och SRID 4326.
    /// </summary>
    Polygon ToWgs84(Polygon source);
}
