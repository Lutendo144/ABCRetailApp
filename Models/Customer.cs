using Azure;
using Azure.Data.Tables;

namespace ABCRetailApp.Models
{
    public class Customer : ITableEntity
    {
        public string PartitionKey { get; set; } = "Customers";
        public string RowKey { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
