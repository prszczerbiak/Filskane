using System.Net.Http;
using System.Xml.Linq;
using WebApplication1.Models;
using WebApplication1.DAL; // Używamy nowych DALi

namespace WebApplication1.Services;

public class FarmService
{
    private readonly FarmDAL _farmDal;
    private readonly FieldDAL _fieldDal;
    private readonly SettingsDAL _settingsDal; // Potrzebny do pobrania info o farmie (UserDetail)
    private readonly HttpClient _httpClient;

    // Konstruktor wstrzykuje 3 DALe i HttpClienta
    public FarmService(FarmDAL farmDal, FieldDAL fieldDal, SettingsDAL settingsDal, HttpClient httpClient)
    {
        _farmDal = farmDal;
        _fieldDal = fieldDal;
        _settingsDal = settingsDal;
        _httpClient = httpClient;
    }

    // --- INFORMACJE O FARMIE ---

    public async Task<UserDetailDto?> GetCurrentFarmInfoAsync(string username)
    {
        // Delegujemy do SettingsDAL, który ma metodę pobierania szczegółów usera (w tym farmy)
        return await _settingsDal.GetLongInfoAsync(username);
    }

    public async Task SetFarmCoordsAsync(string username, double? x, double? y)
    {
        await _farmDal.SaveFarmCoordinatesAsync(username, x, y);
    }

    public async Task DeleteFarmCoordsAsync(string username)
    {
        await _farmDal.DeleteFarmCoordinatesAsync(username);
    }

    // --- ZARZĄDZANIE POLAMI (Proxy do FieldDAL) ---
    // FarmService może udostępniać metody FieldDAL, jeśli kontroler tego wymaga,
    // choć docelowo FieldController mógłby gadać bezpośrednio z FieldService.

    public async Task<List<FieldShortDto>> GetUserFieldsAsync(string username)
    {
        return await _fieldDal.GetUserFieldsAsync(username);
    }

    public async Task DeleteFieldAsync(string username, int fieldId)
    {
        await _fieldDal.DeleteFieldAsync(username, fieldId);
    }

    // --- LOGIKA BIZNESOWA (GEOPORTAL + ZAPIS) ---

    public async Task<int> AddFieldWithSoilInfoAsync(string username, SaveFieldRequest dto)
    {
        // 1. Logika Biznesowa: Pobranie danych z zewnętrznego API (Geoportal)
        var (complex, type, substrate) = await FetchSoilDetails(dto.CenterX, dto.CenterY);

        // 2. Warstwa Danych: Zapis do bazy przy użyciu FieldDAL
        // FieldDAL ma metodę SaveFieldAsync przygotowaną wcześniej
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

    // Metoda pomocnicza (pozostaje bez zmian, bo to czysta logika HTTP/XML)
    private async Task<(string complex, string type, string substrate)> FetchSoilDetails(double x, double y)
    {
        try
        {
            string url = "https://mapy.geoportal.gov.pl/wss/service/pub/guest/MapaGlebowoRolnicza/MapServer/WMSServer";
            // Mały bufor wokół punktu
            double minx = x - 0.001;
            double miny = y - 0.001;
            double maxx = x + 0.001;
            double maxy = y + 0.001;

            int width = 800, height = 600;
            // Przeliczenie współrzędnych geograficznych na piksele obrazka
            int I = (int)((x - minx) / (maxx - minx) * width);
            int J = (int)((maxy - y) / (maxy - miny) * height);

            var query = new[]
            {
                "SERVICE=WMS", "VERSION=1.3.0", "REQUEST=GetFeatureInfo",
                "LAYERS=0", "QUERY_LAYERS=0", "STYLES=", "CRS=EPSG:4326",
                $"BBOX={minx},{miny},{maxx},{maxy}",
                $"WIDTH={width}", $"HEIGHT={height}",
                $"I={I}", $"J={J}",
                "INFO_FORMAT=text/xml"
            };

            string fullUrl = url + "?" + string.Join("&", query);

            // Pobranie danych
            string response = await _httpClient.GetStringAsync(fullUrl);

            // Parsowanie XML
            var doc = XDocument.Parse(response);
            XNamespace esri = "http://www.esri.com/wms";
            var fields = doc.Descendants(esri + "FIELDS").FirstOrDefault();

            if (fields == null) return ("Nieznany", "Nieznany", "Nieznany");

            return (
                (string)fields.Attribute("KOMPLEKS") ?? "",
                (string)fields.Attribute("TYPPODTYP") ?? "",
                (string)fields.Attribute("PODLOZE1") ?? ""
            );
        }
        catch
        {
            // Fallback w razie awarii Geoportalu
            return ("Brak danych", "Błąd API", "");
        }
    }
}