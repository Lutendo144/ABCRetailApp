using Azure;
using Azure.Data.Tables;
using ABCRetailApp.Models;
using System.Security.Cryptography;

namespace ABCRetailApp.Helpers
{
    public static class AzureTableInitializer
    {
        public static void InitializeEmployeeTable(string connectionString, string tableName)
        {
            var tableClient = new TableClient(connectionString, tableName);
            tableClient.CreateIfNotExists();
        }

        public static void AddInitialEmployees(string connectionString, string tableName)
        {
            var tableClient = new TableClient(connectionString, tableName);

           
            var employees = new[]
            {
                new { RowKey = "emp001", FullName = "John Smith", Email = "john@abc.com", Password = "Easy@123" },
                new { RowKey = "emp002", FullName = "Jane Doe", Email = "jane@abc.com", Password = "Pass@456" },
                new { RowKey = "emp003", FullName = "Michael Brown", Email = "michael@abc.com", Password = "Admin@789" }
            };

            foreach (var emp in employees)
            {
                try
                {
                    
                    var existing = tableClient.GetEntity<Employee>("Employees", emp.RowKey);
                    continue;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                  
                }

                
                using (var sha256 = SHA256.Create())
                {
                    var hashedPassword = Convert.ToBase64String(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(emp.Password)));

                    var employee = new Employee
                    {
                        RowKey = emp.RowKey,
                        FullName = emp.FullName,
                        Email = emp.Email,
                        PasswordHash = hashedPassword,
                        Role = "Employee",
                        PartitionKey = "Employees"
                    };

                    tableClient.AddEntity(employee);
                }
            }
        }
    }
}
