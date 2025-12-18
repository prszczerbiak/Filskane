using WebApplication1.DAL;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class SettingsService
    {
        private readonly SettingsDAL _settingsDal;

        public SettingsService(SettingsDAL settingsDal)
        {
            _settingsDal = settingsDal;
        }

        public async Task<UserShortDto?> GetShortInfoAsync(string username)
        {
            return await _settingsDal.GetShortInfoAsync(username);
        }

        public async Task<UserDetailDto?> GetLongInfoAsync(string username)
        {
            return await _settingsDal.GetLongInfoAsync(username);
        }

        public async Task UpdateSurfaceAsync(string username, int surface)
        {
            await _settingsDal.UpdateSurfaceAsync(username, surface);
        }

        public async Task UpdateThemeAsync(string username, int theme)
        {
            await _settingsDal.UpdateThemeAsync(username, theme);
        }

        public async Task UpdateNameAsync(string username, string name)
        {
            await _settingsDal.UpdateFirstNameAsync(username, name);
        }

        public async Task UpdateEmailAsync(string username, string email)
        {
            // Tutaj możesz dodać logikę biznesową, np. sprawdzenie 
            // czy email nie jest zajęty, używając metody z AuthDAL:
            // if (await _authDal.CheckIfEmailExistsAsync(email)) throw ...

            await _settingsDal.UpdateEmailAsync(username, email);
        }

        public async Task UpdatePhoneAsync(string username, string phone)
        {
            await _settingsDal.UpdatePhoneAsync(username, phone);
        }
    }
}