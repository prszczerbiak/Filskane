using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using WebApplication1.Models;
using static WebApplication1.Utils.ImageUtils;

namespace WebApplication1.Utils
{
    public class GeoUtils
    {
        public static bool IsFieldWithinRaster(string rasterBboxJson, string fieldGeoJson)
        {
            if (string.IsNullOrWhiteSpace(rasterBboxJson) || string.IsNullOrWhiteSpace(fieldGeoJson))
            {
                Console.WriteLine("Some of Bbox is null");
                return false;
            }

            try
            {
                // 1️⃣ Deserializacja BBOX rastra
                var rasterBbox = JsonSerializer.Deserialize<Bbox>(rasterBboxJson);
                if (rasterBbox == null)
                    return false;

                // 2️⃣ BBOX pola z GeoJSON
                var fieldBbox = GetBboxFromGeoJson(fieldGeoJson);
                if (fieldBbox == null)
                    return false;

                // 3️⃣ Sprawdzenie czy pole mieści się w BBOX rastra
                bool fits =
                    fieldBbox.MinX >= rasterBbox.MinX &&
                    fieldBbox.MaxX <= rasterBbox.MaxX &&
                    fieldBbox.MinY >= rasterBbox.MinY &&
                    fieldBbox.MaxY <= rasterBbox.MaxY;

                return fits;
            }
            catch
            {
                return false;
            }
        }

        public static Bbox? GetBboxFromGeoJson(string geojson)
        {
            try
            {
                var reader = new GeoJsonReader();
                Geometry? geom = null;

                var jsonObject = JObject.Parse(geojson);

                // Obsłuż Feature
                if (jsonObject["type"]?.ToString() == "Feature" && jsonObject["geometry"] != null)
                {
                    string geometryJson = jsonObject["geometry"]!.ToString();
                    geom = reader.Read<Geometry>(geometryJson);
                }
                else
                {
                    // Obsłuż czystą geometrię
                    geom = reader.Read<Geometry>(geojson);
                }

                if (geom == null)
                    return null;

                // Envelope zwraca minimalny prostokąt obejmujący geometrię
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
    }
}


