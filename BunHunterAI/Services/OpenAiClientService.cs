using Microsoft.SemanticKernel;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace BunHunterAI.Services
{
    public interface IOpenAiClientService
    {
        Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt);
    }

    public class OpenAiClientService : IOpenAiClientService
    {

        public OpenAiClientService()
        {

        }

        public async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt)
        {

            string modelId = "gpt-4o";              
            string endpoint = "https://hackathon-openai-codecrafter-svc11.openai.azure.com"; // e.g., azure openai endpoint
            string apiKey = "CPoAZo0LsAUt3bvOMG8JdtkLWlgc3A6ga65wDcQaJIumKBwZpipaJQQJ99BKAC5T7U2XJ3w3AAABACOG2l10";
            string deploymentName = "CodeCrafter";
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(
      deploymentName: deploymentName,     // ?? this is your custom model deployment name
      endpoint: endpoint,      // e.g. https://my-openai-resource.openai.azure.com/
      apiKey: apiKey);


            Kernel kernel = builder.Build();

            // example text to summarise
            string textToSummarise = userPrompt;
            string prompt = $@"
                            You are a smart quality analyzer AI.
                        Given this error or stack trace, extract:
                        1. A short title (max 10 words)
                        2. A concise summary (1–2 sentences)
                        3. A severity level: [Low, Medium, High, Critical]
                        Format the response as JSON.
                        Error:{textToSummarise}";

            string result = string.Empty;
            try
            {
                 var result1 = await kernel.InvokePromptAsync(prompt);
                    result = result1.ToString();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during summarization: {ex.Message}");
            }
            Console.WriteLine("Summary:");
            Console.WriteLine(result);
            return result;
        }
    }
}
