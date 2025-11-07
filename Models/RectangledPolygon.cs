using System.Text.Json;

namespace WebApplication1.Models
{
    public class RectangledPolygon
    {
        public double MinLon { get; private set; }
        public double MinLat { get; private set; }
        public double MaxLon { get; private set; }
        public double MaxLat { get; private set; }
        public static RectangledPolygon FromGeoJson(string geoJsonString)
        {
            var geoJsonDoc = JsonDocument.Parse(geoJsonString);

            // Zakładamy Polygon z jednym pierścieniem: "coordinates": [[[lon, lat], ...]]
            var coords = geoJsonDoc.RootElement
                .GetProperty("geometry")
                .GetProperty("coordinates")[0];

            var lons = new List<double>();
            var lats = new List<double>();

            foreach (var point in coords.EnumerateArray())
            {
                lons.Add(point[0].GetDouble());
                lats.Add(point[1].GetDouble());
            }

            return new RectangledPolygon
            {
                MinLon = lons.Min(),
                MaxLon = lons.Max(),
                MinLat = lats.Min(),
                MaxLat = lats.Max()
            };
        }
        public double[] GetBBoxArray()
        {
            return new[] { MinLon, MinLat, MaxLon, MaxLat };
        }
        public double Width => MaxLon - MinLon;
        public double Height => MaxLat - MinLat;
    }
}
