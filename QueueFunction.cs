using Azure.Storage.Queues;
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
    public class QueueFunction
    {
        [FunctionName("SendToQueue")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var content = await new StreamReader(req.Body).ReadToEndAsync();

            var queueClient = new QueueClient(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
                "messages");
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)));

            log.LogInformation("Message sent to queue.");
            return new OkObjectResult("Message queued!");
        }
    }
}
