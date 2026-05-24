namespace Filskane.Models
{
    /// <summary>
    /// Model danych wymagany do zapisania nowego pola w systemie.
    /// </summary>
    /// <param name="Name">Nazwa pola nadana przez użytkownika.</param>
    /// <param name="Geojson">Geometria pola w formacie GeoJSON.</param>
    /// <param name="CenterX">Współrzędna X (Długość) środka pola.</param>
    /// <param name="CenterY">Współrzędna Y (Szerokość) środka pola.</param>
    /// <param name="Area">Całkowita powierzchnia pola w metrach kwadratowych.</param>
    public record SaveFieldRequest(
        string Name,
        string Geojson,
        double CenterX,
        double CenterY,
        double Area
    );

    /// <summary>
    /// Model służący do aktualizacji lokalizacji gospodarstwa (bazy).
    /// </summary>
    /// <param name="FarmX">Długość geograficzna (Longitude). Null oznacza usunięcie.</param>
    /// <param name="FarmY">Szerokość geograficzna (Latitude). Null oznacza usunięcie.</param>
    public record FarmCoordsRequest(double? FarmX, double? FarmY);

    /// <summary>
    /// Skrócony obiekt DTO reprezentujący pole (używany np. na mapie lub listach).
    /// </summary>
    /// <param name="Id">Unikalny identyfikator pola.</param>
    /// <param name="Name">Nazwa pola.</param>
    /// <param name="CenterX">Współrzędna X środka.</param>
    /// <param name="CenterY">Współrzędna Y środka.</param>
    /// <param name="Geojson">Pełna geometria pola.</param>
    public record FieldShortDto(
        int Id,
        string Name,
        double CenterX,
        double CenterY,
        string Geojson
    );

    /// <summary>
    /// Minimalistyczny obiekt pola używany do budowania menu i list wyboru.
    /// </summary>
    /// <param name="Id">Identyfikator pola.</param>
    /// <param name="Name">Nazwa pola.</param>
    public record FieldListItemDto(int Id, string Name);

    /// <summary>
    /// Surowy rekord pojazdu zapisany w bazie danych.
    /// </summary>
    /// <param name="Id">Identyfikator pojazdu.</param>
    /// <param name="Name">Nazwa pojazdu.</param>
    /// <param name="IpAdress">Adres IP pojazdu.</param>
    /// <param name="TcpPort">Port TCP pojazdu.</param>
    public record VehicleBaseDto(
        int Id,
        string Name,
        string? IpAdress,
        int? TcpPort
    );

    /// <summary>
    /// Pojazd z wygenerowaną pozycją na mapie.
    /// </summary>
    /// <param name="Id">Identyfikator pojazdu.</param>
    /// <param name="Name">Nazwa pojazdu.</param>
    /// <param name="IpAdress">Adres IP pojazdu.</param>
    /// <param name="TcpPort">Port TCP pojazdu.</param>
    /// <param name="Lat">Szerokość geograficzna markeru.</param>
    /// <param name="Lng">Długość geograficzna markeru.</param>
    public record VehicleMapDto(
        int Id,
        string Name,
        string? IpAdress,
        int? TcpPort,
        double Lat,
        double Lng
    );

    /// <summary>
    /// Żądanie dodania nowego pojazdu do bazy.
    /// </summary>
    /// <param name="VehicleName">Nazwa pojazdu.</param>
    /// <param name="IpAdress">Adres IP pojazdu.</param>
    /// <param name="TcpPort">Port TCP pojazdu.</param>
    public record AddVehicleRequest(
        string VehicleName,
        string IpAdress,
        int TcpPort
    );
}