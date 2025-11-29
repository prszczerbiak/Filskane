using Accord.MachineLearning.Bayes;
using Accord.Statistics;
using Accord.Statistics.Distributions.Fitting;
using Accord.Statistics.Distributions.Univariate;
using Accord.Statistics.Distributions.DensityKernels;
using Microsoft.Net.Http.Headers;
using WebApplication1.Utils;

namespace WebApplication1.Analysis
{
    public class BayesNDVI
    {
        private NaiveBayes<NormalDistribution>? _model;

        /// <summary>
        /// Uczy model na podstawie wartości NDVI, współrzędnych i progów
        /// </summary>
        public void Train(double[][] ndvi, int[] labels)
        {
            var teacher = new NaiveBayesLearning<NormalDistribution>()
            {
                Distribution = (classIndex, variableIndex) =>
                {
                    return new NormalDistribution(mean: 0.5, stdDev: 1.5);
                }
            };

            _model = teacher.Learn(ndvi, labels);
            //utworzenie modelu Bayesa
            //_model = new NaiveBayesLearning<TriangularDistribution>().Learn(ndvi, labels);

 
           var nClasses = _model.NumberOfClasses;

            if (nClasses == 3)
                _model.Priors = [0.3, 0.5, 0.2];
            else if (nClasses == 2)
                _model.Priors = [0.4, 0.6];
            else
                _model.Priors = [1.0];
        }

        /// <summary>
        /// Przewiduje klasy dla całego obrazu NDVI
        /// </summary>
        public int[] Predict(double[][] input)
        {
            if (_model == null)
                throw new InvalidOperationException("Model nie został wytrenowany.");

            int[] result = new int[input.Length];

            for(int i = 0; i < result.Length;i++)
            {
                result[i] = _model.Decide(input[i]);
            }

            //for (int y = 0; y < height; y++)
            //{
            //    for (int x = 0; x < width; x++)
            //    {
            //        result[y, x] = _model.Decide([y,x]);
            //    }
            //}

            return result;
        }
    }
}
