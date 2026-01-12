using WebApplication1.Models;
using WebApplication1.DAL;

namespace WebApplication1.Services;

/// <summary>
/// Serwis biznesowy pośredniczący w operacjach na polach oraz powiązanych z nimi skanach.
/// Agreguje metody z FieldDAL i ScanDAL.
/// </summary>
public class FieldService
{
    private readonly FieldDAL _fieldDal;
    private readonly ScanDAL _scanDal;

    public FieldService(FieldDAL fieldDal, ScanDAL scanDal)
    {
        _fieldDal = fieldDal;
        _scanDal = scanDal;
    }

    /// <summary>
    /// Pobiera szczegółowe informacje o polu na podstawie jego identyfikatora.
    /// </summary>
    /// <param name="username">Nazwa użytkownika (właściciela pola).</param>
    /// <param name="fieldId">Identyfikator pola.</param>
    /// <returns>Obiekt ze szczegółami pola lub null, jeśli pole nie istnieje lub nie należy do użytkownika.</returns>
    public async Task<FieldDetailDto?> GetFieldDetailsAsync(string username, int fieldId)
    {
        return await _fieldDal.GetUserFieldByIdAsync(username, fieldId);
    }

    /// <summary>
    /// Aktualizuje konfigurację pola (przypisaną uprawę, datę zasiewu).
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">Identyfikator pola.</param>
    /// <param name="dto">Obiekt z nowymi danymi.</param>
    public async Task UpdateFieldAsync(string username, int fieldId, UpdateFieldRequest dto)
    {
        await _fieldDal.SaveFieldChangesAsync(username, fieldId, dto);
    }

    /// <summary>
    /// Pobiera informacje o aktualnym cyklu wzrostu rośliny na danym polu.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">Identyfikator pola.</param>
    /// <returns>Obiekt z informacjami o cyklu lub null.</returns>
    public async Task<CycleDto?> GetCycleAsync(string username, int fieldId)
    {
        return await _fieldDal.GetCycleByIdAsync(username, fieldId);
    }

    /// <summary>
    /// Pobiera pełną listę dostępnych roślin uprawnych (słownik).
    /// </summary>
    /// <returns>Lista dostępnych roślin.</returns>
    public async Task<List<PlantDto>> GetAllPlantsAsync()
    {
        return await _fieldDal.GetPlantsAsync();
    }

    /// <summary>
    /// Pobiera historię skanów satelitarnych dla danego pola.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">Identyfikator pola.</param>
    /// <returns>Lista podsumowań skanów.</returns>
    public async Task<List<ScanSummaryDto>> GetScansHistoryAsync(string username, int fieldId)
    {
        return await _scanDal.GetFieldScansAsync(username, fieldId);
    }

    /// <summary>
    /// Usuwa wybrany skan satelitarny.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="scanId">Identyfikator skanu.</param>
    /// <returns>True, jeśli operacja zakończyła się sukcesem.</returns>
    public async Task<bool> DeleteScanAsync(string username, int scanId)
    {
        return await _scanDal.DeleteScanAsync(username, scanId);
    }
}