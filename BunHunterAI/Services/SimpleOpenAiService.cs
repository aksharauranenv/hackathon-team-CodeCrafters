using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BunHunterAI.Services
{
 public interface ISimpleOpenAiService
 {
 /// <summary>
 /// Returns the assistant's text content for the provided system and user prompts.
 /// </summary>
 Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt);
 }

 public class SimpleOpenAiService : ISimpleOpenAiService
 {
 private readonly IConfiguration _config;
 private readonly HttpClient _http;
 private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

 public SimpleOpenAiService(IConfiguration config, HttpClient http)
 {
 _config = config;
 _http = http;
 }

 public async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt)
 {
 // Configuration
 var useAzure = bool.TryParse(_config["OpenAI:UseAzure"], out var u) && u;

 if (useAzure)
 {
 var endpoint = (_config["AzureOpenAI:Endpoint"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? string.Empty).TrimEnd('/');
 var apiKey = _config["AzureOpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
 var deployment = _config["AzureOpenAI:DeploymentName"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
 var model = _config["AzureOpenAI:Model"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL") ?? "gpt-35-turbo";
 var apiVersion = _config["AzureOpenAI:ApiVersion"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION") ?? "2023-05-15";

 if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
 throw new InvalidOperationException("Azure OpenAI endpoint and key must be set.");

 string url;
 object payload;

 if (!string.IsNullOrEmpty(deployment))
 {
 url = $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
 payload = new
 {
 messages = new[]
 {
 new { role = "system", content = systemPrompt },
 new { role = "user", content = userPrompt }
 },
 max_tokens =500,
 temperature =0.0
 };
 }
 else
 {
 url = $"{endpoint}/openai/chat/completions?api-version={apiVersion}";
 payload = new
 {
 model = model,
 messages = new[]
 {
 new { role = "system", content = systemPrompt },
 new { role = "user", content = userPrompt }
 },
 max_tokens =500,
 temperature =0.0
 };
 }

 _http.DefaultRequestHeaders.Clear();
 _http.DefaultRequestHeaders.Add("api-key", apiKey);
 _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

 string contentString = JsonSerializer.Serialize(payload);
 using var content = new StringContent(contentString, Encoding.UTF8, "application/json");

 using var resp = await _http.PostAsync(url, content);
 var text = await resp.Content.ReadAsStringAsync();
 if (!resp.IsSuccessStatusCode)
 throw new InvalidOperationException($"Azure OpenAI returned {(int)resp.StatusCode}: {text}");

 try
 {
 using var doc = JsonDocument.Parse(text);
 var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
 return choice ?? string.Empty;
 }
 catch (JsonException)
 {
 return text;
 }
 }
 else
 {
 // Public OpenAI API
 var apiKey = _config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
 var model = _config["OpenAI:Model"] ?? "gpt-3.5-turbo";
 if (string.IsNullOrEmpty(apiKey))
 throw new InvalidOperationException("OpenAI API key must be set in OpenAI:ApiKey or OPENAI_API_KEY.");

 var url = "https://api.openai.com/v1/chat/completions";
 var payload = new
 {
 model = model,
 messages = new[]
 {
 new { role = "system", content = systemPrompt },
 new { role = "user", content = userPrompt }
 },
 max_tokens =500,
 temperature =0.0
 };

 _http.DefaultRequestHeaders.Clear();
 _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
 _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

 string contentString = JsonSerializer.Serialize(payload);
 using var content = new StringContent(contentString, Encoding.UTF8, "application/json");

 using var resp = await _http.PostAsync(url, content);
 var text = await resp.Content.ReadAsStringAsync();
 if (!resp.IsSuccessStatusCode)
 throw new InvalidOperationException($"OpenAI returned {(int)resp.StatusCode}: {text}");

 try
 {
 using var doc = JsonDocument.Parse(text);
 var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
 return choice ?? string.Empty;
 }
 catch (JsonException)
 {
 return text;
 }
 }
 }
 }
}
