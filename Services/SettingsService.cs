using WebApplication1.DAL;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Serwis biznesowy pośredniczący w zarządzaniu ustawieniami profilu użytkownika.
    /// </summary>
    public class SettingsService
    {
        private readonly SettingsDAL _settingsDal;

        public SettingsService(SettingsDAL settingsDal)
        {
            _settingsDal = settingsDal;
        }

        /// <summary>
        /// Pobiera podstawowe informacje o użytkowniku (np. do paska nawigacji).
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <returns>Obiekt DTO z podstawowymi danymi lub null.</returns>
        public async Task<UserShortDto?> GetShortInfoAsync(string username)
        {
            return await _settingsDal.GetShortInfoAsync(username);
        }

        /// <summary>
        /// Pobiera pełne dane profilowe użytkownika.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <returns>Szczegółowy obiekt DTO.</returns>
        public async Task<UserDetailDto?> GetLongInfoAsync(string username)
        {
            return await _settingsDal.GetLongInfoAsync(username);
        }

        /// <summary>
        /// Aktualizuje preferencję jednostki powierzchni.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="surface">Nowa wartość jednostki.</param>
        public async Task UpdateSurfaceAsync(string username, int surface)
        {
            await _settingsDal.UpdateSurfaceAsync(username, surface);
        }

        /// <summary>
        /// Aktualizuje preferencję motywu graficznego.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="theme">Nowa wartość motywu.</param>
        public async Task UpdateThemeAsync(string username, int theme)
        {
            await _settingsDal.UpdateThemeAsync(username, theme);
        }

        /// <summary>
        /// Aktualizuje imię użytkownika.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="name">Nowe imię.</param>
        public async Task UpdateNameAsync(string username, string name)
        {
            await _settingsDal.UpdateFirstNameAsync(username, name);
        }

        /// <summary>
        /// Aktualizuje adres e-mail użytkownika.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="email">Nowy adres e-mail.</param>
        public async Task UpdateEmailAsync(string username, string email)
        {
            await _settingsDal.UpdateEmailAsync(username, email);
        }

        /// <summary>
        /// Aktualizuje numer telefonu użytkownika.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="phone">Nowy numer telefonu.</param>
        public async Task UpdatePhoneAsync(string username, string phone)
        {
            await _settingsDal.UpdatePhoneAsync(username, phone);
        }
    }
}