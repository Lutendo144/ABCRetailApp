using Azure;
using Azure.Data.Tables;
using System;

namespace ABCRetailApp.Models
{
    public class Product : ITableEntity
    {
        public string PartitionKey { get; set; } = "Products";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public string ProductName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public bool OutOfStock { get; set; }
        public string SellerEmail { get; set; }
        public string ImageUrl { get; set; }
    }


}
