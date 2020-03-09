using System.Collections.Generic;

namespace BulkyBook.Models.ViewModels
{
    public class ShoppingCardVM
    {
        public IEnumerable<ShoppingCard> ListCard { get; set; }

        public OrderHeader OrderHeader { get; set; }
    }
}
