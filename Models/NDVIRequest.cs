namespace WebApplication1.Models
{
    public class NDVIRequest
    {
        public double MinLon { get; set; }
        public double MinLat { get; set; }
        public double MaxLon { get; set; }
        public double MaxLat { get; set; }
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
    }
}
