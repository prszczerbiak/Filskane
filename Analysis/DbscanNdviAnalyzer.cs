using Dbscan;
using Dbscan.RBush;
using System.Diagnostics.PerformanceData;

namespace WebApplication1.Analysis
{
    public class DbscanNdviAnalyzer
    {
        private readonly double _eps;
        private readonly int _minPts;

        public DbscanNdviAnalyzer(double eps = 5.0, int minPts = 5)
        {
            _eps = eps;
            _minPts = minPts;
        }

        // 🔹 1. Liczenie NDVI z TIFF
        public double[,] CalculateNdvi(byte[] tiffBytes)
        {
            // Tu wklejasz kod z funkcji ScanResult.CalculateNdvi lub nową implementację
            // Zwraca double[,] ndvi
            throw new NotImplementedException();
        }

        // 🔹 2. Wyodrębnienie pikseli zagrożonych
        private List<PixelPoint> GetThreatPoints(double[,] ndvi, double threshold)
        {
            int height = ndvi.GetLength(0);
            int width = ndvi.GetLength(1);
            var points = new List<PixelPoint>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (ndvi[y, x] < threshold)
                        points.Add(new PixelPoint { X = x, Y = y });
                }
            }

            return points;
        }

        

        // 🔹 3. Uruchomienie DBSCAN
        public int[,] RunDbscan(double[,] ndvi, double threshold)
        {
            int height = ndvi.GetLength(0);
            int width = ndvi.GetLength(1);
            int[,] clusterMap = new int[height, width];

            var points = GetThreatPoints(ndvi, threshold);
            var index = new ListSpatialIndex<PixelPoint>(points);

            var clusters = Dbscan.Dbscan.CalculateClusters(
                points,
                _eps,
                _minPts
                );


            //var dbscan = new DbscanAlgorithm<PixelPoint>(
            //    eps: _eps,
            //    minPts: _minPts,
            //    metric: (a, b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2))
            //);

            //var clusters = dbscan.ComputeClusterDbscan(points);

            // Wypełniamy tablicę clusterId
            foreach (var cluster in clusters.Clusters)
            {
                var cluster = clusters[clusterIndex];
                foreach (var p in cluster.Objects)
                {
                    clusterMap[(int)p.Point.Y, (int)p.Point.X] = clusterIndex + 1;
                }
            }

            foreach (var p in clusters.UnclusteredObjects)
            {
                clusterMap[(int)p.Point.Y, (int)p.Point.X] = 0; // 0 = brak klastra
            }

            return clusterMap;
        }

        // 🔹 4. Metoda łącząca NDVI + DBSCAN
        public int[,] GetClusterMap(byte[] tiffBytes, double threshold)
        {
            var ndvi = CalculateNdvi(tiffBytes);
            return RunDbscan(ndvi, threshold);
        }

    }

    // Klasa pomocnicza dla DBSCAN
    public class PixelPoint: IPointData
    {
        public int X { get; set; }
        public int Y { get; set; }

        // Właściwość wymagana przez IPointData
        public Point Point => new Point ( X, Y );
    }
}
