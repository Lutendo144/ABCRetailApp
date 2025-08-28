using Azure;
using Azure.Data.Tables;

namespace ABCRetailApp.Models
{
    public class CartItem : ITableEntity
    {
        public string PartitionKey { get; set; } = "Cart";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal => Price * Quantity;
    }
}
