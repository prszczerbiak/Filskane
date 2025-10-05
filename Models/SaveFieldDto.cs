using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class SaveFieldDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("ordinates")]
        public double[] Ordinates { get; set; } = Array.Empty<double>();

        [JsonPropertyName("geojson")]
        public string? Geojson { get; set; }

        [JsonPropertyName("centerX")]
        public double CenterX { get; set; }

        [JsonPropertyName("centerY")]
        public double CenterY { get; set; }

        [JsonPropertyName("area")]

        public double Area {  get; set; }
    }
}
