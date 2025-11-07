namespace WebApplication1.Services.Agronomy
{
    public class ThresholdSelector
    {
        /// <summary>
        /// Zwraca wartość progową NDVI dla danej rośliny i etapu wzrostu.
        /// </summary>
        /// <param name="cropType">Typ rośliny (np. pszenica, kukurydza, rzepak)</param>
        /// <param name="sowingDate">Data zasiania rośliny</param>
        /// <param name="scanDate">Data wykonania skanu NDVI</param>
        /// <returns>Wartość progowa NDVI uznawana za "zdrową"</returns>
        public double GetThreshold(string cropType, DateTime sowingDate, DateTime scanDate)
        {
            var daysSinceSowing = (scanDate - sowingDate).TotalDays;

            return cropType.ToLower() switch
            {
                "pszenica" => GetWheatThreshold(daysSinceSowing),
                "rzepak" => GetRapeseedThreshold(daysSinceSowing),
                "kukurydza" => GetCornThreshold(daysSinceSowing),
                _ => 0.45 // domyślny próg dla nieznanych roślin
            };
        }

        private double GetWheatThreshold(double days)
        {
            if (days < 30) return 0.30;  // faza wschodów
            if (days < 60) return 0.45;  // faza wzrostu
            if (days < 90) return 0.60;  // faza krzewienia
            return 0.70;                 // faza kłoszenia i dojrzewania
        }

        private double GetRapeseedThreshold(double days)
        {
            if (days < 40) return 0.35;
            if (days < 80) return 0.50;
            return 0.65;
        }

        private double GetCornThreshold(double days)
        {
            if (days < 30) return 0.25;
            if (days < 60) return 0.40;
            if (days < 90) return 0.55;
            return 0.65;
        }
    }
}
