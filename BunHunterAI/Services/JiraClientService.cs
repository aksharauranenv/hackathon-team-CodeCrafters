using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BunHunterAI.Services
{
    public interface IJiraClientService
    {
        /// <summary>
        /// Creates a Jira issue of type 'Bug' (or specified issueType) and returns the issue key (e.g. PROJ-123) and browse URL.
        /// </summary>
        Task<(string IssueKey, string IssueUrl)> CreateBugAsync(string projectKey, string summary, string description, string issueType = "Bug");
    }

    public class JiraClientService : IJiraClientService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public JiraClientService(IConfiguration config, HttpClient http)
        {
            _config = config;
            _http = http;
        }

        public async Task<(string IssueKey, string IssueUrl)> CreateBugAsync(string projectKey, string summary, string description, string issueType = "Bug")
        {
            var baseUrl = (_config["Jira:BaseUrl"] ?? Environment.GetEnvironmentVariable("JIRA_BASE_URL") ?? string.Empty).TrimEnd('/');
            var email = _config["Jira:Email"] ?? Environment.GetEnvironmentVariable("JIRA_EMAIL");
            var apiToken = _config["Jira:ApiToken"] ?? Environment.GetEnvironmentVariable("JIRA_API_TOKEN");

            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(apiToken))
                throw new InvalidOperationException("Jira configuration missing. Set Jira:BaseUrl, Jira:Email, Jira:ApiToken or corresponding environment variables.");

            var requestUrl = $"{baseUrl}/rest/api/3/issue";

            var payload = new
            {
                fields = new
                {
                    project = new { key = projectKey },
                    summary = summary,
                    description = new { type = "doc", version = 1, content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = description } } } } },
                    issuetype = new { name = issueType }
                }
            };

            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            var resp = await _http.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Jira API returned {(int)resp.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var key = root.GetProperty("key").GetString() ?? string.Empty;
            var issueUrl = $"{baseUrl}/browse/{key}";

            return (key, issueUrl);
        }
    }
}
