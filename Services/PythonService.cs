using Python.Runtime;
using System.Text.Json;
using Filskane.Models;

namespace Filskane.Services;

/// <summary>
/// Serwis integrujący aplikację .NET z silnikiem Python (Python.NET).
/// Odpowiada za inicjalizację środowiska oraz wywoływanie skryptów analitycznych (DBSCAN).
/// </summary>
public class PythonService
{
    private static bool _initialized = false;
    private static readonly object _lock = new object();

    private readonly IConfiguration _configuration;
    private readonly string _scriptPath;

    public PythonService(IConfiguration configuration)
    {
        _configuration = configuration;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string projectDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\.."));
        _scriptPath = Path.Combine(projectDir, "PythonCluster");

        InitializePython();
    }

    /// <summary>
    /// Konfiguruje i uruchamia silnik Python.NET.
    /// Ustawia zmienne środowiskowe PYTHONHOME oraz PYTHONPATH.
    /// </summary>
    /// <exception cref="FileNotFoundException">Rzucany, gdy nie znaleziono biblioteki python311.dll.</exception>
    /// <exception cref="InvalidOperationException">Rzucany w przypadku błędu inicjalizacji silnika.</exception>
    private void InitializePython()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                string pythonHome = _configuration["Python:Path"]
                                    ?? throw new InvalidOperationException("Brak konfiguracji 'Python:Path' w appsettings.json");

                string dllPath = Path.Combine(pythonHome, "python311.dll");

                if (!File.Exists(dllPath))
                    throw new FileNotFoundException($"Nie znaleziono biblioteki Python pod adresem: {dllPath}.");

                Runtime.PythonDLL = dllPath;
                PythonEngine.PythonHome = pythonHome;

                PythonEngine.PythonPath = string.Join(";",
                    Path.Combine(pythonHome, "Lib"),
                    Path.Combine(pythonHome, "Lib", "site-packages"),
                    Path.Combine(pythonHome, "DLLs"),
                    _scriptPath
                );

                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
                _initialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Błąd inicjalizacji Pythona: {ex.Message}", ex);
            }
        }
    }

    public MultiIndexGroupingResultDto RunMultiIndexGrouping(
        List<List<double>> ndvi,
        List<List<double>> gndvi,
        List<List<double>> ndwi,
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

        using (Py.GIL())
        {
            try
            {
                dynamic sys = Py.Import("sys");
                dynamic path = sys.path;
                path.append(_scriptPath);

                dynamic module = Py.Import("ndvi_entry");

                string payload = JsonSerializer.Serialize(new
                {
                    ndvi,
                    gndvi,
                    ndwi,
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
                });

                string resultJson = module.multi_index_grouping(payload);
                var result = JsonSerializer.Deserialize<MultiIndexGroupingResultDto>(resultJson);

                return result ?? new MultiIndexGroupingResultDto(
                    Array.Empty<int[]>(),
                    Array.Empty<int>(),
                    new Dictionary<string, double>(),
                    Array.Empty<int[]>()
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Błąd wykonania grupowania wielowskaźnikowego w Pythonie: {ex.Message}", ex);
            }
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

        using (Py.GIL())
        {
            try
            {
                dynamic sys = Py.Import("sys");
                dynamic path = sys.path;
                path.append(_scriptPath);

                dynamic module = Py.Import("ndvi_entry");

                string payload = JsonSerializer.Serialize(new
                {
                    points,
                    ndvi_values = ndviValues,
                    eps,
                    min_samples = minSamples,
                    ellipse_h = 3,
                    ellipse_w = 4
                });

                string resultJson = module.ndvi_cluster(payload);
                var result = JsonSerializer.Deserialize<DbscanResultDto>(resultJson);

                return (
                    result?.ClusterIds ?? Array.Empty<int>(),
                    result?.NdviMeans ?? new Dictionary<string, double>()
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Błąd wykonania skryptu Python: {ex.Message}", ex);
            }
        }
    }
}