using BitMiracle.LibTiff.Classic;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebApplication1.Models;
using WebApplication1.Services;
using WebApplication1.Utils;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/ndvi")]
    [Obsolete("Ten kontroler jest przestarzały. Funkcjonalność bezpośredniego pobierania skanów pól została wyłączona. ")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class NDVIController : ControllerBase
    {
        private readonly SentinelHubService _sentinelService;
        private readonly DatabaseService _databaseService;

        public NDVIController(SentinelHubService sentinelService, DatabaseService databaseService)
        {
            _sentinelService = sentinelService;
            _databaseService = databaseService;
        }

        //[HttpPost("download/{fieldId}")]
        //public async Task<IActionResult> Download([FromRoute] int fieldId)
        //{

        //    var geoJson = await _databaseService.GetFieldPolygonAsync(fieldId);
        //    if (geoJson == null)
        //        return NotFound(new { error = "Nie znaleziono pola" });

        //    var rectPolygon = RectangledPolygon.FromGeoJson(geoJson);
        //    var bbox = rectPolygon.GetBBoxArray();

        //    DateTime endDate = DateTime.UtcNow;
        //    DateTime startDate = endDate.AddDays(-2);

        //    try
        //    {
        //        // 4️⃣ Pobranie NDVI z Sentinel Hub
        //        var tiffData = await _sentinelService.GetNDVIAsync(
        //            bbox,
        //            startDate.ToString("yyyy-MM-dd"),
        //            endDate.ToString("yyyy-MM-dd")
        //        );

        //        // 5️⃣ Obsługa przypadku, gdy brak dostępnych obrazów
        //        if (tiffData == null || tiffData.Length == 0)
        //            return NotFound(new { error = "Brak dostępnych obrazów NDVI w ostatnich 2 dniach." });

        //        // 6️⃣ Zapis do bazy (opcjonalnie)
        //        await _databaseService.SaveRasterAsync(GeoTiffConverter.ConvertTiffFloat32ToUInt16(tiffData), fieldId, endDate);

        //        Console.WriteLine("Jest dobrze");

        //        // 7️⃣ Zwrócenie pliku GeoTIFF do klienta
        //        //return File(tiffData, "image/tiff", $"ndvi_field_{fieldId}.tiff");
        //        return (Ok(new { }));
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { error = "Błąd pobierania danych NDVI: " + ex.Message });
        //    }


        //}
    }
}
