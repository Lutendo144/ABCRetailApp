using Azure;
using Azure.Data.Tables;

namespace ABCRetailApp.Models
{
    public class Employee : ITableEntity
    {
        public string PartitionKey { get; set; } = "Employees";
        public string RowKey { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } 
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
