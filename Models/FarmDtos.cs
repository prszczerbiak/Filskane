namespace WebApplication1.Models
{
    // Requesty (Dane wejściowe)
    public record SaveFieldRequest(
        string Name,
        string Geojson,
        double CenterX,
        double CenterY,
        double Area
    );

    public record FarmCoordsRequest(double? FarmX, double? FarmY);

    // Response (Dane wyjściowe)
    public record FieldShortDto(
        int Id,
        string Name,
        double CenterX,
        double CenterY,
        string Geojson // complex + type + substrate
    );

    public record FieldListItemDto(int Id, string Name);
}
