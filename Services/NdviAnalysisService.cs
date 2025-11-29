using Accord.Statistics;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Python.Runtime;
using System.Security.Cryptography;
using System.Text.Json;
using WebApplication1.Analysis;
using WebApplication1.Models;
using WebApplication1.Utils;

namespace WebApplication1.Services
{
    public class NdviAnalysisService
    {
        private readonly ThresholdStore _threshold;
        private static bool _pythonInitialized = false;
        private static readonly object _pythonLock = new object();

        private static void EnsurePythonInitialized()
        {
            if (!_pythonInitialized)
            {
                lock (_pythonLock)
                {
                    if (!_pythonInitialized)
                    {
                        InitializePython();
                        _pythonInitialized = true;
                    }
                }
            }
        }

        private static void InitializePython()
        {
            try
            {
                Console.WriteLine("Initializing Python engine...");

                // 🔹 Ustaw PythonDLL PRZED PythonHome
                Runtime.PythonDLL = @"C:\Users\piotr\AppData\Local\Programs\Python\Python311\python311.dll";
                Console.WriteLine($"PythonDLL set to: {Runtime.PythonDLL}");
                Console.WriteLine($"File exists: {File.Exists(Runtime.PythonDLL)}");

                if (!File.Exists(Runtime.PythonDLL))
                {
                    throw new FileNotFoundException($"Python DLL not found at: {Runtime.PythonDLL}");
                }

                PythonEngine.PythonHome = @"C:\Users\piotr\AppData\Local\Programs\Python\Python311";

                // 🔹 Rozszerz PythonPath
                PythonEngine.PythonPath = string.Join(";",
                    @"C:\Users\piotr\AppData\Local\Programs\Python\Python311\Lib",
                    @"C:\Users\piotr\AppData\Local\Programs\Python\Python311\Lib\site-packages",
                    @"C:\Users\piotr\AppData\Local\Programs\Python\Python311\DLLs",
                    @"C:\Users\piotr\Desktop\Materiały na studia\WebApplication1\PythonCluster"
                );

                Console.WriteLine("Calling PythonEngine.Initialize()...");

                // 🔹 INICJALIZACJA Z OBSŁUGĄ WĄTKÓW
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads(); // 🔹 WAŻNE: Pozwól na wielowątkowość

                Console.WriteLine("✓ PythonEngine initialized successfully!");

                // 🔹 TEST: Sprawdź czy możemy użyć GIL
                using (Py.GIL())
                {
                    Console.WriteLine("✓ GIL acquired successfully in initialization");
                    dynamic sys = Py.Import("sys");
                    Console.WriteLine($"✓ Python version: {sys.version}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Python initialization failed: {ex}");
                throw;
            }
        }

        public NdviAnalysisService(ThresholdStore threshold)
        {
            _threshold = threshold;
            EnsurePythonInitialized();
        }

        public GroupByRiskResult GroupByRisk(NdviGroupRequest request)
        {
            // 🔹 1. Oblicz macierz NDVI z pliku TIFF
            double[,] ndvi = ImageUtils.ConvertFromNestedList(request.Ndvi);

            ImageUtils.SaveArrayToTxt(ndvi, "nvdi.txt");

            int height = ndvi.GetLength(0);
            int width = ndvi.GetLength(1);

            double[][] fieldNdvi = ImageUtils.GetPixelsInsidePolygon(width, height, request.FieldGeojson, request.ImageBbox);

            var classyficator = new NdviClassifier(_threshold);
            int[] labels = classyficator.Classify(fieldNdvi, ndvi, request.CycleId);

            var pointsForDbscan = fieldNdvi
                .Where((p, idx) => labels[idx] == 2)
                .ToArray();

            var ndviValuesForDbscan = fieldNdvi
                .Where((p, idx) => labels[idx] == 2)
                .Select(p =>
                {
                    int x = (int)p[0];
                    int y = (int)p[1];
                    return ndvi[y, x];
                })
                .ToArray();

            var (clusterIds, ndviMedians) = RunPythonDbscan(pointsForDbscan, ndviValuesForDbscan);

            int[] combinedLabels = new int[labels.Length];
            int redCounter = 0;

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == 2)
                {
                    // 🔹 ZAMIENIAMY 2 NA clusterId (-1, -2, -3...)
                    combinedLabels[i] = clusterIds[redCounter++];
                }
                else
                {
                    // 🔹 ZOSTAWIAMY 0 i 1
                    combinedLabels[i] = labels[i];
                }
            }

            // 🔹 PRZEKAZUJEMY TYLKO JEDNĄ TABLICĘ
            byte[] overlayBytes = ImageUtils.CreateRiskOverlay(
                fieldNdvi, combinedLabels, width, height);
            var presentClusterIds = combinedLabels.Where(id => id < 0).Distinct().ToArray();
            byte[] legendBytes = ImageUtils.CreateLegend(ndviMedians, presentClusterIds,request.DarkMode);
            Console.WriteLine(request.DarkMode);

            return new GroupByRiskResult
            {
                Overlay = overlayBytes,
                Legend = legendBytes
            };
        }

        private (int[] ClusterIds, Dictionary<string, double> NdviMedians) RunPythonDbscan(double[][] points, double[] ndviValues)
        {
            Console.WriteLine("=== Running Python DBSCAN with NDVI ===");

            EnsurePythonInitialized();

            try
            {
                using (Py.GIL())
                {
                    Console.WriteLine("✓ GIL acquired");

                    dynamic sys = Py.Import("sys");
                    string pythonClusterPath = @"C:\Users\piotr\Desktop\Materiały_na_studia\WebApplication1\PythonCluster";
                    sys.path.insert(0, pythonClusterPath);

                    Console.WriteLine("Importing ndvi_entry...");
                    dynamic module = Py.Import("ndvi_entry");
                    Console.WriteLine("✓ ndvi_entry imported successfully!");

                    string payload = JsonSerializer.Serialize(new
                    {
                        points,
                        ndvi_values = ndviValues,  // 🔹 NOWE: wartości NDVI
                        eps = 2,
                        min_samples = 3,
                        ellipse_h = 3,
                        ellipse_w = 4
                    });

                    Console.WriteLine($"Calling ndvi_cluster with {points.Length} points...");
                    string resultJson = module.ndvi_cluster(payload);
                    Console.WriteLine("✓ ndvi_cluster executed successfully!");

                    var result = JsonSerializer.Deserialize<DbscanResult>(resultJson);

                    // DIAGNOSTYKA
                    if (result.cluster_ids != null)
                    {
                        Console.WriteLine($"✓ Clustering completed: {result.cluster_ids.Length} points processed");

                        var uniqueClusters = result.cluster_ids.Distinct().OrderBy(x => x).ToArray();
                        Console.WriteLine($"✓ Unique cluster IDs: [{string.Join(", ", uniqueClusters)}]");

                        // 🔹 POKAŻ MEDIANY NDVI
                        if (result.ndvi_medians != null)
                        {
                            Console.WriteLine("✓ NDVI medians per cluster:");
                            foreach (var median in result.ndvi_medians.OrderBy(x => x.Key))
                            {
                                string clusterName = median.Key == "-1" ? "Noise" : $"Cluster {median.Key}";
                                Console.WriteLine($"  {clusterName}: {median.Value:F3}");
                            }
                        }
                    }

                    return (result.cluster_ids, result.ndvi_medians ?? new Dictionary<string, double>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General error: {ex.Message}");
                return (new int[0], new Dictionary<string, double>());
            }
        }

        public class DbscanResult
        {
            public int[] cluster_ids { get; set; }
            public Dictionary<string, double> ndvi_medians { get; set; }  // 🔹 NOWE: mediany NDVI
        }

        public class GroupByRiskResult
        {
            public byte[] Overlay { get; set; }
            public byte[] Legend { get; set; }
        }
    }
}
