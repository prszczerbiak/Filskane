
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

    public FarmController(DatabaseService dbService)
    {
        _dbService = dbService;
       
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



        string complex = (string)fields.Attribute("KOMPLEKS");
        string type = (string)fields.Attribute("TYPPODTYP");
        string substrate = (string)fields.Attribute("PODLOZE1"); 

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
