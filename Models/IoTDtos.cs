namespace Filskane.Models;

/// <summary>
/// Żądanie uruchomienia pobierania danych IoT z symulatora TCP.
/// </summary>
/// <param name="Lat">Szerokość geograficzna punktu startowego.</param>
/// <param name="Lng">Długość geograficzna punktu startowego.</param>
/// <param name="Type">Typ maszyny, np. tractor.</param>
/// <param name="IntervalSeconds">Opcjonalny interwał wysyłania danych.</param>
public record IoTStartTrackingRequest(
    double Lat,
    double Lng,
    string? Type = null,
    double? IntervalSeconds = null
);

/// <summary>
/// Żądanie uruchomienia śledzenia pojedynczego pojazdu na podstawie jego adresu TCP.
/// </summary>
/// <param name="Host">Host/IP urządzenia.</param>
/// <param name="Port">Port TCP urządzenia.</param>
/// <param name="InitialLat">Początkowa szerokość geograficzna markera.</param>
/// <param name="InitialLng">Początkowa długość geograficzna markera.</param>
/// <param name="Type">Typ/etykieta maszyny.</param>
/// <param name="IntervalSeconds">Opcjonalny interwał odczytu.</param>
public record IoTVehicleTrackingRequest(
    string Host,
    int Port,
    double InitialLat,
    double InitialLng,
    string? Type = null,
    double? IntervalSeconds = null
);

/// <summary>
/// Jeden rekord telemetryczny odebrany z urządzenia IoT.
/// </summary>
/// <param name="Type">Typ maszyny.</param>
/// <param name="Lat">Szerokość geograficzna.</param>
/// <param name="Lng">Długość geograficzna.</param>
/// <param name="Timestamp">Znacznik czasu UTC.</param>
public record IoTMachineTelemetryDto(
    string Type,
    double Lat,
    double Lng,
    DateTime Timestamp
);

/// <summary>
/// Status aktualnego połączenia IoT.
/// </summary>
/// <param name="IsTracking">Czy serwis próbuje odbierać dane.</param>
/// <param name="IsConnected">Czy połączenie TCP jest aktualnie aktywne.</param>
/// <param name="SimulatorHost">Host symulatora TCP.</param>
/// <param name="SimulatorPort">Port symulatora TCP.</param>
/// <param name="MachineType">Typ aktualnie śledzonej maszyny.</param>
/// <param name="LastLat">Ostatnia szerokość geograficzna.</param>
/// <param name="LastLng">Ostatnia długość geograficzna.</param>
/// <param name="LastTimestamp">Data i czas ostatniej próbki.</param>
public record IoTMachineStatusDto(
    bool IsTracking,
    bool IsConnected,
    string SimulatorHost,
    int SimulatorPort,
    string MachineType,
    double? LastLat,
    double? LastLng,
    DateTime? LastTimestamp
);