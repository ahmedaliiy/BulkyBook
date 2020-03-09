using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;

namespace BulkyBook.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            IEnumerable<Product> productList = _unitOfWork.Product.GetAll(includeProperties: "Category,CoverType");
            return View(productList);
        }

        public IActionResult Details(int id)
        {
            var productFromDB = _unitOfWork.Product.GetFirstOrDefault(u => u.Id == id, includeProperties: "Category,CoverType");
            ShoppingCard cardObj = new ShoppingCard()
            {
                Product = productFromDB,
                ProductId = productFromDB.Id
            };
            return View(cardObj);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult Details(ShoppingCard cardObject)
        {
            cardObject.Id = 0;
            if (ModelState.IsValid)
            {
                //then we will add to card
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                cardObject.ApplicationUserId = claim.Value;

                ShoppingCard cardFromDb = _unitOfWork.ShoppingCard.GetFirstOrDefault(
                    u => u.ApplicationUserId == cardObject.ApplicationUserId && u.ProductId == cardObject.ProductId,
                    includeProperties: "Product"
                    );

                if(cardFromDb == null)
                {
                    //no records exists in db for that product for that user
                    _unitOfWork.ShoppingCard.Add(cardObject);
                }
                else
                {
                    cardFromDb.Count += cardObject.Count;
                    //_unitOfWork.ShoppingCard.Update(cardFromDb);
                }
                _unitOfWork.Save();

                var count = _unitOfWork.ShoppingCard
                    .GetAll(c => c.ApplicationUserId == cardObject.ApplicationUserId)
                    .ToList().Count();

                HttpContext.Session.SetInt32(SD.ssShoppingCard, count);

                return RedirectToAction(nameof(Index));
            }
            else
            {
                var productFromDB = _unitOfWork.Product.GetFirstOrDefault(u => u.Id == cardObject.Id, includeProperties: "Category,CoverType");
                ShoppingCard cardObj = new ShoppingCard()
                {
                    Product = productFromDB,
                    ProductId = productFromDB.Id
                };
                return View(cardObj);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
