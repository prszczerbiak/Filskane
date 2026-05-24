using Filskane.Models;
using Filskane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Filskane.Controllers;

/// <summary>
/// Kontroler obsługujący integrację IoT i dane telemetryczne maszyny.
/// </summary>
[ApiController]
[Authorize]
[Route("api/iot")]
public class IoTController : ControllerBase
{
    private readonly IoTService _iotService;
    private readonly ILogger<IoTController> _logger;

    public IoTController(IoTService iotService, ILogger<IoTController> logger)
    {
        _iotService = iotService;
        _logger = logger;
    }

    /// <summary>
    /// Uruchamia pobieranie danych z symulatora TCP dla wskazanej maszyny.
    /// </summary>
    [HttpPost("machine/start")]
    public async Task<IActionResult> StartMachineTracking([FromBody] IoTStartTrackingRequest request)
    {
        try
        {
            await _iotService.StartTrackingAsync(request);
            return Ok(_iotService.GetStatus());
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd uruchamiania śledzenia IoT.");
            return StatusCode(500, new { message = "Nie udało się uruchomić połączenia IoT." });
        }
    }

    /// <summary>
    /// Zatrzymuje aktywne pobieranie danych.
    /// </summary>
    [HttpPost("machine/stop")]
    public async Task<IActionResult> StopMachineTracking()
    {
        await _iotService.StopTrackingAsync();
        return Ok(new { message = "Śledzenie maszyny zatrzymane." });
    }

    /// <summary>
    /// Zwraca ostatnio odebrane dane telemetryczne.
    /// </summary>
    [HttpGet("machine/latest")]
    public IActionResult GetLatestMachineData()
    {
        var telemetry = _iotService.GetLatestTelemetry();
        if (telemetry is null)
            return NoContent();

        return Ok(telemetry);
    }

    /// <summary>
    /// Zwraca status połączenia IoT.
    /// </summary>
    [HttpGet("machine/status")]
    public IActionResult GetMachineStatus()
    {
        return Ok(_iotService.GetStatus());
    }

    /// <summary>
    /// Uruchamia śledzenie TCP dla pojedynczego pojazdu.
    /// </summary>
    [HttpPost("vehicle/start/{vehicleId}")]
    public async Task<IActionResult> StartVehicleTracking(int vehicleId, [FromBody] IoTVehicleTrackingRequest request)
    {
        try
        {
            await _iotService.StartVehicleTrackingAsync(vehicleId, request);
            return Ok(new { vehicleId });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd uruchamiania śledzenia pojazdu {VehicleId}.", vehicleId);
            return StatusCode(500, new { message = "Nie udało się uruchomić połączenia pojazdu." });
        }
    }

    /// <summary>
    /// Zatrzymuje śledzenie TCP dla wybranego pojazdu.
    /// </summary>
    [HttpPost("vehicle/stop/{vehicleId}")]
    public async Task<IActionResult> StopVehicleTracking(int vehicleId)
    {
        await _iotService.StopVehicleTrackingAsync(vehicleId);
        return Ok(new { message = "Śledzenie pojazdu zatrzymane." });
    }

    /// <summary>
    /// Zwraca ostatnią odebraną pozycję dla wybranego pojazdu.
    /// </summary>
    [HttpGet("vehicle/latest/{vehicleId}")]
    public IActionResult GetLatestVehicleData(int vehicleId)
    {
        var telemetry = _iotService.GetLatestVehicleTelemetry(vehicleId);
        if (telemetry is null)
            return NoContent();

        return Ok(telemetry);
    }
}
