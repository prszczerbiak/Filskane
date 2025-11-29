
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Xml.Linq;
using WebApplication1.Models;
using WebApplication1.Services;

[Authorize]
[Route("api/farm")]
[ApiController]
public class FarmController : ControllerBase
{
    private readonly DatabaseService _dbService;
    private readonly Dictionary<string, string> _complexes;
    private readonly Dictionary<string, string> _types;
    private readonly Dictionary<string, string> _substrates;

    public FarmController(DatabaseService dbService)
    {
        _dbService = dbService;
        _complexes = new Dictionary<string, string>
        {
            //gleby orne
            { "1", "pszenny bardzo dobry" },
            { "2", "pszenny dobry" },
            { "3", "pszenny wadliwy" },
            { "4", "żytni bardzo dobry" },
            { "5", "żytni dobry" },
            { "6", "żytni słaby" },
            { "7", "żytni bardzo słaby" },
            { "8", "zbożowo-pastewny mocny" },
            { "9", "zbożowo-pastewny słaby" },
            { "10", "zbożowo-pastewny górski" },
            { "11", "pszenny górski" },
            { "12", "owsiano-ziemniaczany górski" },
            { "13", "owsiano-pastewny górski" },
            { "14", "gleby orne przeznaczone pod użytki zielone" },
            { "GO", "grunt orny (woj. dolnośląskie)" },

            //trwałe użytki zielone
            { "1z", "użytki zielone bardzo dobre i dobre" },
            { "2z", "użytki zielone średnie" },
            { "3z", "użytki zielone słabe i bardzo słabe" },
            { "TUZ", "trwałe użytki zielone (woj. dolnośląskie)" },

            //inne
            { "Tnk", "teren nieklasyfikowany" },
            { "Ls", "las" },
            { "Lz", "zadrzewienie" },
            { "N", "nieużytki rolne" },
            { "RN", "gleby rolniczo nieprzydatne (pod zalesienie)" },
            { "Tz", "teren zabudowany" },
            { "W", "wody" },
            { "WN", "wody nieużytki" },
            { "Null", "brak informacji" }
        };

        _types = new Dictionary<string, string>
        {
            { "A", "gleby bielicowe właściwe i pseudobielicowe" },
            { "Ad", "gleby bielicowe właściwe i pseudobielicowe deluwialne" },
            { "B", "gleby brunatne właściwe" },
            { "Bd", "gleby brunatne deluwialne" },
            { "Bk", "gleby brunatne kwaśne" },
            { "Bkd", "gleby brunatne kwaśne deluwialne" },
            { "Bw", "gleby brunatne wyługowane" },
            { "Bwd", "gleby brunatne wyługowane deluwialne" },
            { "C", "czarnoziemy właściwe" },
            { "Cd", "czarnoziemy właściwe deluwialne" },
            { "Cz", "czarnoziemy zdegradowane i gleby szare" },
            { "Czd", "czarnoziemy zdegradowane i gleby szare deluwialne" },
            { "Cz1", "czarnoziemy właściwe" },
            { "Dd", "czarne ziemie właściwe deluwialne" },
            { "D", "czarne ziemie właściwe" },
            { "DG", "czarne ziemie glejowe" },
            { "DGd", "czarne ziemie glejowe deluwialne" },
            { "Dz", "czarne ziemie zdegradowane i ziemie szare" },
            { "Dzd", "czarne ziemie zdegradowane i ziemie szare deluwialne" },
            { "DzG", "czarne ziemie zdegradowane i ziemie szare glejowe" },
            { "DzGd", "czarne ziemie zdegradowane i ziemie szare glejowe deluwialne" },
            { "E", "gleby mułowo-torfowe i torfowo-mułowe" },
            { "Ed", "gleby mułowo-torfowe i torfowo-mułowe deluwialne" },
            { "Etm", "gleby torfowo-mułowe" },
            { "F", "mady" },
            { "Fb", "mady brunatne" },
            { "Fc", "mady czarnoziemne" },
            { "Fd", "mady deluwialne" },
            { "Fg", "mady glejowe" },
            { "FGd", "mady glejowe deluwialne" },
            { "G", "gleby glejowe" },
            { "G1", "gleby glejowe (opadowo-glejowe lub gleby gruntowo-glejowe)" },
            { "Gd", "gleby glejowe deluwialne (opadowo-glejowe lub gleby gruntowo-glejowe)" },
            { "Gm", "gleby murszowo-mineralne i gleby murszowate" },
            { "Gm1", "gleby murszowo-mineralne i gleby murszowate deluwialne" },
            { "R", "rędziny o słabo wykształconym profilu (inicjalne)" },
            { "Rb", "rędziny brunatne" },
            { "Rc", "rędziny próchniczne (czarnoziemne i szare)" },
            { "Rg", "rędziny gipsowe (siarczanowe)" },
            { "T", "gleby torfowe i murszowo-torfowe" },
            { "Null", "brak informacji" }
        };

        _substrates = new Dictionary<string, string>
        {
            // Gatunki gleb
            { "zp", "żwiry piaszczyste" },
            { "zg", "żwiry gliniaste" },
            { "pl", "piaski luźne" },
            { "plp", "piaski luźne pylaste" },
            { "ps", "piaski słabo-gliniaste" },
            { "psp", "piaski słabo-gliniaste pylaste" },
            { "pgl", "piaski gliniaste lekkie" },
            { "pglp", "piaski gliniaste lekkie pylaste" },
            { "pgm", "piaski gliniaste mocne" },
            { "pgmp", "piaski gliniaste mocne pylaste" },
            { "gl", "gliny lekkie" },
            { "glp", "gliny lekkie pylaste" },
            { "gs", "gliny średnie" },
            { "gsp", "gliny średnie pylaste" },
            { "gc", "gliny ciężkie" },
            { "gcp", "gliny ciężkie pylaste" },
            { "i", "iły (gleby ilaste bardzo ciężkie)" },
            { "ip", "iły pylaste (gleby pylowe lekkie i średnie)" },
            { "pi", "pyły zwykłe (gleby pyłowe lekkie i średnie)" },
            { "pi+", "pyły ilaste (gleby pyłowe mocne)" },
            { "l", "lessy i utwory lessowate (gleby lessowe i lessowate lekkie i średnie)" },
            { "li", "lessy i utwory lessowate ilaste (gleby lessowe i lessowate mocne)" },
            { "w", "skała wapienna" },
            { "d", "utwory deluwialne" },
            { "dl", "deluwia lekkie" },
            { "ds", "deluwia średnie" },
            { "dc", "deluwia ciężkie" },

            // Gatunki gleb wytworzonych ze skał masywnych niewapiennych
            { "p", "gleby piaszczyste" },
            { "g", "gleby gliniaste" },
            { "py", "gleby pyłowe" },
            { "sz", "gleby szkieletowe" },
            { "sk", "gleby skaliste (skały)" },
            { "Null", "brak informacji" }
        };
    }


    [HttpGet("getFarm")]
    public IActionResult GetCurrentFarm()
    {
        string? username = User.Identity?.Name;

        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = _dbService.GetUserInfo(username);

        if (user == null) return NotFound("Nie znaleziono użytkownika.");

        return Ok(new { user.Username, user.FarmX, user.FarmY });
    }

    [HttpPost("setCoords")]
    public IActionResult SetFarmCoords([FromBody] FarmCords coords)
    {
        string username = User.Identity?.Name!;

        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        _dbService.SaveFarmCoordinates(username, coords.FarmX, coords.FarmY);
        return Ok(new { message = "Koordynaty zostały zapisane." });
    }

    [HttpDelete("deleteFarm")]
    public IActionResult DeleteFarmCoords()
    {
        string username = User.Identity?.Name!;

        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        _dbService.DeleteFarmCoordinates(username);
        return Ok(new { message = "Koordynaty zostały usunięte." });
    }

    [HttpPost("saveField")]
    public async Task<IActionResult> SaveField([FromBody] SaveFieldDto dto)
    {
        string? username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        if (string.IsNullOrEmpty(dto.Geojson))
        {
            return BadRequest(new { error = "Pole GeoJSON nie może być puste." });
        }

        string url = "https://mapy.geoportal.gov.pl/wss/service/pub/guest/MapaGlebowoRolnicza/MapServer/WMSServer";

        double minx = dto.CenterX - 0.001;
        double miny = dto.CenterY - 0.001;
        double maxx = dto.CenterX + 0.001;
        double maxy = dto.CenterY + 0.001;

        int width = 800, height = 600;

        int I = (int)((dto.CenterX - minx) / (maxx - minx) * width);
        int J = (int)((maxy - dto.CenterY) / (maxy - miny) * height);

        var query = new[]
        {
            "SERVICE=WMS",
            "VERSION=1.3.0",
            "REQUEST=GetFeatureInfo",
            "LAYERS=0",
            "QUERY_LAYERS=0",
            "STYLES=",
            "CRS=EPSG:4326",
            $"BBOX={minx},{miny},{maxx},{maxy}",
            $"WIDTH={width}",
            $"HEIGHT={height}",
            $"I={I}",
            $"J={J}",
            "INFO_FORMAT=text/xml"
        };

        string fullUrl = url + "?" + string.Join("&", query);

        using var client = new HttpClient();
        string response = await client.GetStringAsync(fullUrl);

        // Parsowanie XML
        var doc = XDocument.Parse(response);
        XNamespace esri = "http://www.esri.com/wms";

        var fields = doc.Descendants(esri + "FIELDS").FirstOrDefault();



        string complex = _complexes[(string)fields.Attribute("KOMPLEKS")];
        string type = _types[(string)fields.Attribute("TYPPODTYP")];
        string substrate = _substrates[(string)fields.Attribute("PODLOZE1")]; //jedynie pierwsza warstwa podłoża

        try
        {

            int fieldId = _dbService.SaveField(username, dto.Name, dto.Geojson, dto.CenterX, dto.CenterY, dto.Area, complex, type, substrate);
            return Ok(new { fieldId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("getUserFields")]
    public IActionResult GetUserFields()
    {
        
        var username = User.Identity?.Name;

        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        try
        {
            var fields = _dbService.GetUserFields(username);
            return Ok(fields);
        }
        catch (OracleException ex)
        {
            return StatusCode(500, new { error = $"Oracle error {ex.Number}: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("deleteField/{fieldId}")]
    public IActionResult DeleteField(int fieldId)
    {
        var username = User.Identity?.Name!;

        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        try
        {
            _dbService.DeleteField(username, fieldId);
            return Ok(new { message = "Pole usunięte" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
