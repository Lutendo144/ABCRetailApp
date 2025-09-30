using Azure.Storage.Files.Shares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace abcretail
{
    public class FileFunction
    {
        [FunctionName("UploadToFileShare")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var content = await new StreamReader(req.Body).ReadToEndAsync();
            var shareClient = new ShareClient(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
                "fileshare");
            await shareClient.CreateIfNotExistsAsync();

            var dir = shareClient.GetRootDirectoryClient();
            var file = dir.GetFileClient($"file-{System.Guid.NewGuid()}.txt");

            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await file.CreateAsync(ms.Length);
            ms.Position = 0;
            await file.UploadRangeAsync(new Azure.HttpRange(0, ms.Length), ms);

            log.LogInformation("File uploaded to Azure File Share.");
            return new OkObjectResult("File uploaded to Azure File Share!");
        }
    }
}
