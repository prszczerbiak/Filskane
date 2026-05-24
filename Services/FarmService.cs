using System.Xml.Linq;
using Filskane.Models;
using Filskane.DAL;

namespace Filskane.Services
{
    /// <summary>
    /// Serwis biznesowy zarządzający danymi gospodarstwa (lokalizacja bazy) oraz polami uprawnymi.
    /// Integruje się z zewnętrznym API Geoportalu w celu pobierania danych glebowych.
    /// </summary>
    public class FarmService
    {
        private readonly FarmDAL _farmDal;
        private readonly FieldDAL _fieldDal;
        private readonly VehicleDAL _vehicleDal;
        private readonly SettingsDAL _settingsDal;
        private readonly HttpClient _httpClient;
        private const double DefaultFarmLatitude = 50.800667;
        private const double DefaultFarmLongitude = 19.124278;

        public FarmService(FarmDAL farmDal, FieldDAL fieldDal, VehicleDAL vehicleDal, SettingsDAL settingsDal, HttpClient httpClient)
        {
            _farmDal = farmDal;
            _fieldDal = fieldDal;
            _vehicleDal = vehicleDal;
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
        /// Pobiera pojazdy przypisane do użytkownika i nadaje im pozycje na mapie wokół gospodarstwa.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <returns>Lista pojazdów z wygenerowanymi pozycjami mapowymi.</returns>
        public async Task<List<VehicleMapDto>> GetUserVehiclesAsync(string username)
        {
            var farmInfo = await _settingsDal.GetLongInfoAsync(username);
            var vehicles = await _vehicleDal.GetUserVehiclesAsync(username);

            if (vehicles.Count == 0)
                return [];

            double baseLat = farmInfo?.FarmY ?? DefaultFarmLatitude;
            double baseLng = farmInfo?.FarmX ?? DefaultFarmLongitude;

            return GenerateVehicleMapPositions(baseLat, baseLng, vehicles);
        }

        /// <summary>
        /// Dodaje nowy pojazd dla użytkownika.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="dto">Dane pojazdu.</param>
        /// <returns>ID nowo dodanego pojazdu.</returns>
        public async Task<int> AddVehicleAsync(string username, AddVehicleRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.VehicleName))
                throw new ArgumentException("Nazwa pojazdu nie może być pusta.", nameof(dto.VehicleName));

            if (string.IsNullOrWhiteSpace(dto.IpAdress))
                throw new ArgumentException("Adres IP pojazdu nie może być pusty.", nameof(dto.IpAdress));

            if (dto.TcpPort is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(dto.TcpPort), "Port TCP musi mieścić się w zakresie 1..65535.");

            return await _vehicleDal.AddVehicleAsync(username, dto.VehicleName.Trim(), dto.IpAdress.Trim(), dto.TcpPort);
        }

        /// <summary>
        /// Usuwa pojazd należący do użytkownika.
        /// </summary>
        public async Task<bool> DeleteVehicleAsync(string username, int vehicleId)
        {
            if (vehicleId <= 0)
                throw new ArgumentOutOfRangeException(nameof(vehicleId), "ID pojazdu musi być większe od zera.");

            return await _vehicleDal.DeleteVehicleAsync(username, vehicleId);
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

            Console.WriteLine($"Pobieranie danych glebowych dla pola '{dto.Name}': Kompleks={complex}, Typ={type}, Podłoże={substrate}");

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

                if (fields == null) return (null, null, null);

                string? complex = (string?)fields.Attribute("KOMPLEKS");
                string? type = (string?)fields.Attribute("TYPPODTYP");
                string? substrate = (string?)fields.Attribute("PODLOZE1");

                return (
                    complex?.Equals("Null", StringComparison.OrdinalIgnoreCase) == true ? null : complex,
                    type?.Equals("Null", StringComparison.OrdinalIgnoreCase) == true ? null : type,
                    substrate?.Equals("Null", StringComparison.OrdinalIgnoreCase) == true ? null : substrate
                );
            }
            catch
            {
                return ("Brak danych", "Błąd API", "");
            }
        }

        private static List<VehicleMapDto> GenerateVehicleMapPositions(
            double baseLat,
            double baseLng,
            IReadOnlyList<VehicleBaseDto> vehicles)
        {
            var result = new List<VehicleMapDto>(vehicles.Count);
            if (vehicles.Count == 0) return result;

            const double baseRadiusMeters = 18.0;
            const double radiusStepMeters = 7.0;
            double latitudeScale = 111_000.0;
            double longitudeScale = 111_000.0 * Math.Max(Math.Cos(baseLat * Math.PI / 180.0), 0.1);

            for (int i = 0; i < vehicles.Count; i++)
            {
                var vehicle = vehicles[i];
                double angle = (2.0 * Math.PI * i) / vehicles.Count;
                double radiusMeters = baseRadiusMeters + (i / 8) * radiusStepMeters;

                double lat = baseLat + (Math.Sin(angle) * radiusMeters / latitudeScale);
                double lng = baseLng + (Math.Cos(angle) * radiusMeters / longitudeScale);

                result.Add(new VehicleMapDto(vehicle.Id, vehicle.Name, vehicle.IpAdress, vehicle.TcpPort, lat, lng));
            }

            return result;
        }
    }
}