namespace WebApplication1.Models
{
    public class Bbox
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public override string ToString()
        {
            return $"[MinX={MinX}, MinY={MinY}, MaxX={MaxX}, MaxY={MaxY}]";
        }
    }

}
