using Azure;
using Azure.Data.Tables;
using System.Security.Cryptography;
using System.Text;

public class AppUser : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }
}
