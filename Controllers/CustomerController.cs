using ABCRetailApp.Helpers;
using ABCRetailApp.Models;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ABCRetailApp.Controllers
{
    public class CustomerController : Controller
    {
        private readonly TableClient _customerTable;
        private readonly TableClient _productTable;
        private readonly TableClient _cartTable;
        private readonly TableClient _orderTable;
        private readonly QueueClient _orderQueue;

        public CustomerController(IConfiguration configuration)
        {
            string conn = configuration["AzureStorage:ConnectionString"];
            _customerTable = new TableClient(conn, "Customers"); _customerTable.CreateIfNotExists();
            _productTable = new TableClient(conn, "Products"); _productTable.CreateIfNotExists();
            _cartTable = new TableClient(conn, "CartItems"); _cartTable.CreateIfNotExists();
            _orderTable = new TableClient(conn, "Orders"); _orderTable.CreateIfNotExists();

            string queueName = configuration["AzureStorage:OrderQueue"] ?? "orders";

            _orderQueue = new QueueClient(conn, queueName);
            _orderQueue.CreateIfNotExists();
        }

        #region Login/Register/Dashboard/Logout

        [HttpGet] public IActionResult Login() => View();
        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            { ViewBag.Error = "Email and password required."; return View(); }

            var user = _customerTable.Query<AppUser>(u => u.PartitionKey == "Customer" && u.Email == email).FirstOrDefault();
            if (user == null || user.PasswordHash != HashPassword(password))
            { ViewBag.Error = "Invalid email or password."; return View(); }

            HttpContext.Session.SetString("CustomerEmail", user.Email);
            HttpContext.Session.SetString("CustomerName", user.FullName);

            return RedirectToAction("Dashboard");
        }

        [HttpGet] public IActionResult Register() => View();
        [HttpPost]
        public IActionResult Register(string fullName, string email, string password)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            { ViewBag.Error = "All fields are required."; return View(); }

            var existing = _customerTable.Query<AppUser>(u => u.PartitionKey == "Customer" && u.Email == email).FirstOrDefault();
            if (existing != null) { ViewBag.Error = "Email already registered."; return View(); }

            var user = new AppUser
            {
                PartitionKey = "Customer",
                RowKey = Guid.NewGuid().ToString(),
                FullName = fullName,
                Email = email,
                PasswordHash = HashPassword(password)
            };

            _customerTable.AddEntity(user);

            HttpContext.Session.SetString("CustomerEmail", user.Email);
            HttpContext.Session.SetString("CustomerName", user.FullName);

            return RedirectToAction("Dashboard");
        }

        public IActionResult Dashboard()
        {
            string name = HttpContext.Session.GetString("CustomerName");
            if (string.IsNullOrEmpty(name)) return RedirectToAction("Login");

            ViewBag.CustomerName = name;
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        #endregion

        #region View Products

        [HttpGet]
        public IActionResult ViewProducts(string category)
        {
            var products = _productTable.Query<Product>().ToList();
            if (!string.IsNullOrEmpty(category) && category != "All")
                products = products.Where(p => p.Category == category).ToList();

            var cart = GetCartFromSession();
            ViewBag.Cart = cart;
            ViewBag.SelectedCategory = category ?? "All";

            return View(products);
        }

        #endregion

        #region Cart Actions

        [HttpPost]
        public IActionResult AddToCart(string rowKey)
        {
            var product = _productTable.GetEntity<Product>("Products", rowKey); 
            if (product == null) return RedirectToAction("ViewProducts");

            var cart = GetCartFromSession();
            var existing = cart.FirstOrDefault(c => c.RowKey == rowKey);

            if (existing != null)
                existing.Quantity++;
            else
                cart.Add(new CartItem
                {
                    RowKey = product.Value.RowKey,
                    ProductName = product.Value.ProductName,
                    Price = (decimal)product.Value.Price,
                    Quantity = 1
                });

            SaveCartToSession(cart);
            return RedirectToAction("ViewProducts");
        }



        [HttpPost]
        public IActionResult RemoveFromCart(string rowKey)
        {
            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(c => c.RowKey == rowKey);
            if (item != null) cart.Remove(item);
            SaveCartToSession(cart);
            return RedirectToAction("ViewCart");
        }

        public IActionResult ViewCart()
        {
            var cart = GetCartFromSession();
            ViewBag.Total = cart.Sum(c => c.Subtotal);
            return View(cart);
        }

        [HttpPost]
        public IActionResult ClearCart()
        {
            SaveCartToSession(new List<CartItem>());
            return RedirectToAction("ViewCart");
        }

        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCartFromSession();
            if (!cart.Any()) return RedirectToAction("ViewCart");

            string email = HttpContext.Session.GetString("CustomerEmail") ?? "guest";

            var order = new Order
            {
                PartitionKey = "Orders",
                RowKey = Guid.NewGuid().ToString(),
                CustomerEmail = email,
                OrderDate = DateTime.UtcNow,
                Status = "Success",
                TotalAmount = cart.Sum(c => c.Subtotal),
                ItemsJson = JsonSerializer.Serialize(cart)
            };

            await _orderTable.AddEntityAsync(order);

            SaveCartToSession(new List<CartItem>()); 
            return RedirectToAction("OrderSuccess", new { orderId = order.RowKey });
        }

        public IActionResult OrderSuccess(string orderId)
        {
            var order = _orderTable.Query<Order>(o => o.RowKey == orderId).FirstOrDefault();
            if (order == null) return NotFound();

            
            var items = JsonSerializer.Deserialize<List<CartItem>>(order.ItemsJson);

         
            ViewBag.CartItems = items;
            ViewBag.Total = items.Sum(i => i.Price * i.Quantity);

            return View(items); 
        }

        #endregion
        public IActionResult MyOrders()
        {
            var customerEmail = HttpContext.Session.GetString("CustomerEmail");
            if (string.IsNullOrEmpty(customerEmail)) return RedirectToAction("Login");

          
            var orders = _orderTable.Query<Order>(o => o.CustomerEmail == customerEmail)
                                    .OrderByDescending(o => o.OrderDate)
                                    .ToList();

            return View(orders);
        }

        public IActionResult OrderDetails(string orderId)
        {
            var order = _orderTable.Query<Order>(o => o.RowKey == orderId).FirstOrDefault();
            if (order == null) return NotFound();

            var items = JsonSerializer.Deserialize<List<CartItem>>(order.ItemsJson);
            ViewBag.Total = items.Sum(i => i.Price * i.Quantity);

            return View(items);
        }






        #region Helpers

        private List<CartItem> GetCartFromSession()
        {
            return HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
        }

        private void SaveCartToSession(List<CartItem> cart)
        {
            HttpContext.Session.SetObjectAsJson("Cart", cart);
        }

        #endregion
    }
}
