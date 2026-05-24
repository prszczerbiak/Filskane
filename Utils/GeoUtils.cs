using System.Text.Json;
using SixLabors.ImageSharp;
using Filskane.Models;

namespace Filskane.Utils;

/// <summary>
/// Statyczna klasa pomocnicza do operacji na danych geoprzestrzennych (GeoJSON) 
/// oraz ich transformacji na układ współrzędnych obrazu (rastra).
/// </summary>
public static class GeoUtils
{
    #region Public API

    /// <summary>
    /// Analizuje ciąg GeoJSON i wyznacza jego prostokąt ograniczający (Bbox).
    /// </summary>
    /// <param name="geojson">Ciąg znaków w formacie GeoJSON (FeatureCollection, Feature lub Geometry).</param>
    /// <returns>
    /// Obiekt <see cref="Bbox"/> reprezentujący skrajne współrzędne geograficzne.
    /// Zwraca <c>null</c>, jeśli GeoJSON jest niepoprawny lub nie zawiera punktów.
    /// </returns>
    public static Bbox? GetBboxFromGeoJson(string geojson)
    {
        var points = ExtractPointsFromGeoJson(geojson);

        if (points.Count == 0)
            return null;

        double minX = points[0].X;
        double maxX = points[0].X;
        double minY = points[0].Y;
        double maxY = points[0].Y;

        for (int i = 1; i < points.Count; i++)
        {
            var p = points[i];
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new Bbox(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Sprawdza, czy obszar pola (zdefiniowany przez GeoJSON) mieści się w granicach rastra.
    /// </summary>
    /// <param name="rasterBbox">Bounding Box zdjęcia satelitarnego/mapy.</param>
    /// <param name="fieldGeoJson">Geometria pola w formacie GeoJSON.</param>
    /// <returns>
    /// <c>true</c>, jeśli granice pola mieszczą się całkowicie wewnątrz rastra.
    /// </returns>
    public static bool IsFieldWithinRaster(Bbox rasterBbox, string fieldGeoJson)
    {
        if (rasterBbox == null) return false;

        var fieldBbox = GetBboxFromGeoJson(fieldGeoJson);
        if (fieldBbox == null) return false;

        return fieldBbox.MinX >= rasterBbox.MinX &&
                fieldBbox.MaxX <= rasterBbox.MaxX &&
                fieldBbox.MinY >= rasterBbox.MinY &&
                fieldBbox.MaxY <= rasterBbox.MaxY;
    }

    /// <summary>
    /// Konwertuje współrzędne geograficzne z GeoJSON na współrzędne pikseli wewnątrz obrazu.
    /// </summary>
    /// <param name="geoJson">Geometria wejściowa.</param>
    /// <param name="rasterBbox">Współrzędne geograficzne krawędzi obrazu.</param>
    /// <param name="imgWidth">Szerokość obrazu w pikselach.</param>
    /// <param name="imgHeight">Wysokość obrazu w pikselach.</param>
    /// <returns>Lista punktów <see cref="PointF"/> w układzie współrzędnych obrazu.</returns>
    public static List<PointF> GetPolygonPixels(string geoJson, Bbox rasterBbox, int imgWidth, int imgHeight)
    {
        var resultPoints = new List<PointF>();

        if (rasterBbox == null) return resultPoints;

        var geoPoints = ExtractPointsFromGeoJson(geoJson);

        foreach (var coord in geoPoints)
        {
            float px = (float)((coord.X - rasterBbox.MinX) / (rasterBbox.MaxX - rasterBbox.MinX) * imgWidth);

            float py = (float)((rasterBbox.MaxY - coord.Y) / (rasterBbox.MaxY - rasterBbox.MinY) * imgHeight);

            resultPoints.Add(new PointF(px, py));
        }

        return resultPoints;
    }

    /// <summary>
    /// Funkcja wyciągająca piksele znajdujące się wewnątrz polygonu pola
    /// </summary>
    /// <param name="geoJson">Współrzędne wielokątu pola w GeoJson</param>
    /// <param name="rasterBbox">Bbox zdjęcia pola</param>
    /// <param name="imgWidth">Szerokość zdjęcia</param>
    /// <param name="imgHeight">Wyokość zdjęcia</param>
    /// <returns>Tablica zawierająca współrzędne punktów leżacych w granicy pola</returns>
    public static double[][] GetPixelsInsidePolygonAsArray(string geoJson, Bbox rasterBbox, int imgWidth, int imgHeight)
    {
        if (rasterBbox == null) return Array.Empty<double[]>();

        var polygonPixels = GetPolygonPixels(geoJson, rasterBbox, imgWidth, imgHeight);

        if (polygonPixels.Count == 0) return Array.Empty<double[]>();

      
        float minX = polygonPixels.Min(p => p.X);
        float maxX = polygonPixels.Max(p => p.X);
        float minY = polygonPixels.Min(p => p.Y);
        float maxY = polygonPixels.Max(p => p.Y);

        var insidePoints = new List<double[]>();

        int startY = Math.Max(0, (int)minY);
        int endY = Math.Min(imgHeight - 1, (int)maxY);
        int startX = Math.Max(0, (int)minX);
        int endX = Math.Min(imgWidth - 1, (int)maxX);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (IsPointInPolygon(new PointF(x, y), polygonPixels))
                {
                    insidePoints.Add(new double[] { x, y });
                }
            }
        }

        return insidePoints.ToArray();
    }

    #endregion

    #region Private Methods

    // Wewnętrzna struktura pomocnicza
    private struct GeoPoint { public double X; public double Y; }

    /// <summary>
    /// Parsuje strukturę GeoJSON i spłaszcza ją do listy wierzchołków.
    /// </summary>
    /// <remarks>
    /// Obsługuje: FeatureCollection, Feature, Polygon, MultiPolygon (tylko pierwszy wielokąt).
    /// </remarks>
    private static List<GeoPoint> ExtractPointsFromGeoJson(string geojson)
    {
        var points = new List<GeoPoint>();
        if (string.IsNullOrWhiteSpace(geojson)) return points;

        try
        {
            using var doc = JsonDocument.Parse(geojson);
            var root = doc.RootElement;
            JsonElement geometry = root;

            // 1. Obsługa FeatureCollection
            if (root.TryGetProperty("type", out var type) && type.GetString() == "FeatureCollection")
            {
                if (root.TryGetProperty("features", out var features) && features.GetArrayLength() > 0)
                {
                    var firstFeature = features[0];
                    if (!firstFeature.TryGetProperty("geometry", out geometry)) return points;
                }
                else return points;
            }
            // 2. Obsługa pojedynczego Feature
            else if (type.GetString() == "Feature")
            {
                if (!root.TryGetProperty("geometry", out geometry)) return points;
            }

            // 3. Parsowanie właściwej geometrii
            if (geometry.TryGetProperty("coordinates", out var coordsArray))
            {
                string geomType = geometry.GetProperty("type").GetString();

                if (geomType == "Polygon" && coordsArray.GetArrayLength() > 0)
                {
                    var exteriorRing = coordsArray[0];
                    ExtractCoordinates(exteriorRing, points);
                }
                else if (geomType == "MultiPolygon" && coordsArray.GetArrayLength() > 0)
                {
                    // Dla MultiPolygon bierzemy tylko pierwszy Polygon
                    var firstPolygon = coordsArray[0];
                    if (firstPolygon.GetArrayLength() > 0)
                    {
                        var exteriorRing = firstPolygon[0];
                        ExtractCoordinates(exteriorRing, points);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GeoUtils Error] Błąd parsowania GeoJSON: {ex.Message}");
        }

        return points;
    }

    /// <summary>
    /// Funkcja wyłuskująca z obiektu JsonElement współrzędne wierzchołków wielokąta
    /// </summary>
    /// <param name="ring"></param>
    /// <param name="points"></param>
    private static void ExtractCoordinates(JsonElement ring, List<GeoPoint> points)
    {
        foreach (var pointJson in ring.EnumerateArray())
        {
            points.Add(new GeoPoint
            {
                X = pointJson[0].GetDouble(),
                Y = pointJson[1].GetDouble()
            });
        }
    }

    /// <summary>
    /// Implementacja algorytmu sprawdzającego czy punkt leży wewnątrz wielokąta.
    /// </summary>
    /// <param name="p">Sprawdzany punkt</param>
    /// <param name="polygon">Lista wierzchołków polygona</param>
    private static bool IsPointInPolygon(PointF p, List<PointF> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            // Sprawdzenie przecięcia promienia z krawędzią wielokąta
            if (polygon[i].Y < p.Y && polygon[j].Y >= p.Y || polygon[j].Y < p.Y && polygon[i].Y >= p.Y)
            {
                if (polygon[i].X + (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < p.X)
                {
                    inside = !inside;
                }
            }
            j = i;
        }
        return inside;
    }

    #endregion
}
