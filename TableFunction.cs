using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace abcretail
{
    public class Product : ITableEntity
    {
        public string PartitionKey { get; set; } = "Products";
        public string RowKey { get; set; }
        public string ProductName { get; set; }
        public double Price { get; set; }

     
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    public class TableFunction
    {
        [FunctionName("StoreToTable")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var content = await new StreamReader(req.Body).ReadToEndAsync();
            var product = JsonConvert.DeserializeObject<Product>(content);

            var tableClient = new TableClient(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
                "ProductMetadata");

            await tableClient.CreateIfNotExistsAsync();
            await tableClient.AddEntityAsync(product); 

            log.LogInformation($"Stored product {product.ProductName} to table.");
            return new OkObjectResult($"Product {product.ProductName} saved!");
        }
    }
}
