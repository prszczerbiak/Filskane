using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp; // Do PointF
using System.Text.Json;     // Do prostej deserializacji BBOX
using WebApplication1.Models;

namespace WebApplication1.Utils;

public static class GeoUtils
{

    // --- 1. Parsowanie BBOX z bazy danych (JSON string) ---
    public static Bbox? ParseBbox(string bboxJson)
    {
        if (string.IsNullOrWhiteSpace(bboxJson)) return null;
        try
        {
            // Używamy System.Text.Json dla wydajności przy prostych obiektach
            return JsonSerializer.Deserialize<Bbox>(bboxJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    // --- 2. Wyciąganie BBOX z GeoJSON (Twoja logika na NetTopologySuite) ---
    public static Bbox? GetBboxFromGeoJson(string geojson)
    {
        if (string.IsNullOrWhiteSpace(geojson)) return null;

        try
        {
            var reader = new GeoJsonReader();
            Geometry? geom = null;

            // Parsujemy wstępnie JObject, żeby sprawdzić czy to Feature czy Geometry
            var jsonObject = JObject.Parse(geojson);

            if (jsonObject["type"]?.ToString() == "Feature" && jsonObject["geometry"] != null)
            {
                string geometryJson = jsonObject["geometry"]!.ToString();
                geom = reader.Read<Geometry>(geometryJson);
            }
            else
            {
                geom = reader.Read<Geometry>(geojson);
            }

            if (geom == null) return null;

            // NetTopologySuite idealnie liczy Envelope (otoczkę)
            var env = geom.EnvelopeInternal;

            return new Bbox
            {
                MinX = env.MinX,
                MinY = env.MinY,
                MaxX = env.MaxX,
                MaxY = env.MaxY
            };
        }
        catch
        {
            return null;
        }
    }

    // --- 3. Sprawdzanie czy pole mieści się w rastrze ---
    public static bool IsFieldWithinRaster(string rasterBboxJson, string fieldGeoJson)
    {
        var rasterBbox = ParseBbox(rasterBboxJson);
        var fieldBbox = GetBboxFromGeoJson(fieldGeoJson);

        if (rasterBbox == null || fieldBbox == null) return false;

        // Sprawdzamy czy prostokąt pola zawiera się w prostokącie rastra
        return fieldBbox.MinX >= rasterBbox.MinX &&
               fieldBbox.MaxX <= rasterBbox.MaxX &&
               fieldBbox.MinY >= rasterBbox.MinY &&
               fieldBbox.MaxY <= rasterBbox.MaxY;
    }

    // --- 4. Konwersja GeoJSON -> Piksele (Do rysowania w ImageUtils) ---
    // Ta metoda korzysta teraz z NetTopologySuite, co jest bezpieczniejsze niż ręczne parsowanie JSON
    public static List<PointF> GetPolygonPixels(string geoJson, Bbox rasterBbox, int imgWidth, int imgHeight)
    {
        var points = new List<PointF>();

        try
        {
            var reader = new GeoJsonReader();
            Geometry? geom = null;
            var jsonObject = JObject.Parse(geoJson);

            // Obsługa Feature vs Geometry (tak samo jak w GetBbox)
            if (jsonObject["type"]?.ToString() == "Feature" && jsonObject["geometry"] != null)
                geom = reader.Read<Geometry>(jsonObject["geometry"]!.ToString());
            else
                geom = reader.Read<Geometry>(geoJson);

            if (geom == null) return points;

            // Pobieramy współrzędne (zewnętrzny pierścień polygonu)
            var coordinates = geom.Coordinates;

            foreach (var coord in coordinates)
            {
                // Projekcja geograficzna na piksele
                // X (Lon) -> 0..Width
                float px = (float)((coord.X - rasterBbox.MinX) / (rasterBbox.MaxX - rasterBbox.MinX) * imgWidth);

                // Y (Lat) -> 0..Height (UWAGA: Y na obrazach rośnie w dół, a Latitude rośnie w górę)
                float py = (float)((rasterBbox.MaxY - coord.Y) / (rasterBbox.MaxY - rasterBbox.MinY) * imgHeight);

                points.Add(new PointF(px, py));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas konwersji GeoJSON na piksele: {ex.Message}");
        }

        return points;
    }

    // Zwraca tablicę tablic [[x, y], [x, y]...] punktów wewnątrz pola
    public static double[][] GetPixelsInsidePolygonAsArray(string geoJson, string bboxJson, int imgWidth, int imgHeight)
    {
        var bbox = ParseBbox(bboxJson);
        if (bbox == null) return Array.Empty<double[]>();

        // Pobierz wierzchołki wielokąta w pikselach
        var polygonPixels = GetPolygonPixels(geoJson, bbox, imgWidth, imgHeight);

        // Algorytm "Point in Polygon" dla każdego piksela w bounding boxie
        // (Optymalizacja: sprawdzamy tylko prostokąt otaczający polygon)

        float minX = polygonPixels.Min(p => p.X);
        float maxX = polygonPixels.Max(p => p.X);
        float minY = polygonPixels.Min(p => p.Y);
        float maxY = polygonPixels.Max(p => p.Y);

        var insidePoints = new List<double[]>();

        for (int y = (int)minY; y <= maxY; y++)
        {
            if (y < 0 || y >= imgHeight) continue;

            for (int x = (int)minX; x <= maxX; x++)
            {
                if (x < 0 || x >= imgWidth) continue;

                // Sprawdź czy punkt (x,y) jest w środku wielokąta
                if (IsPointInPolygon(new PointF(x, y), polygonPixels))
                {
                    insidePoints.Add(new double[] { x, y });
                }
            }
        }

        return insidePoints.ToArray();
    }

    // Algorytm Ray-Casting (czy punkt jest w wielokącie)
    private static bool IsPointInPolygon(PointF p, List<PointF> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
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
}