using System.Text.Json;
using SixLabors.ImageSharp;
using Filskane.Models;
using System.Runtime.CompilerServices;

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
    public static Bbox? GetBboxFromGeoJson(JsonElement geojson)
    {
        if (!TryGetExteriorRing(geojson, out JsonElement ring))
            return null;

        double minX = double.MaxValue;
        double maxX = double.MinValue;
        double minY = double.MaxValue;
        double maxY = double.MinValue;
        bool hasPoints = false;

        // The Leap: Błyskawiczna iteracja i wektoryzacja matematyczna bez if'ów
        foreach (var point in ring.EnumerateArray())
        {
            hasPoints = true;
            double x = point[0].GetDouble();
            double y = point[1].GetDouble();

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        if (!hasPoints) return null;
        return new Bbox(minX, minY, maxX, maxY);
    }

    public static Bbox? GetBboxFromGeoJson(string geojson)
    {
        if (string.IsNullOrWhiteSpace(geojson)) return null;

        try
        {
            using var document = JsonDocument.Parse(geojson);
            return GetBboxFromGeoJson(document.RootElement);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sprawdza, czy obszar pola (zdefiniowany przez GeoJSON) mieści się w granicach rastra.
    /// </summary>
    /// <param name="rasterBbox">Bounding Box zdjęcia satelitarnego/mapy.</param>
    /// <param name="fieldGeoJson">Geometria pola w formacie GeoJSON.</param>
    /// <returns>
    /// <c>true</c>, jeśli granice pola mieszczą się całkowicie wewnątrz rastra.
    /// </returns>
    public static bool IsFieldWithinRaster(Bbox? rasterBbox, JsonElement fieldGeoJson)
    {
        return rasterBbox is not null && GetBboxFromGeoJson(fieldGeoJson) is {} fieldBbox &&
                fieldBbox.MinX >= rasterBbox.MinX &&
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
    public static PointF[] GetPolygonPixels(JsonElement geoJson, Bbox? rasterBbox, int imgWidth, int imgHeight)
    {
        
        if (rasterBbox is not { } raster || !TryGetExteriorRing(geoJson, out JsonElement ring))
        return Array.Empty<PointF>();

        var resultPoints = new PointF[ring.GetArrayLength()];

        float dX = (float)((rasterBbox.MaxX.Value - rasterBbox.MinX.Value) / imgWidth);
        float dY = (float)((rasterBbox.MaxY.Value - rasterBbox.MinY.Value) / imgHeight);

        int i = -1;
        foreach (var point in ring.EnumerateArray())
        {
            double x = point[0].GetDouble();
            double y = point[1].GetDouble();
            float px = (float)((x - rasterBbox.MinX.Value) / dX);
            float py = (float)((rasterBbox.MaxY.Value - y) / dY);
            resultPoints[++i] = new PointF(px, py);
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
    public static List<(int X, int Y)> GetPixelsFromInsidePolygonAsArray(JsonElement geoJson, Bbox rasterBbox, int imgWidth, int imgHeight)
    {
        var polygonPixels = GetPolygonPixels(geoJson, rasterBbox, imgWidth, imgHeight);

        if (polygonPixels.Length == 0) return Array.Empty<(int X, int Y)>();

      
        float minX = MinValue;
        float maxX = MaxValue;
        float minY = MinValue;
        float maxY = MaxValue;

        foreach (var pixel in polygonPixels){
            minX = Math.Min(minX, pixel.X);
            maxX = Math.Max(maxX, pixel.X);
            minY = Math.Min(minY, pixel.Y);
            maxY = Math.Max(maxY, pixel.Y);
        }

        var insidePoints = new List<(int X, int Y)>(0);

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
                    insidePoints.Add((x, y));
                }
            }
        }

        return insidePoints;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Parsuje strukturę GeoJSON i spłaszcza ją do listy wierzchołków.
    /// </summary>
    /// <remarks>
    /// Obsługuje: FeatureCollection, Feature, Polygon, MultiPolygon (tylko pierwszy wielokąt).
    /// </remarks>
    private static bool TryGetExteriorRing(JsonElement geojson, out JsonElement ring)
    {
        ring = default;

        if (geojson.ValueKind != JsonValueKind.Object || !geojson.TryGetProperty("type", out var typeElement))
            return false;

        JsonElement geometry = geojson;

        // Używamy ValueEquals - zero alokacji!
        if (typeElement.ValueEquals("FeatureCollection"))
        {
            if (!geojson.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
                return false;
            
            var firstFeature = features[0];
            if (!firstFeature.TryGetProperty("geometry", out geometry))
                return false;
                
            // Aktualizujemy typeElement dla geometrii
            if (!geometry.TryGetProperty("type", out typeElement))
                return false;
        }
        else if (typeElement.ValueEquals("Feature"))
        {
            if (!geojson.TryGetProperty("geometry", out geometry))
                return false;
                
            // Aktualizujemy typeElement dla geometrii
            if (!geometry.TryGetProperty("type", out typeElement))
                return false;
        }

        if (!geometry.TryGetProperty("coordinates", out var coords) || coords.ValueKind != JsonValueKind.Array)
            return false;

        // Ponownie ValueEquals dla konkretnych typów geometrii
        if (typeElement.ValueEquals("Polygon") && coords.GetArrayLength() > 0)
        {
            ring = coords[0];
            return true;
        }
        
        if (typeElement.ValueEquals("MultiPolygon") && coords.GetArrayLength() > 0)
        {
            var firstPolygon = coords[0];
            if (firstPolygon.GetArrayLength() > 0)
            {
                ring = firstPolygon[0];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Implementacja algorytmu sprawdzającego czy punkt leży wewnątrz wielokąta.
    /// </summary>
    /// <param name="p">Sprawdzany punkt</param>
    /// <param name="polygon">Lista wierzchołków polygona</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPointInPolygon(PointF p, ReadOnlySpan<PointF> polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;
        for (int i = 0; i < polygon.Length; i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            // Sprawdzenie przecięcia promienia z krawędzią wielokąta
            if (pi.Y < p.Y && pj.Y >= p.Y || pj.Y < p.Y && pi.Y >= p.Y)
            {
                if (pi.X + (p.Y - pi.Y) / (pj.Y - pi.Y) * (pj.X - pi.X) < p.X)
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
