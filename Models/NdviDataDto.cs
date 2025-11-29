namespace WebApplication1.Models
{
    public class NdviDataDto
    {
        public List<List<Double>> Ndvi { get; set; } = new();
        public string? FieldBbox { get; set; }
    }
}
