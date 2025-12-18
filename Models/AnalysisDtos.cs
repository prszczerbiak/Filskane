using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public record FieldDetailDto(
        int Id, string Name,
        int? CropId, string PlantName,
        int? PlantStateId, string CycleName,
        DateTime? SowingDate,
        string SoilComplex, string SoilType, string SoilSubstrate,
        double Area, string Geojson, string MinBbox
    );

    // Do zapisu zmian
    public record UpdateFieldRequest(int? CropId, DateTime? SowingDate);

    // Do cykli
    public record CycleDto(int Id, string Name);

    // Do listy skanów
    public record ScanSummaryDto(int Id, int FieldId, DateTime Date);

    // Do roślin
    public record PlantDto(int Id, string Name);

    // Do progów (GrowthCycles)
    public record ThresholdDto(int CycleId, double MinNdvi, double MaxNdvi);

    // Wynik pobrania skanu (Obraz + Metadane)
    public record ScanResultDto(
        DateTime ScanDate,
        byte[] ImageBytes,
        string FieldBbox
    );

    public record NdviDataDto(
        List<List<double>> Ndvi, // Jeśli ScanResult zwraca float, zmień tutaj double na float
        string? FieldBbox
    );

    public record UpdateTiffsDto(
        DateTime Date,
        IFormFile? Zip,   // Nullable, bo walidację "czy plik istnieje" robisz ręcznie w kontrolerze
        string? Geojson
    );

    public record ScanRequestDto(string Geojson);

    public record NdviVisualizationDto(
        List<List<double>> NdviMatrix, // Główna macierz danych
        string? FieldBbox,             // Opcjonalne granice pola
        string? Bbox                   // Opcjonalne granice obrazu
    );

    public record GroupingResultDto(
        string MainImageBase64, // Główna mapa z nałożonym ryzykiem
        string LegendBase64     // Pasek legendy
    );

    public record DbscanResultDto(
    [property: JsonPropertyName("cluster_ids")] int[] ClusterIds,
    [property: JsonPropertyName("ndvi_medians")] Dictionary<string, double> NdviMedians
);
}
