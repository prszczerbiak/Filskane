using System.Xml.Linq;
using WebApplication1.Models;
using WebApplication1.DAL;

namespace WebApplication1.Services
{
    /// <summary>
    /// Serwis biznesowy zarządzający danymi gospodarstwa (lokalizacja bazy) oraz polami uprawnymi.
    /// Integruje się z zewnętrznym API Geoportalu w celu pobierania danych glebowych.
    /// </summary>
    public class FarmService
    {
        private readonly FarmDAL _farmDal;
        private readonly FieldDAL _fieldDal;
        private readonly SettingsDAL _settingsDal;
        private readonly HttpClient _httpClient;

        public FarmService(FarmDAL farmDal, FieldDAL fieldDal, SettingsDAL settingsDal, HttpClient httpClient)
        {
            _farmDal = farmDal;
            _fieldDal = fieldDal;
            _settingsDal = settingsDal;
            _httpClient = httpClient;
        }


        /// <summary>
        /// Pobiera szczegółowe dane o użytkowniku i lokalizacji jego gospodarstwa.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <returns>Obiekt DTO ze szczegółami lub null.</returns>
        public async Task<UserDetailDto?> GetCurrentFarmInfoAsync(string username)
        {
            return await _settingsDal.GetLongInfoAsync(username);
        }

        /// <summary>
        /// Ustawia lub aktualizuje współrzędne bazy gospodarstwa.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="x">Długość geograficzna.</param>
        /// <param name="y">Szerokość geograficzna.</param>
        public async Task SetFarmCoordsAsync(string username, double? x, double? y)
        {
            await _farmDal.SaveFarmCoordinatesAsync(username, x, y);
        }

        /// <summary>
        /// Usuwa zapisaną lokalizację gospodarstwa.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        public async Task DeleteFarmCoordsAsync(string username)
        {
            await _farmDal.DeleteFarmCoordinatesAsync(username);
        }


        /// <summary>
        /// Pobiera listę pól użytkownika w formacie skróconym.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <returns>Lista pól.</returns>
        public async Task<List<FieldShortDto>> GetUserFieldsAsync(string username)
        {
            return await _fieldDal.GetUserFieldsAsync(username);
        }

        /// <summary>
        /// Usuwa pole użytkownika.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="fieldId">ID pola do usunięcia.</param>
        public async Task DeleteFieldAsync(string username, int fieldId)
        {
            await _fieldDal.DeleteFieldAsync(username, fieldId);
        }


        /// <summary>
        /// Dodaje nowe pole do bazy danych, automatycznie pobierając dane glebowe z zewnętrznego serwisu Geoportal (WMS).
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="dto">Dane nowego pola (geometria, powierzchnia, środek).</param>
        /// <returns>ID nowo utworzonego pola.</returns>
        public async Task<int> AddFieldWithSoilInfoAsync(string username, SaveFieldRequest dto)
        {
            var (complex, type, substrate) = await FetchSoilDetails(dto.CenterX, dto.CenterY);

            return await _fieldDal.SaveFieldAsync(
                username,
                dto.Name,
                dto.Geojson,
                dto.CenterX,
                dto.CenterY,
                dto.Area,
                complex,
                type,
                substrate
            );
        }

        /// <summary>
        /// Prywatna metoda wykonująca zapytanie WMS GetFeatureInfo do Geoportalu w celu ustalenia klasy gleby.
        /// </summary>
        /// <param name="x">Długość geograficzna punktu.</param>
        /// <param name="y">Szerokość geograficzna punktu.</param>
        /// <returns>Krotka (Kompleks, Typ, Podłoże).</returns>
        private async Task<(string complex, string type, string substrate)> FetchSoilDetails(double x, double y)
        {
            try
            {
                string url = "https://mapy.geoportal.gov.pl/wss/service/pub/guest/MapaGlebowoRolnicza/MapServer/WMSServer";

                double minx = x - 0.001;
                double miny = y - 0.001;
                double maxx = x + 0.001;
                double maxy = y + 0.001;

                string minxStr = minx.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string minyStr = miny.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string maxxStr = maxx.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string maxyStr = maxy.ToString(System.Globalization.CultureInfo.InvariantCulture);

                int width = 800, height = 600;

                int I = (int)((x - minx) / (maxx - minx) * width);
                int J = (int)((maxy - y) / (maxy - miny) * height);

                var query = new[]
                {
                    "SERVICE=WMS", "VERSION=1.3.0", "REQUEST=GetFeatureInfo",
                    "LAYERS=0", "QUERY_LAYERS=0", "STYLES=", "CRS=EPSG:4326",
                    $"BBOX={minxStr},{minyStr},{maxxStr},{maxyStr}",
                    $"WIDTH={width}", $"HEIGHT={height}",
                    $"I={I}", $"J={J}",
                    "INFO_FORMAT=text/xml"
                };

                string fullUrl = url + "?" + string.Join("&", query);

                string response = await _httpClient.GetStringAsync(fullUrl);

                var doc = XDocument.Parse(response);
                XNamespace esri = "http://www.esri.com/wms";
                var fields = doc.Descendants(esri + "FIELDS").FirstOrDefault();

                if (fields == null) return ("Nieznany", "Nieznany", "Nieznany");

                return (
                    (string?)fields.Attribute("KOMPLEKS") ?? "",
                    (string?)fields.Attribute("TYPPODTYP") ?? "",
                    (string?)fields.Attribute("PODLOZE1") ?? ""
                );
            }
            catch
            {
                return ("Brak danych", "Błąd API", "");
            }
        }
    }
}