using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using ABCRetailApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;

namespace ABCRetailApp.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly string _connectionString;
        private readonly string _employeeTable;
        private readonly string _logsShare = "logs";
        private readonly TableClient _orderTable;
        private readonly TableClient _productTable;
        private readonly QueueClient _orderQueue;


        public EmployeeController(IConfiguration configuration)
        {
            _connectionString = configuration["AzureStorage:ConnectionString"];
            _employeeTable = configuration["AzureStorage:EmployeeTable"];
        

        }

        #region Login
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var tableClient = new TableClient(_connectionString, _employeeTable);
            var employee = tableClient.Query<Employee>(e => e.Email == email).FirstOrDefault();

            if (employee != null && VerifyPassword(password, employee.PasswordHash))
            {
                await LogEventAsync($"Employee logged in: {email}");
                return RedirectToAction("Dashboard");
            }

            ViewBag.Error = "Invalid email or password";
            return View();
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            using var sha256 = SHA256.Create();
            var hashed = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return hashed == storedHash;
        }
        #endregion

        #region Dashboard
        public IActionResult Dashboard() => View();
        #endregion

        #region Product Upload
        [HttpPost]
        public async Task<IActionResult> UploadProduct(IFormFile productImage, string productName, string category, double price, int quantity, string description, bool outOfStock)
        {
            if (productImage == null || productImage.Length == 0)
            {
                TempData["Error"] = "Please select an image file.";
                return RedirectToAction("Dashboard");
            }

            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("productimages");
            await containerClient.CreateIfNotExistsAsync();

            string blobName = Guid.NewGuid() + Path.GetExtension(productImage.FileName);
            var blobClient = containerClient.GetBlobClient(blobName);

            using (var stream = productImage.OpenReadStream())
                await blobClient.UploadAsync(stream);

            var tableClient = new TableClient(_connectionString, "Products");
            tableClient.CreateIfNotExists();

            var product = new Product
            {
                PartitionKey = "Products",
                RowKey = Guid.NewGuid().ToString(),
                ProductName = productName,
                Category = category,
                Description = description,
                Price = price,
                Quantity = quantity,
                OutOfStock = outOfStock,
                ImageUrl = blobClient.Uri.ToString()
            };

            tableClient.AddEntity(product);

            await LogEventAsync($"Product uploaded: {productName}");
            TempData["Success"] = "Product uploaded successfully!";
            return RedirectToAction("ViewProducts");
        }
        #endregion

        #region ManageProfiles (Employees & Customers)
        public IActionResult ManageProfiles()
        {
            var employeeTableClient = new TableClient(_connectionString, _employeeTable);
            var customerTableClient = new TableClient(_connectionString, "Customers");

            employeeTableClient.CreateIfNotExists();
            customerTableClient.CreateIfNotExists();

            var employees = employeeTableClient.Query<Employee>().ToList();
            var customers = customerTableClient.Query<Customer>().ToList();

            var model = new ManageProfilesViewModel
            {
                Employees = employees,
                Customers = customers
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddEmployeeProfile(string fullName, string email, string password)
        {
            var tableClient = new TableClient(_connectionString, _employeeTable);
            tableClient.CreateIfNotExists();

            if (tableClient.Query<Employee>(e => e.Email == email).Any())
            {
                TempData["Error"] = "Employee already exists.";
                return RedirectToAction("ManageProfiles");
            }

            using var sha256 = SHA256.Create();
            var hashedPassword = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));

            var employee = new Employee
            {
                PartitionKey = "Employees",
                RowKey = Guid.NewGuid().ToString(),
                FullName = fullName,
                Email = email,
                PasswordHash = hashedPassword,
                Role = "Employee"
            };

            tableClient.AddEntity(employee);
            await LogEventAsync($"Employee added: {fullName} ({email})");
            TempData["Success"] = "Employee added successfully!";
            return RedirectToAction("ManageProfiles");
        }

        [HttpPost]
        public async Task<IActionResult> EditEmployeeProfile(string rowKey, string fullName, string email)
        {
            var tableClient = new TableClient(_connectionString, _employeeTable);
            var employee = tableClient.GetEntity<Employee>("Employees", rowKey).Value;

            employee.FullName = fullName;
            employee.Email = email;

            tableClient.UpdateEntity(employee, employee.ETag, TableUpdateMode.Replace);
            await LogEventAsync($"Employee updated: {fullName} ({email})");
            TempData["Success"] = "Employee updated successfully!";
            return RedirectToAction("ManageProfiles");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteEmployeeProfile(string rowKey)
        {
            var tableClient = new TableClient(_connectionString, _employeeTable);
            tableClient.DeleteEntity("Employees", rowKey);
            await LogEventAsync($"Employee deleted: {rowKey}");
            TempData["Success"] = "Employee deleted successfully!";
            return RedirectToAction("ManageProfiles");
        }

        [HttpPost]
        public async Task<IActionResult> AddCustomerProfile(string fullName, string email, string password)
        {
            var tableClient = new TableClient(_connectionString, "Customers");
            tableClient.CreateIfNotExists();

            if (tableClient.Query<Customer>(c => c.Email == email).Any())
            {
                TempData["Error"] = "Customer already exists.";
                return RedirectToAction("ManageProfiles");
            }

            using var sha256 = SHA256.Create();
            var hashedPassword = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));

            var customer = new Customer
            {
                PartitionKey = "Customers",
                RowKey = Guid.NewGuid().ToString(),
                FullName = fullName,
                Email = email,
                PasswordHash = hashedPassword
            };

            tableClient.AddEntity(customer);
            await LogEventAsync($"Customer added: {fullName} ({email})");
            TempData["Success"] = "Customer added successfully!";
            return RedirectToAction("ManageProfiles");
        }

        [HttpPost]
        public async Task<IActionResult> EditCustomerProfile(string rowKey, string fullName, string email)
        {
            var tableClient = new TableClient(_connectionString, "Customers");
            var customer = tableClient.GetEntity<Customer>("Customers", rowKey).Value;

            customer.FullName = fullName;
            customer.Email = email;

            tableClient.UpdateEntity(customer, customer.ETag, TableUpdateMode.Replace);
            await LogEventAsync($"Customer updated: {fullName} ({email})");
            TempData["Success"] = "Customer updated successfully!";
            return RedirectToAction("ManageProfiles");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCustomerProfile(string rowKey)
        {
            var tableClient = new TableClient(_connectionString, "Customers");
            tableClient.DeleteEntity("Customers", rowKey);
            await LogEventAsync($"Customer deleted: {rowKey}");
            TempData["Success"] = "Customer deleted successfully!";
            return RedirectToAction("ManageProfiles");
        }
        #endregion

        #region Manage Orders & Inventory
        public IActionResult ManageOrders()
        {
          
            var queueClient = new QueueClient(_connectionString, "ordersqueue");
            queueClient.CreateIfNotExists();
            var messages = queueClient.ReceiveMessages(maxMessages: 32).Value.ToList();

            List<Order> pendingOrders = new List<Order>();
            foreach (var msg in messages)
            {
                var order = System.Text.Json.JsonSerializer.Deserialize<Order>(
                    System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(msg.MessageText))
                );
                order.RowKey = msg.MessageId;      
                order.Status = "Pending";          
                pendingOrders.Add(order);
            }

            ViewBag.PendingOrders = pendingOrders;

           
            var productTable = new TableClient(_connectionString, "Products");
            productTable.CreateIfNotExists();
            var products = productTable.Query<Product>().ToList();
            ViewBag.Products = products;

            return View();
        }
        #endregion


        #region Logs (Azure Files)
        public IActionResult ViewLogs()
        {
            var shareClient = new ShareClient(_connectionString, _logsShare);
            shareClient.CreateIfNotExists();

            var rootDir = shareClient.GetRootDirectoryClient();
            List<string> fileNames = new List<string>();

            foreach (ShareFileItem item in rootDir.GetFilesAndDirectories())
                if (!item.IsDirectory) fileNames.Add(item.Name);

            return View(fileNames);
        }

        public IActionResult DownloadLog(string fileName)
        {
            var shareClient = new ShareClient(_connectionString, _logsShare);
            var rootDir = shareClient.GetRootDirectoryClient();
            var fileClient = rootDir.GetFileClient(fileName);

            if (!fileClient.Exists())
                return NotFound("Log file not found.");

            var downloadResponse = fileClient.Download();
            var ms = new MemoryStream();
            downloadResponse.Value.Content.CopyTo(ms);
            ms.Position = 0;

            return File(ms, "application/octet-stream", fileName);
        }
        #endregion

        #region Profile File Management
        [HttpPost]
        public async Task<IActionResult> UploadProfile(IFormFile file, string profileId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file selected.");

            try
            {
                var shareName = "profileshare";
                var directory = new ShareClient(_connectionString, shareName).GetDirectoryClient("profiles");
                await directory.CreateIfNotExistsAsync();

                var fileClient = directory.GetFileClient($"{profileId}_{file.FileName}");

                using (var stream = file.OpenReadStream())
                {
                    await fileClient.CreateAsync(stream.Length);
                    await fileClient.UploadAsync(stream);

                    var headers = new ShareFileHttpHeaders { ContentType = file.ContentType ?? "application/octet-stream" };
                    var options = new ShareFileSetHttpHeadersOptions { HttpHeaders = headers };
                    await fileClient.SetHttpHeadersAsync(options);
                }

                await LogEventAsync($"Profile file uploaded: {profileId}");
                return Ok("Profile file uploaded successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error uploading file: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadProfile(string fileName)
        {
            try
            {
                var shareName = "profileshare";
                var directory = new ShareClient(_connectionString, shareName).GetDirectoryClient("profiles");
                var fileClient = directory.GetFileClient(fileName);

                if (!fileClient.Exists())
                    return NotFound("Profile file not found.");

                var download = await fileClient.DownloadAsync();
                var ms = new MemoryStream();
                await download.Value.Content.CopyToAsync(ms);
                ms.Position = 0;

                return File(ms, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error downloading file: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProfile(string fileName)
        {
            try
            {
                var shareName = "profileshare";
                var directory = new ShareClient(_connectionString, shareName).GetDirectoryClient("profiles");
                var fileClient = directory.GetFileClient(fileName);

                if (!fileClient.Exists())
                    return NotFound("Profile file not found.");

                await fileClient.DeleteAsync();
                await LogEventAsync($"Profile file deleted: {fileName}");

                return Ok("Profile file deleted successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting file: {ex.Message}");
            }
        }
        #endregion

        #region Logging Helper
        private async Task LogEventAsync(string message)
        {
            var shareClient = new ShareClient(_connectionString, _logsShare);
            await shareClient.CreateIfNotExistsAsync();

            var rootDir = shareClient.GetRootDirectoryClient();
            string fileName = $"log_{DateTime.UtcNow:yyyyMMdd}.txt";
            var fileClient = rootDir.GetFileClient(fileName);

            byte[] content = Encoding.UTF8.GetBytes($"{DateTime.UtcNow:u} - {message}\n");

            if (!fileClient.Exists())
            {
                fileClient.Create(content.Length);
                using var ms = new MemoryStream(content);
                await fileClient.UploadRangeAsync(new Azure.HttpRange(0, content.Length), ms);
            }
            else
            {
                var download = fileClient.Download();
                using var existingStream = new MemoryStream();
                download.Value.Content.CopyTo(existingStream);
                existingStream.Write(content, 0, content.Length);
                fileClient.Create(existingStream.Length);
                existingStream.Position = 0;
                await fileClient.UploadRangeAsync(new Azure.HttpRange(0, existingStream.Length), existingStream);
            }
        }
        #endregion

        #region Product Management
        [HttpGet]
        public IActionResult ViewProducts()
        {
            var tableClient = new TableClient(_connectionString, "Products");
            tableClient.CreateIfNotExists();
            var products = tableClient.Query<Product>().ToList();
            return View(products);
        }

        [HttpGet]
        public IActionResult EditProduct(string rowKey)
        {
            var tableClient = new TableClient(_connectionString, "Products");
            var product = tableClient.GetEntity<Product>("Products", rowKey).Value;
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(Product updatedProduct)
        {
            var tableClient = new TableClient(_connectionString, "Products");
            var product = tableClient.GetEntity<Product>("Products", updatedProduct.RowKey).Value;

            product.ProductName = updatedProduct.ProductName;
            product.Category = updatedProduct.Category;
            product.Description = updatedProduct.Description;
            product.Price = updatedProduct.Price;
            product.Quantity = updatedProduct.Quantity;
            product.OutOfStock = updatedProduct.OutOfStock;

            tableClient.UpdateEntity(product, product.ETag, TableUpdateMode.Replace);

            await LogEventAsync($"Product updated: {product.ProductName}");
            TempData["Success"] = "Product updated successfully!";
            return RedirectToAction("ViewProducts");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct(string rowKey)
        {
            var tableClient = new TableClient(_connectionString, "Products");
            tableClient.DeleteEntity("Products", rowKey);

            await LogEventAsync($"Product deleted: {rowKey}");
            TempData["Success"] = "Product deleted successfully!";
            return RedirectToAction("ViewProducts");
        }
        #endregion
    }
}
