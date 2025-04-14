using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Company.Function
{
    public static class MyOrchestration
    {
        [Function(nameof(MyOrchestration))]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(MyOrchestration));
            logger.LogInformation("Starting fan-out.");
            var outputs = new List<string>();
            
            var tasks = new List<Task<string>>();
            for (int i = 1; i <= 1000; i++)
            {
                tasks.Add(context.CallActivityAsync<string>(nameof(SayHello), $"Activity{i}"));
            }
            
            // Wait for all tasks to complete
            string[] results = await Task.WhenAll(tasks);
            
            // Add results to outputs
            outputs.AddRange(results);
            
            logger.LogInformation($"Completed {outputs.Count} activities");
            return outputs;
        }

        [Function(nameof(SayHello))]
        public static async Task<string> SayHello([ActivityTrigger] string name, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SayHello");
            logger.LogInformation("Saying hello to {name}.", name);
            
            logger.LogInformation("Delaying...");
            await Task.Delay(TimeSpan.FromSeconds(60));
            logger.LogInformation("Delay complete for {name}.", name);
            
            return $"Hello {name}!";
        }

        [Function("MyOrchestration_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("MyOrchestration_HttpStart");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(MyOrchestration));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
