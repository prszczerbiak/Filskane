namespace WebApplication1.Models
{
    public class NdviVisualizationDto
    {
        public List<List<double>> NdviMatrix { get; set; } = new();
        public string? FieldBbox { get; set; }  // jeśli chcesz opcjonalnie przekazać bbox
        public string? Bbox { get; set; }
    }
}
