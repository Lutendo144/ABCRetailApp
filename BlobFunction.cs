using Azure.Storage.Blobs;
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
    public class BlobFunction
    {
        [FunctionName("UploadToBlob")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var content = await new StreamReader(req.Body).ReadToEndAsync();
            var blobClient = new BlobContainerClient(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "files");
            await blobClient.CreateIfNotExistsAsync();

            var blob = blobClient.GetBlobClient($"file-{System.Guid.NewGuid()}.txt");
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await blob.UploadAsync(ms);

            log.LogInformation("File uploaded to blob storage.");
            return new OkObjectResult("Uploaded to blob storage!");
        }
    }
}
