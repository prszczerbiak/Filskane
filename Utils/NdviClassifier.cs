namespace Filskane.Utils;
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
    public static int[] ClassifyPoints(ReadOnlySpan<(int X, int Y)> fieldPoints, ReadOnlySpan<float> ndviArray, int width, float minThreshold, float maxThreshold)
    {
        int[] labels = new int[fieldPoints.Length];

        for (int i = 0; i < fieldPoints.Length; i++)
        {
            (int x, int y) = fieldPoints[i];

            float value = ndviArray[y * width + x];
            labels[i] = value >= maxThreshold ? 0 : value >= minThreshold ? 1 : 2;
        }

        return labels;
    }
    #endregion
}