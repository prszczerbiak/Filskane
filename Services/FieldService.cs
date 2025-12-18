using WebApplication1.Models;
using WebApplication1.DAL;

namespace WebApplication1.Services;

public class FieldService
{
    private readonly FieldDAL _fieldDal;
    private readonly ScanDAL _scanDal; // Zostawiamy ScanDAL osobno (GeoRaster/Blob)

    // Teraz wstrzykujemy tylko dwa DALe
    public FieldService(FieldDAL fieldDal, ScanDAL scanDal)
    {
        _fieldDal = fieldDal;
        _scanDal = scanDal;
    }

    // --- CRUD PÓL ---

    public async Task<FieldDetailDto?> GetFieldDetailsAsync(string username, int fieldId)
    {
        return await _fieldDal.GetUserFieldByIdAsync(username, fieldId);
    }

    public async Task UpdateFieldAsync(int fieldId, UpdateFieldRequest dto)
    {
        await _fieldDal.SaveFieldChangesAsync(fieldId, dto);
    }

    // --- METODY SŁOWNIKOWE (TERAZ Z FIELD_DAL) ---

    public async Task<CycleDto?> GetCycleAsync(int fieldId)
    {
        return await _fieldDal.GetCycleByIdAsync(fieldId);
    }

    public async Task<List<PlantDto>> GetAllPlantsAsync()
    {
        return await _fieldDal.GetPlantsAsync();
    }

    // --- SKANY (Z SCAN_DAL) ---

    public async Task<List<ScanSummaryDto>> GetScansHistoryAsync(int fieldId)
    {
        return await _scanDal.GetFieldScansAsync(fieldId);
    }

    public async Task<bool> DeleteScanAsync(int scanId)
    {
        return await _scanDal.DeleteScanAsync(scanId);
    }
}