namespace WebApplication1.Utils;

public static class NdviClassifier
{
    /// <summary>
    /// Klasyfikuje piksele pola na podstawie progów.
    /// 0 - Dobry (powyżej maxT)
    /// 1 - Średni (między minT a maxT)
    /// 2 - Zły/Zagrożony (poniżej minT) - te trafią do Pythona
    /// </summary>
    public static int[] ClassifyPoints(double[][] fieldPoints, double[,] ndviMatrix, double minThreshold, double maxThreshold)
    {
        int[] labels = new int[fieldPoints.Length];

        for (int i = 0; i < fieldPoints.Length; i++)
        {
            // fieldPoints[i] to tablica {x, y}
            int x = (int)fieldPoints[i][0];
            int y = (int)fieldPoints[i][1];

            // Pobieramy wartość NDVI dla tego piksela
            double value = ndviMatrix[y, x];

            if (value >= maxThreshold)
            {
                labels[i] = 0; // Bardzo dobry (Zielony)
            }
            else if (value >= minThreshold)
            {
                labels[i] = 1; // Średni (Żółty)
            }
            else
            {
                labels[i] = 2; // Zagrożony (Czerwony) -> Do analizy klastrów
            }
        }

        return labels;
    }
}