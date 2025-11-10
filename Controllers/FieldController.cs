using BitMiracle.LibTiff.Classic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OSGeo.GDAL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Services;
using WebApplication1.Utils;

[ApiController]
[Route("api/field")]
public class FieldController : ControllerBase
{
    [DllImport("gdal.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr VSIGetMemFileBuffer(string filename, out long size, int unref);

    private readonly DatabaseService _db;
    private readonly NdviAnalysisService _analysisService;
    public FieldController(DatabaseService db, NdviAnalysisService ndviAnalysisService)
    {
        _db = db;
        _analysisService = ndviAnalysisService;
    }

    [HttpGet("getData/{fieldId}")]
    public IActionResult GetFieldInfo([FromRoute] int fieldId)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized("Brak użytkownika w tokenie");

        var field = _db.GetUserFieldById(username, fieldId);
        if (field == null)
            return NotFound("Nie znaleziono pola");

        return Ok(field);
    }
    [HttpPut("update/{fieldId}")]
    public IActionResult UpdateFieldInfo(int fieldId, [FromBody] UpdateFieldDto updateDto)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized("Brak użytkownika w tokenie");

        try
        {
            _db.SaveFieldChanges(fieldId, updateDto);

            // pobierz zaktualizowane dane i zwróć je
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("getCycle/{fieldId}")]
    public IActionResult GetCycle(int fieldId)
    {
        //var username = User.Identity?.Name;
        //if (string.IsNullOrEmpty(username))
        //    return Unauthorized("Brak użytkownika w tokenie");
        var cycle = _db.GetCycleById(fieldId);
        if (cycle == null)
            return NotFound(new { error = "Nie znaleziono cyklu" });

        return Ok(cycle);
    }

    [HttpGet("latestScan/{fieldId}")]
    public async Task<IActionResult> GetLatestScan(int fieldId)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized("Brak użytkownika w tokenie");

        try
        {
            var result = await _db.GetLatestScanAsync(fieldId);

            if (result == null || result.ImageBytes == null)
            {
                Response.Headers.Append("X-Scan-Date", "Brak danych");
                return NoContent();

            }
            // 📅 Dodaj datę do nagłówka
            Response.Headers.Append("X-Scan-Date", result.ScanDate.ToString("yyyy-MM-dd"));

            // 🖼️ Zwróć PNG jako strumień binarny
            return File(ImageUtils.DrawGeoJsonPolygonOnImage(ScanResult.ConvertTiffBytesToRgbPng(result.ImageBytes),result.FieldBbox,result.Bbox, false), "image/png");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("getScansHistory/{fieldId}")]
    public async Task<IActionResult> GetScans(int fieldId)
    {
        try
        {
            var scans = await _db.GetFieldScansAsync(fieldId);
            return Ok(scans);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("deleteScan/{scanId}")]
    public async Task<IActionResult> DeleteScan(int scanId)
    {
        var deleted = await _db.DeleteScanAsync(scanId);
        if (!deleted) return NotFound(new { error = "Skan nie istnieje" });

        return Ok(new { message = "Skan został usunięty" });
    }


    [HttpPost("uploadScan/{fieldId}")]
    public async Task<IActionResult> UploadScan(int fieldId, [FromForm] UpdateTiffsDto data)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized("Brak użytkownika w tokenie");

        if (data.Zip == null || data.Zip.Length == 0)
            return BadRequest("Nie przesłano pliku ZIP.");

        Gdal.AllRegister();
        string[] expectedBands = { "B02", "B03", "B04", "B08" };
        var bandDatasets = new Dictionary<string, Dataset>();
        string outPath = $"/vsimem/merged_{fieldId}.tif";

        try
        {
            // 1️⃣ Odczyt pliku ZIP do pamięci
            using var zipStream = new MemoryStream();
            await data.Zip.CopyToAsync(zipStream);
            zipStream.Position = 0;

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) &&
                    !entry.FullName.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                    continue;

                string nameUpper = Path.GetFileNameWithoutExtension(entry.Name).ToUpper();
                var band = expectedBands.FirstOrDefault(b => nameUpper.Contains(b));
                if (band == null) continue;

                // 2️⃣ Wczytaj pasmo do pamięci (vsimem)
                using var bandStream = entry.Open();
                using var ms = new MemoryStream();
                await bandStream.CopyToAsync(ms);
                byte[] bytes = ms.ToArray();

                string vsiPath = $"/vsimem/{fieldId}_{band}.tif";
                Gdal.FileFromMemBuffer(vsiPath, bytes);
                var ds = Gdal.Open(vsiPath, Access.GA_ReadOnly);
                if (ds == null)
                    return StatusCode(500, $"Nie udało się otworzyć pasma {band}");

                bandDatasets[band] = ds;
            }

            // 3️⃣ Sprawdzenie kompletności
            var missing = expectedBands.Where(b => !bandDatasets.ContainsKey(b)).ToList();
            if (missing.Any())
                return BadRequest($"Brakuje pasm: {string.Join(", ", missing)}");

            // 4️⃣ Utworzenie scalonego GeoTIFF-a w pamięci
            int xSize = bandDatasets["B02"].RasterXSize;
            int ySize = bandDatasets["B02"].RasterYSize;
            var driver = Gdal.GetDriverByName("GTiff");

            using var outDs = driver.Create(outPath, xSize, ySize, 4, DataType.GDT_Int16, ["INTERLEAVE=PIXEL"]);

            for (int i = 0; i < expectedBands.Length; i++)
            {
                var ds = bandDatasets[expectedBands[i]];
                var band = ds.GetRasterBand(1);
                float[] buffer = new float[xSize * ySize];
                band.ReadRaster(0, 0, xSize, ySize, buffer, xSize, ySize, 0, 0);
                outDs.GetRasterBand(i + 1).WriteRaster(0, 0, xSize, ySize, buffer, xSize, ySize, 0, 0);
            }

            // Skopiowanie geotransformacji
            double[] gtSrc = new double[6];
            bandDatasets["B02"].GetGeoTransform(gtSrc);
            outDs.SetGeoTransform(gtSrc);

            // Skopiowanie projekcji (SRID)
            string proj = bandDatasets["B02"].GetProjectionRef();
            if (!string.IsNullOrEmpty(proj))
            {
                outDs.SetProjection(proj);
            }

            outDs.FlushCache();

            double[] gt = new double[6];
            outDs.GetGeoTransform(gt);

            double minX = gt[0];
            double maxY = gt[3];
            double maxX = minX + gt[1] * outDs.RasterXSize + gt[2] * outDs.RasterYSize;
            double minY = maxY + gt[4] * outDs.RasterXSize + gt[5] * outDs.RasterYSize;

            var bboxObj = new
            {
                minX,
                minY,
                maxX,
                maxY
            };
            string bboxJson = JsonSerializer.Serialize(bboxObj);


            // 5️⃣ Pobranie z /vsimem/ jako byte[]
            long size;
            IntPtr ptr = VSIGetMemFileBuffer(outPath, out size, 0);
            if (ptr == IntPtr.Zero)
                return StatusCode(500, "Nie udało się pobrać bufora z pamięci.");

            byte[] mergedBytes = new byte[size];
            Marshal.Copy(ptr, mergedBytes, 0, (int)size);

            // 7️⃣ Zapis do bazy danych (jeśli potrzebny)
            await _db.SaveRasterAsync(mergedBytes, fieldId, data.Date, bboxJson);

            return Ok(new { message = "Scalono TIFF w pamięci.", size = mergedBytes.Length });
        }
        catch (Exception ex)
        {
            // Możesz tu dodać logger, np. _logger.LogError(ex, "Błąd podczas przetwarzania TIFF");
            return StatusCode(500, $"Wystąpił błąd podczas przetwarzania danych: {ex.Message}");
        }
        finally
        {
            // 6️⃣ Zwolnienie pamięci
            try
            {
                Gdal.Unlink(outPath);

                foreach (var kv in bandDatasets)
                {
                    string vsi = $"/vsimem/{fieldId}_{kv.Key}.tif";
                    Gdal.Unlink(vsi);
                }
            }
            catch
            {
                // Ignoruj błędy zwalniania zasobów
            }
        }
    }

    [HttpGet("latestNDVI/{fieldId}")]
    public async Task<IActionResult> GetLatestNDVI(int fieldId)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized("Brak użytkownika w tokenie");

        try
        {
            var result = await _db.GetLatestScanAsync(fieldId);

            if (result == null || result.ImageBytes == null)
            {
                Response.Headers.Append("X-Scan-Date", "Brak danych");
                return NoContent();

            }
            // 📅 Dodaj datę do nagłówka
            Response.Headers.Append("X-Scan-Date", result.ScanDate.ToString("yyyy-MM-dd"));


            // 🖼️ Zwróć PNG jako strumień binarny
            //byte[] ndvi = ImageUtils.DrawGeoJsonPolygonOnImage(ScanResult.ConvertTiffToNdviHeatmap(result.ImageBytes, result.FieldBbox), result.FieldBbox, result.Bbox, true);
            return File(ImageUtils.DrawGeoJsonPolygonOnImage(ScanResult.RenderNdviHeatmap(ScanResult.CalculateNdvi(result.ImageBytes)), result.FieldBbox, result.Bbox, true),"image/png");

        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("group/{fieldId}")]
    public async Task<IActionResult> Group(int fieldId, [FromBody] NdviGroupRequest request)
    {
        if (request == null)
            return BadRequest("Brak danych wejściowych.");

        if (string.IsNullOrEmpty(request.CropType) || request.SowingDate == DateTime.MinValue)
            return BadRequest("Niepoprawne dane rośliny lub daty zasiewu.");

        // 1️⃣ Pobierz najnowszy skan NDVI dla pola
        var result = await _db.GetLatestScanAsync(fieldId);
        if (result == null)
            return NotFound($"Brak skanu dla pola {fieldId}");

        // 2️⃣ Wygeneruj klasy ryzyka
        var overlay = _analysisService.GroupByRisk(request, result.ImageBytes, result.FieldBbox, result.Bbox);

        // 3️⃣ Wyrenderuj mapę NDVI w formie heatmapy
        var ndviMap = ScanResult.RenderNdviHeatmap(ScanResult.CalculateNdvi(result.ImageBytes));

        // 4️⃣ Nałóż półprzezroczystą mapę ryzyka
        var withOverlay = ImageUtils.ApplyRiskOverlay(ndviMap, overlay);

        // 5️⃣ Dorysuj granice pola z GeoJSON
        var finalImage = ImageUtils.DrawGeoJsonPolygonOnImage(withOverlay, result.FieldBbox, result.Bbox, true);

        // 6️⃣ Zwróć obraz jako PNG
        return File(finalImage, "image/png");
    }

    [HttpGet("getPlantsList")]
    public async Task<IActionResult> GetPlantsList()
    {
        try
        {
            // Pobierz listę roślin z serwisu bazy danych
            var plants = await _db.GetPlantsAsync();

            // Zwróć w formacie JSON
            return Ok(plants);
        }
        catch (Exception ex)
        {
            // Obsłuż błędy
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
