using System.Text.Json;

namespace WebApplication1.Models
{
    public class Bbox
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public override string ToString() => JsonSerializer.Serialize(this);
    }

}
