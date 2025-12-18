using WebApplication1.Models;
using WebApplication1.DAL; // Pamiętaj o tym usingu

namespace WebApplication1.Services
{
    public class FieldsListService
    {
        private readonly FieldDAL _fieldDal;

        // Wstrzykujemy FieldDAL, który ma metodę GetFieldListAsync
        public FieldsListService(FieldDAL fieldDal)
        {
            _fieldDal = fieldDal;
        }

        public async Task<List<FieldListItemDto>> GetFieldsListForMenuAsync(string username)
        {
            // Delegujemy zadanie do warstwy dostępu do danych
            return await _fieldDal.GetFieldListAsync(username);
        }
    }
}