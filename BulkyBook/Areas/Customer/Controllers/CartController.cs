using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace BulkyBook.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<IdentityUser> _userManager;

        [BindProperty]
        public ShoppingCardVM shoppingCardVM { get; set; }
        public CartController(IUnitOfWork unitOfWork, IEmailSender emailSender, UserManager<IdentityUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _userManager = userManager;
        }
        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            shoppingCardVM = new ShoppingCardVM()
            {
                OrderHeader = new Models.OrderHeader(),
                ListCard = _unitOfWork.ShoppingCard.GetAll(u => u.ApplicationUserId == claim.Value, includeProperties: "Product")
            };

            shoppingCardVM.OrderHeader.OrderTotal = 0;
            shoppingCardVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser
                .GetFirstOrDefault(u => u.Id == claim.Value, includeProperties: "Company");

            foreach (var list in shoppingCardVM.ListCard)
            {
                list.Price = SD.GetPriceBasedOnQuantity(list.Count, list.Product.Price, list.Product.Price50, list.Product.Price100);
                shoppingCardVM.OrderHeader.OrderTotal += (list.Price * list.Count);
                list.Product.Description = SD.ConvertToRawHtml(list.Product.Description);
                if (list.Product.Description.Length > 100)
                {
                    list.Product.Description = list.Product.Description.Substring(0, 99) + "...";
                }
            }
            return View(shoppingCardVM);
        }

        [HttpPost]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPost()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            var user = _unitOfWork.ApplicationUser.GetFirstOrDefault(u => u.Id == claim.Value);

            if (user == null)
                ModelState.AddModelError(string.Empty, "Verification email is empty!");

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code = code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(user.Email, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            ModelState.AddModelError(string.Empty, "Verification email sent.please check your email");
            return RedirectToAction("Index");
        }

        public IActionResult Plus(int cartId)
        {
            var cart = _unitOfWork.ShoppingCard.GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");
            cart.Count += 1;
            cart.Price = SD.GetPriceBasedOnQuantity(cart.Count, cart.Product.Price, cart.Product.Price50, cart.Product.Price100);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cart = _unitOfWork.ShoppingCard.GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");

            if (cart.Count == 1)
            {
                var cnt = _unitOfWork.ShoppingCard.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count();
                _unitOfWork.ShoppingCard.Remove(cart);
                _unitOfWork.Save();
                HttpContext.Session.SetInt32(SD.ssShoppingCard, cnt - 1);
            }
            else
            {
                cart.Count -= 1;
                cart.Price = SD.GetPriceBasedOnQuantity(cart.Count, cart.Product.Price, cart.Product.Price50, cart.Product.Price100);
                _unitOfWork.Save();
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cart = _unitOfWork.ShoppingCard.GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");

            var cnt = _unitOfWork.ShoppingCard.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count();
            _unitOfWork.ShoppingCard.Remove(cart);
            _unitOfWork.Save();
            HttpContext.Session.SetInt32(SD.ssShoppingCard, cnt - 1);

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            shoppingCardVM = new ShoppingCardVM()
            {
                OrderHeader = new Models.OrderHeader(),
                ListCard = _unitOfWork.ShoppingCard.GetAll(c => c.ApplicationUserId == claim.Value, includeProperties: "Product")
            };

            shoppingCardVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.GetFirstOrDefault(c => c.Id == claim.Value, includeProperties: "Company");

            foreach (var list in shoppingCardVM.ListCard)
            {
                list.Price = SD.GetPriceBasedOnQuantity(list.Count, list.Product.Price, list.Product.Price50, list.Product.Price100);
                shoppingCardVM.OrderHeader.OrderTotal += (list.Price * list.Count);
            }

            shoppingCardVM.OrderHeader.Name = shoppingCardVM.OrderHeader.ApplicationUser.Name;
            shoppingCardVM.OrderHeader.PhoneNumber = shoppingCardVM.OrderHeader.ApplicationUser.PhoneNumber;
            shoppingCardVM.OrderHeader.StreetAddress = shoppingCardVM.OrderHeader.ApplicationUser.StreetAddress;
            shoppingCardVM.OrderHeader.City = shoppingCardVM.OrderHeader.ApplicationUser.City;
            shoppingCardVM.OrderHeader.State = shoppingCardVM.OrderHeader.ApplicationUser.State;
            shoppingCardVM.OrderHeader.PostalCode = shoppingCardVM.OrderHeader.ApplicationUser.PostalCode;

            return View(shoppingCardVM);
        }

        [HttpPost]
        [ActionName("Summary")]
        [ValidateAntiForgeryToken]
        public IActionResult SummaryPost(string stripeToken)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            shoppingCardVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.GetFirstOrDefault(c => c.Id == claim.Value, includeProperties: "Company");
            shoppingCardVM.ListCard = _unitOfWork.ShoppingCard.GetAll(c => c.ApplicationUserId == claim.Value, includeProperties: "Product");
            shoppingCardVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            shoppingCardVM.OrderHeader.OrderStatus = SD.StatusPending;
            shoppingCardVM.OrderHeader.ApplicationUserId = claim.Value;
            shoppingCardVM.OrderHeader.OrderDate = DateTime.Now;

            _unitOfWork.OrderHeader.Add(shoppingCardVM.OrderHeader);
            _unitOfWork.Save();

            List<OrderDetails> orderDetailsList = new List<OrderDetails>();
            foreach (var item in shoppingCardVM.ListCard)
            {
                item.Price = SD.GetPriceBasedOnQuantity(item.Count, item.Product.Price, item.Product.Price50, item.Product.Price100);
                OrderDetails orderDetails = new OrderDetails()
                {
                    ProductId = item.ProductId,
                    OrderId = shoppingCardVM.OrderHeader.Id,
                    Price = item.Price,
                    Count = item.Count
                };
                shoppingCardVM.OrderHeader.OrderTotal += orderDetails.Count * orderDetails.Price;
                _unitOfWork.OrderDetails.Add(orderDetails);
                _unitOfWork.Save();
            }
            _unitOfWork.ShoppingCard.RemoveRange(shoppingCardVM.ListCard);
            _unitOfWork.Save();
            HttpContext.Session.SetInt32(SD.ssShoppingCard, 0);

            if (stripeToken == null)
            {
                //order will be created for delayed payment for authorized company
                shoppingCardVM.OrderHeader.PaymentDueDate = DateTime.Now.AddDays(10);
                shoppingCardVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                shoppingCardVM.OrderHeader.OrderStatus = SD.StatusApproved;
            }
            else
            {
                //process the payment
                var options = new ChargeCreateOptions
                {
                    Amount = Convert.ToInt32(shoppingCardVM.OrderHeader.OrderTotal * 100),
                    Currency = "usd",
                    Description = "Order ID : " + shoppingCardVM.OrderHeader.Id,
                    Source = stripeToken
                };

                var service = new ChargeService();
                Charge charge = service.Create(options);

                if(charge.BalanceTransactionId == null)
                {
                    shoppingCardVM.OrderHeader.PaymentStatus = SD.PaymentStatusRejected;
                }
                else
                {
                    shoppingCardVM.OrderHeader.TransactionId = charge.BalanceTransactionId;
                }
                if (charge.Status.ToLower() == "succeeded")
                {
                    shoppingCardVM.OrderHeader.PaymentStatus = SD.PaymentStatusApproved;
                    shoppingCardVM.OrderHeader.OrderStatus = SD.StatusApproved;
                    shoppingCardVM.OrderHeader.PayementDate = DateTime.Now;
                }

            }
            _unitOfWork.Save();
            return RedirectToAction("OrderConfirmation", "Cart", new { id = shoppingCardVM.OrderHeader.Id });
        }

        public IActionResult OrderConfirmation(int id)
        {
            return View(id);
        }
    }
}