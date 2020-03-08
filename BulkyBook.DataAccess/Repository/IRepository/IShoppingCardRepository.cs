using BulkyBook.Models;

namespace BulkyBook.DataAccess.Repository.IRepository
{
    public interface IShoppingCardRepository : IRepository<ShoppingCard>
    {
        void Update(ShoppingCard obj);
    }
}
