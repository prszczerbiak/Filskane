using System.Text.Json;
using System.Text.Json.Serialization;
using OSGeo.GDAL;

namespace Filskane.Models
{
    /// <summary>
    /// Szczegółowe dane o polu, zawierające informacje o glebie, aktualnej uprawie i geometrii.
    /// </summary>
    public record FieldDetailDto(
        int Id,
        string Name,
        int? CropId,
        string PlantName,
        int? PlantStateId,
        string CycleName,
        DateTime? SowingDate,
        string SoilComplex,
        string SoilType,
        string SoilSubstrate,
        double Area,
        JsonElement Geojson,
        Bbox? MinBbox
    );

    /// <summary>
    /// Model danych do zapisu zmian w konfiguracji pola (zmiana uprawy lub daty siewu).
    /// </summary>
    public record UpdateFieldRequest(int? CropId, DateTime? SowingDate);

    /// <summary>
    /// Obiekt transferowy reprezentujący fazę cyklu wzrostu rośliny.
    /// </summary>
    public record CycleDto(int Id, string Name);

    /// <summary>
    /// Podsumowanie informacji o skanie satelitarnym (do wyświetlania na listach).
    /// </summary>
    public record ScanSummaryDto(int Id, int FieldId, DateTime Date);

    /// <summary>
    /// Obiekt słownikowy reprezentujący roślinę uprawną.
    /// </summary>
    public record PlantDto(int Id, string Name);

    /// <summary>
    /// Definicja progów wartości NDVI dla poszczególnych cykli wzrostu.
    /// </summary>
    public record ThresholdDto(int CycleId, double MinNdvi, double MaxNdvi);

    /// <summary>
    /// Wynik pobrania skanu zawierający dane obrazu oraz jego granice geograficzne.
    /// </summary>
    public record ScanResultDto(
        DateTime ScanDate,
        byte[] ImageBytes,
        Bbox? FieldBbox
    );

    /// <summary>
    /// Zawiera surowe dane numeryczne NDVI w formie macierzy oraz BBox pola.
    /// </summary>
    public record NdviDataDto(
        double[] Ndvi,
        int MatrixWidth,
        int MatrixHeight,
        Bbox? FieldBbox
    );

    /// <summary>
    /// Model służący do przesyłania nowych plików skanów (TIFF) spakowanych w ZIP.
    /// </summary>
    /// <param name="Date">Data wykonania skanu.</param>
    /// <param name="Zip">Plik ZIP zawierający obrazy (może być null przed walidacją).</param>
    /// <param name="Geojson">Opcjonalna geometria pola.</param>
    public record UpdateTiffsDto(
        DateTime Date,
        IFormFile? Zip,
        JsonElement Geojson
    );

    /// <summary>
    /// Prosty wrapper na GeoJSON wysyłany w żądaniach.
    /// </summary>
    public record ScanRequestDto(JsonElement Geojson);

    /// <summary>
    /// Dane wejściowe do wygenerowania wizualizacji graficznej (PNG) indeksu NDVI.
    /// </summary>
    /// <param name="IndexMatrix">Macierz wartości NDVI.</param>
    /// <param name="FieldGeoJson">Opcjonalna geometria pola w formacie GeoJSON.</param>
    /// <param name="Bbox">Opcjonalne granice obrazu.</param>
    public record IndexVisualizationDto(
        double[] IndexMatrix,
        int MatrixWidth,
        int MatrixHeight,
        JsonElement FieldGeoJson,
        Bbox? Bbox,
        string? AnalysisType = null
    );

    /// <summary>
    /// Wynik grupowania ryzyka zawierający obrazy w formacie Base64.
    /// </summary>
    /// <param name="MainImageBase64">Obraz mapy z nałożonymi strefami ryzyka.</param>
    /// <param name="LegendBase64">Obraz legendy kolorystycznej.</param>
    public record GroupingResultDto(
        string MainImageBase64,
        string LegendBase64
    );

    /// <summary>
    /// Wynik działania algorytmu DBSCAN z API Pythonowego.
    /// </summary>
    public record DbscanResultDto(
        [property: JsonPropertyName("cluster_ids")] int[] ClusterIds,
        [property: JsonPropertyName("ndvi_means")] Dictionary<string, double> NdviMeans
    );

    public record MultiIndexGroupingResultDto(
        [property: JsonPropertyName("combined_classes")] int[][] CombinedClasses,
        [property: JsonPropertyName("cluster_ids")] int[] ClusterIds,
        [property: JsonPropertyName("cluster_means")] Dictionary<string, double> ClusterMeans,
        [property: JsonPropertyName("cluster_points")] int[][] ClusterPoints
    );

    /// <summary>
    /// Parametry żądania o pogrupowanie (klasteryzację) danych NDVI.
    /// </summary>
    public record NdviGroupRequestDto
    {
        public int PlantId { get; init; }
        public int CycleId { get; init; }
        public double[] VegetationIndex { get; init; } = [];
        public int MatrixWidth { get; init; }
        public int MatrixHeight { get; init; }
        public string AnalysisType { get; init; } = "NDVI";
        public JsonElement FieldGeojson { get; init; }
        public Bbox? ImageBbox { get; init; }
        public bool DarkMode { get; init; } = false;
    }

    /// <summary>
    /// Klasa reprezentująca prostokąt ograniczający (Bounding Box) w układzie współrzędnych geograficznych.
    /// </summary>
    public readonly record struct Bbox(
    [property: JsonPropertyName("minX")] double MinX,
    [property: JsonPropertyName("minY")] double MinY,
    [property: JsonPropertyName("maxX")] double MaxX,
    [property: JsonPropertyName("maxY")] double MaxY
)
    {
        /// <summary>
        /// Opcje serializatora - można wyciągnąć jako statyczne pole, by nie alokować ich za każdym razem.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Deserializuje obiekt Bbox z ciągu JSON, obsługując różną wielkość liter.
        /// </summary>
        public static Bbox? FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                // Zamiast ręcznie parsować JsonDocument (co alokuje pamięć na DOM),
                // zrzucamy to na zoptymalizowany JsonSerializer, dodając niewrażliwość na wielkość liter.
                return JsonSerializer.Deserialize<Bbox>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tworzy obiekt Bbox na podstawie geotransformacji ze zbioru danych GDAL.
        /// </summary>
        public static Bbox? FromGdal(Dataset ds)
        {
            if (ds == null) return null;

            double[] gt = new double[6];
            ds.GetGeoTransform(gt);

            double minX = gt[0];
            double maxY = gt[3];
            double maxX = minX + (gt[1] * ds.RasterXSize);
            double minY = maxY + (gt[5] * ds.RasterYSize);

            // Zwracamy naszą strukturę przez konstruktor pozycyjny
            return new Bbox(minX, minY, maxX, maxY);
        }
    }
}