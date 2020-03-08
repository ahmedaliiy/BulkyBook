using System;

namespace BulkyBook.DataAccess.Repository.IRepository
{
    public interface IUnitOfWork : IDisposable
    {
        ICategoryRepository Category { get; }
        
        ICompanyRepository Company { get; }

        ICoverTypeRepository CoverType { get; }
        
        IApplicationUserRepository ApplicationUser { get; }
        
        IProductRepository Product { get; }

        IShoppingCardRepository ShoppingCard { get; }

        IOrderHeaderRepository OrderHeader { get; }

        IOrderDetailsRepository OrderDetails { get; }

        ISP_Call SP_Call { get; }

        void Save();
    }
}
