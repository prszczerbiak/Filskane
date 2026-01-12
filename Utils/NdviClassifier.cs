namespace WebApplication1.Utils;
/// <summary>
/// Klasa pomocnicza do klasyfikowania NDVI
/// </summary>
public static class NdviClassifier
{
    #region Public Methods
    /// <summary>
    /// Funkcja klasyfikująca wartości NDVI obrazu na bazie wprowadzonych granic
    /// </summary>
    /// <param name="fieldPoints">Tablica zawierająca współrzędne pikseli obrazu należące do pola</param>
    /// <param name="ndviMatrix">Macierz zawierająca wartości NDVI dla każdego piksela obrazu </param>
    /// <param name="minThreshold">Górna granica poziomu zadowalającego (żółtego) NDVI</param>
    /// <param name="maxThreshold">Dolna granica poziomu zadowalającego (żółtego) NDVI</param>
    /// <returns>Tablica trzech możliwych klas (0 - zdrowy, 1 - zadowalający, 2 - alarmujący) dla każdego punktu z fieldPoints</returns>
    public static int[] ClassifyPoints(double[][] fieldPoints, double[,] ndviMatrix, double minThreshold, double maxThreshold)
    {
        int[] labels = new int[fieldPoints.Length];

        for (int i = 0; i < fieldPoints.Length; i++)
        {
            int x = (int)fieldPoints[i][0];
            int y = (int)fieldPoints[i][1];

            double value = ndviMatrix[y, x];

            if (value >= maxThreshold)
            {
                labels[i] = 0;
            }
            else if (value >= minThreshold)
            {
                labels[i] = 1; 
            }
            else
            {
                labels[i] = 2;
            }
        }

        return labels;
    }
    #endregion
}