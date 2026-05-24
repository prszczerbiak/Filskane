using System.Net.Http.Json;
using System.Text.Json;
using Filskane.Models;

namespace Filskane.Services;

/// <summary>
/// Klient HTTP do mikroserwisu Python odpowiedzialnego za analizy numeryczne.
/// </summary>
public class PythonService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private static double[][] ToJaggedMatrix(double[] values, int matrixWidth, int matrixHeight, string name)
    {
        if (matrixWidth <= 0 || matrixHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "Wymiary macierzy muszą być większe od zera.");
        }

        var expectedSize = matrixWidth * matrixHeight;
        if (values.Length != expectedSize)
        {
            throw new InvalidOperationException(
                $"Tablica '{name}' ma {values.Length} elementów, ale oczekiwano {expectedSize} dla macierzy {matrixWidth}x{matrixHeight}.");
        }

        var matrix = new double[matrixHeight][];
        for (var row = 0; row < matrixHeight; row++)
        {
            var offset = row * matrixWidth;
            var rowValues = new double[matrixWidth];
            Array.Copy(values, offset, rowValues, 0, matrixWidth);
            matrix[row] = rowValues;
        }

        return matrix;
    }

    public PythonService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;

        var baseUrl = _configuration["PythonService:BaseUrl"]
            ?? throw new InvalidOperationException("Brak konfiguracji 'PythonService:BaseUrl' w appsettings.json");
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.GetValue("PythonService:TimeoutSeconds", 120));
    }

    public MultiIndexGroupingResultDto RunMultiIndexGrouping(
        double[] ndvi,
        double[] gndvi,
        double[] ndwi,
        int matrixWidth,
        int matrixHeight,
        double[][] fieldPoints,
        double ndviMin,
        double ndviMax,
        double gndviMin,
        double gndviMax,
        double ndwiMin,
        double ndwiMax)
    {
        double eps = _configuration.GetValue<double>("AlgorithmSettings:Eps", 2.0);
        int minSamples = _configuration.GetValue<int>("AlgorithmSettings:MinSamples", 3);

        try
        {
            var ndviMatrix = ToJaggedMatrix(ndvi, matrixWidth, matrixHeight, nameof(ndvi));
            var gndviMatrix = ToJaggedMatrix(gndvi, matrixWidth, matrixHeight, nameof(gndvi));
            var ndwiMatrix = ToJaggedMatrix(ndwi, matrixWidth, matrixHeight, nameof(ndwi));

            var payload = new
            {
                ndvi = ndviMatrix,
                gndvi = gndviMatrix,
                ndwi = ndwiMatrix,
                matrix_width = matrixWidth,
                matrix_height = matrixHeight,
                field_points = fieldPoints,
                thresholds = new
                {
                    ndvi = new { min = ndviMin, max = ndviMax },
                    gndvi = new { min = gndviMin, max = gndviMax },
                    ndwi = new { min = ndwiMin, max = ndwiMax }
                },
                eps,
                min_samples = minSamples,
                ellipse_h = 3,
                ellipse_w = 4
            };

            var response = _httpClient.PostAsJsonAsync("multi-index-grouping", payload, _jsonOptions)
                .GetAwaiter()
                .GetResult();

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new InvalidOperationException(
                    $"Python API zwróciło {(int)response.StatusCode} ({response.ReasonPhrase}). {errorBody}");
            }

            var result = response.Content.ReadFromJsonAsync<MultiIndexGroupingResultDto>(_jsonOptions)
                .GetAwaiter()
                .GetResult();

            return result ?? new MultiIndexGroupingResultDto(
                Array.Empty<int[]>(),
                Array.Empty<int>(),
                new Dictionary<string, double>(),
                Array.Empty<int[]>()
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Błąd wykonania grupowania wielowskaźnikowego w mikroserwisie Python: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Uruchamia algorytm DBSCAN zaimplementowany w skrypcie Python (moduł 'ndvi_entry').
    /// </summary>
    /// <param name="points">Tablica współrzędnych pikseli [x, y].</param>
    /// <param name="ndviValues">Tablica wartości NDVI odpowiadająca punktom.</param>
    /// <returns>
    /// Krotka zawierająca:
    /// 1. Tablicę ID klastrów dla każdego punktu.
    /// 2. Słownik średnich wartości NDVI dla każdego klastra.
    /// </returns>
    /// <exception cref="InvalidOperationException">Rzucany, gdy wystąpi błąd po stronie skryptu Python.</exception>
    public (int[] ClusterIds, Dictionary<string, double> NdviMedians) RunDbscan(double[][] points, double[] ndviValues)
    {
        if (points.Length == 0) return (Array.Empty<int>(), new Dictionary<string, double>());

        double eps = _configuration.GetValue<double>("AlgorithmSettings:Eps", 2.0);
        int minSamples = _configuration.GetValue<int>("AlgorithmSettings:MinSamples", 3);

        try
        {
            var payload = new
            {
                points,
                ndvi_values = ndviValues,
                eps,
                min_samples = minSamples,
                ellipse_h = 3,
                ellipse_w = 4
            };

            var response = _httpClient.PostAsJsonAsync("dbscan", payload, _jsonOptions)
                .GetAwaiter()
                .GetResult();
            response.EnsureSuccessStatusCode();

            var result = response.Content.ReadFromJsonAsync<DbscanResultDto>(_jsonOptions)
                .GetAwaiter()
                .GetResult();

            return (
                result?.ClusterIds ?? Array.Empty<int>(),
                result?.NdviMeans ?? new Dictionary<string, double>()
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Błąd wykonania analizy DBSCAN w mikroserwisie Python: {ex.Message}", ex);
        }
    }
}