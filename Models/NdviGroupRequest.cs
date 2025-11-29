namespace WebApplication1.Models
{
    public class NdviGroupRequest
    {
        public int CycleId { get; set; }

        public List<List<Double>> Ndvi { get; set; } = [];

        public string? FieldGeojson { get; set; }

        public string? ImageBbox { get; set; }

        public bool DarkMode { get; set; } = false;
    }
}
