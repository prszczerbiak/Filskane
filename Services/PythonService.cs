using Python.Runtime;
using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Services;

public class PythonService
{
    private static bool _initialized = false;
    private static readonly object _lock = new object();

    public PythonService()
    {
        InitializePython();
    }

    private void InitializePython()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                // Konfiguracja ścieżek (dostosuj do swojego środowiska!)
                // W produkcji warto to wyciągnąć do appsettings.json
                string pythonPath = @"C:\Users\piotr\AppData\Local\Programs\Python\Python311";
                string dllPath = Path.Combine(pythonPath, "python311.dll");

                if (!File.Exists(dllPath)) throw new FileNotFoundException("Nie znaleziono python311.dll");

                Runtime.PythonDLL = dllPath;
                PythonEngine.PythonHome = pythonPath;
                PythonEngine.PythonPath = string.Join(";",
                    Path.Combine(pythonPath, "Lib"),
                    Path.Combine(pythonPath, "Lib", "site-packages"),
                    Path.Combine(pythonPath, "DLLs"),
                    @"C:\Users\piotr\Desktop\Materiały_na_studia\WebApplication1\PythonCluster" // Twój skrypt
                );

                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads(); // Ważne dla wielowątkowości
                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd inicjalizacji Pythona: {ex.Message}");
                throw;
            }
        }
    }

    // Metoda wywołująca Twój skrypt DBSCAN
    public (int[] ClusterIds, Dictionary<string, double> NdviMedians) RunDbscan(double[][] points, double[] ndviValues)
    {
        if (points.Length == 0) return (Array.Empty<int>(), new Dictionary<string, double>());

        using (Py.GIL()) // Global Interpreter Lock
        {
            try
            {
                dynamic sys = Py.Import("sys");
                // Upewnij się, że ścieżka jest w sys.path
                string scriptPath = @"C:\Users\piotr\Desktop\Materiały_na_studia\WebApplication1\PythonCluster";
                sys.path.insert(0, scriptPath);

                dynamic module = Py.Import("ndvi_entry");

                string payload = JsonSerializer.Serialize(new
                {
                    points,
                    ndvi_values = ndviValues,
                    eps = 2,
                    min_samples = 3,
                    ellipse_h = 3,
                    ellipse_w = 4
                });

                string resultJson = module.ndvi_cluster(payload);

                // Deserializacja do nowego rekordu
                var result = JsonSerializer.Deserialize<DbscanResultDto>(resultJson);

                return (
                    result?.ClusterIds ?? Array.Empty<int>(),
                    result?.NdviMedians ?? new Dictionary<string, double>()
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd wykonania skryptu Python: {ex.Message}");
                throw;
            }
        }
    }
}