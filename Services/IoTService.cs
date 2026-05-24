using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Filskane.Models;

namespace Filskane.Services;

/// <summary>
/// Serwis odpowiedzialny za odbieranie telemetrii maszyny po TCP i udostępnianie jej backendowi.
/// </summary>
public class IoTService
{
    private sealed class VehicleTrackingSession
    {
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public Task? TrackingTask { get; set; }
        public IoTVehicleTrackingRequest? Request { get; set; }
        public IoTMachineTelemetryDto? LatestTelemetry { get; set; }
        public bool IsTracking { get; set; }
        public bool IsConnected { get; set; }
    }

    private readonly IConfiguration _configuration;
    private readonly ILogger<IoTService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _syncRoot = new();
    private readonly ConcurrentDictionary<int, VehicleTrackingSession> _vehicleSessions = new();

    private CancellationTokenSource? _trackingCts;
    private Task? _trackingTask;
    private IoTStartTrackingRequest? _currentRequest;
    private IoTMachineTelemetryDto? _latestTelemetry;
    private bool _isTracking;
    private bool _isConnected;

    public IoTService(IConfiguration configuration, ILogger<IoTService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Uruchamia odczyt danych z symulatora TCP.
    /// </summary>
    public async Task StartTrackingAsync(IoTStartTrackingRequest request)
    {
        if (request.Lat is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(request.Lat), "Latitude musi mieścić się w zakresie -90..90.");

        if (request.Lng is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(request.Lng), "Longitude musi mieścić się w zakresie -180..180.");

        var normalizedRequest = new IoTStartTrackingRequest(
            request.Lat,
            request.Lng,
            string.IsNullOrWhiteSpace(request.Type)
                ? _configuration["IoT:DefaultMachineType"] ?? "tractor"
                : request.Type,
            request.IntervalSeconds ?? _configuration.GetValue<double?>("IoT:DefaultIntervalSeconds") ?? 0.1
        );

        await StopTrackingAsync().ConfigureAwait(false);

        var cts = new CancellationTokenSource();
        var task = Task.Run(() => TrackingLoopAsync(normalizedRequest, cts.Token), cts.Token);

        lock (_syncRoot)
        {
            _currentRequest = normalizedRequest;
            _trackingCts = cts;
            _trackingTask = task;
            _isTracking = true;
        }
    }

    /// <summary>
    /// Zatrzymuje bieżące połączenie TCP.
    /// </summary>
    public async Task StopTrackingAsync()
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_syncRoot)
        {
            cts = _trackingCts;
            task = _trackingTask;
            _trackingCts = null;
            _trackingTask = null;
            _currentRequest = null;
            _isTracking = false;
            _isConnected = false;
        }

        if (cts is null)
            return;

        try
        {
            cts.Cancel();
            if (task is not null)
                await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Pomijamy oczekiwane anulowanie.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd podczas zatrzymywania połączenia IoT.");
        }
        finally
        {
            cts.Dispose();
        }
    }

    /// <summary>
    /// Zwraca ostatnią odebraną próbkę telemetryczną.
    /// </summary>
    public IoTMachineTelemetryDto? GetLatestTelemetry()
    {
        lock (_syncRoot)
        {
            return _latestTelemetry;
        }
    }

    /// <summary>
    /// Zwraca status aktualnego połączenia i ostatnią znaną pozycję.
    /// </summary>
    public IoTMachineStatusDto GetStatus()
    {
        lock (_syncRoot)
        {
            return new IoTMachineStatusDto(
                _isTracking,
                _isConnected,
                _configuration["IoT:SimulatorHost"] ?? "localhost",
                _configuration.GetValue("IoT:SimulatorPort", 8001),
                _currentRequest?.Type ?? _configuration["IoT:DefaultMachineType"] ?? "tractor",
                _latestTelemetry?.Lat,
                _latestTelemetry?.Lng,
                _latestTelemetry?.Timestamp
            );
        }
    }

    /// <summary>
    /// Uruchamia śledzenie TCP dla pojedynczego pojazdu.
    /// </summary>
    public async Task StartVehicleTrackingAsync(int vehicleId, IoTVehicleTrackingRequest request)
    {
        if (vehicleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(vehicleId), "ID pojazdu musi być większe od zera.");

        if (string.IsNullOrWhiteSpace(request.Host))
            throw new ArgumentException("Host pojazdu nie może być pusty.", nameof(request.Host));

        if (request.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(request.Port), "Port TCP musi mieścić się w zakresie 1..65535.");

        var normalizedRequest = new IoTVehicleTrackingRequest(
            NormalizeHost(request.Host),
            request.Port,
            request.InitialLat,
            request.InitialLng,
            string.IsNullOrWhiteSpace(request.Type) ? $"vehicle-{vehicleId}" : request.Type,
            request.IntervalSeconds ?? _configuration.GetValue<double?>("IoT:DefaultIntervalSeconds") ?? 0.1
        );

        await StopVehicleTrackingAsync(vehicleId).ConfigureAwait(false);

        var session = new VehicleTrackingSession
        {
            CancellationTokenSource = new CancellationTokenSource(),
            Request = normalizedRequest,
            IsTracking = true,
            IsConnected = false
        };

        session.TrackingTask = Task.Run(
            () => TrackingLoopAsync(vehicleId, normalizedRequest, session, session.CancellationTokenSource.Token),
            session.CancellationTokenSource.Token);

        _vehicleSessions[vehicleId] = session;
    }

    /// <summary>
    /// Zatrzymuje śledzenie wybranego pojazdu.
    /// </summary>
    public async Task StopVehicleTrackingAsync(int vehicleId)
    {
        if (!_vehicleSessions.TryRemove(vehicleId, out var session))
            return;

        try
        {
            session.CancellationTokenSource?.Cancel();
            if (session.TrackingTask is not null)
                await session.TrackingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Błąd podczas zatrzymywania śledzenia pojazdu {VehicleId}.", vehicleId);
        }
        finally
        {
            session.CancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Pobiera ostatnią telemetryczną pozycję pojazdu.
    /// </summary>
    public IoTMachineTelemetryDto? GetLatestVehicleTelemetry(int vehicleId)
    {
        return _vehicleSessions.TryGetValue(vehicleId, out var session) ? session.LatestTelemetry : null;
    }

    private static string NormalizeHost(string host)
    {
        return host == "0.0.0.0" ? "127.0.0.1" : host;
    }

    private async Task TrackingLoopAsync(IoTStartTrackingRequest request, CancellationToken cancellationToken)
    {
        var host = _configuration["IoT:SimulatorHost"] ?? "localhost";
        var port = _configuration.GetValue("IoT:SimulatorPort", 8001);
        var reconnectDelaySeconds = _configuration.GetValue("IoT:ReconnectDelaySeconds", 2);

        var handshakePayload = JsonSerializer.Serialize(new
        {
            type = request.Type ?? "tractor",
            lat = request.Lat,
            lng = request.Lng,
            interval = request.IntervalSeconds ?? 0.1
        }, _jsonOptions);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
                using var cancellationRegistration = cancellationToken.Register(client.Close);

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
                {
                    AutoFlush = true
                };
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

                await writer.WriteLineAsync(handshakePayload).ConfigureAwait(false);

                SetConnectionState(true);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                        break;

                    IoTMachineTelemetryDto? telemetry;
                    try
                    {
                        telemetry = JsonSerializer.Deserialize<IoTMachineTelemetryDto>(line, _jsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Nieprawidłowa linia JSON odebrana z TCP: {Line}", line);
                        continue;
                    }

                    if (telemetry is null)
                        continue;

                    lock (_syncRoot)
                    {
                        _latestTelemetry = telemetry;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd połączenia TCP z symulatorem IoT.");
            }
            finally
            {
                SetConnectionState(false);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(reconnectDelaySeconds), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task TrackingLoopAsync(
        int vehicleId,
        IoTVehicleTrackingRequest request,
        VehicleTrackingSession session,
        CancellationToken cancellationToken)
    {
        var reconnectDelaySeconds = _configuration.GetValue("IoT:ReconnectDelaySeconds", 2);

        var handshakePayload = JsonSerializer.Serialize(new
        {
            type = request.Type ?? $"vehicle-{vehicleId}",
            lat = request.InitialLat,
            lng = request.InitialLng,
            interval = request.IntervalSeconds ?? 0.1
        }, _jsonOptions);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(request.Host, request.Port, cancellationToken).ConfigureAwait(false);
                using var cancellationRegistration = cancellationToken.Register(client.Close);

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
                {
                    AutoFlush = true
                };
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

                await writer.WriteLineAsync(handshakePayload).ConfigureAwait(false);

                lock (session)
                {
                    session.IsConnected = true;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                        break;

                    IoTMachineTelemetryDto? telemetry;
                    try
                    {
                        telemetry = JsonSerializer.Deserialize<IoTMachineTelemetryDto>(line, _jsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Nieprawidłowa linia JSON odebrana z TCP pojazdu {VehicleId}: {Line}", vehicleId, line);
                        continue;
                    }

                    if (telemetry is null)
                        continue;

                    lock (session)
                    {
                        session.LatestTelemetry = telemetry;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd połączenia TCP z pojazdem {VehicleId}.", vehicleId);
            }
            finally
            {
                lock (session)
                {
                    session.IsConnected = false;
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(reconnectDelaySeconds), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void SetConnectionState(bool isConnected)
    {
        lock (_syncRoot)
        {
            _isConnected = isConnected;
        }
    }
}
