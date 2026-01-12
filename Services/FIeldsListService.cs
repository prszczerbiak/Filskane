using WebApplication1.Models;
using WebApplication1.DAL;

namespace WebApplication1.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za dostarczanie list pól uprawnych w formacie skróconym,
    /// optymalizowany pod kątem budowania menu nawigacyjnego i list wyboru.
    /// </summary>
    public class FieldsListService
    {
        private readonly FieldDAL _fieldDal;

        public FieldsListService(FieldDAL fieldDal)
        {
            _fieldDal = fieldDal;
        }

        /// <summary>
        /// Pobiera uproszczoną listę pól (ID + Nazwa) należących do wskazanego użytkownika.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <returns>Lista obiektów DTO zawierających podstawowe dane pól.</returns>
        public async Task<List<FieldListItemDto>> GetFieldsListForMenuAsync(string username)
        {
            return await _fieldDal.GetFieldListAsync(username);
        }
    }
}