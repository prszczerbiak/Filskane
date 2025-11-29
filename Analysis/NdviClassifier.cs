using WebApplication1.Services;
namespace WebApplication1.Analysis
{
    public class NdviClassifier
    {
        private readonly ThresholdStore _threshold;

        public NdviClassifier(ThresholdStore threshold)
        {
            _threshold = threshold;
        }

        /// <summary>
        /// Klasyfikuje każdy piksel NDVI do jednej z trzech klas: 
        /// 0 - bardzo dobry, 1 - średni, 2 - zagrożony
        /// </summary>
        public int[] Classify(double[][] input, double[,] ndvi, int cycleId)
        {
            int height = ndvi.GetLength(0);
            int width = ndvi.GetLength(1);
            int[] labels = new int[input.Length];

            // Pobierz dynamiczne progi dla tej rośliny
            var (medium, good) = _threshold.GetThreshold(cycleId);

            Console.WriteLine(medium.ToString(), good.ToString());

            //for (int y = 0; y < height; y++)
            //{
            //    for (int x = 0; x < width; x++)
            //    {
            //        double value = ndvi[y][x];
            //        if (value >= good)
            //            labels[y, x] = 0; // bardzo dobry
            //        else if (value >= medium)
            //            labels[y, x] = 1; // średni
            //        else
            //            labels[y, x] = 2; // zagrożony
            //    }
            //}

            for (int i = 0; i < input.Length; i++)
            {
                int y = (int)input[i][1];
                int x = (int)input[i][0];
                double value = ndvi[y,x];
                if (value >= good)
                    labels[i] = 0; // bardzo dobry
                else if (value >= medium)
                    labels[i] = 1; // średni
                else
                    labels[i] = 2; // zagrożony
            }

            return labels;
        }
    }
}
