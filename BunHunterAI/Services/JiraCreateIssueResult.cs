using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DefectLogSystem.Services
{
    public class JiraCreateIssueResult
    {
        public string Id { get; init; } = "";
        public string Key { get; init; } = "";
        public string Self { get; init; } = "";
    }

    public class CreateJiraIssueDto
    {
        public string ProjectKey { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Description { get; set; } = "";
        public string IssueType { get; set; } = "Bug";
        public string? Assignee { get; set; }
        public string? Priority { get; set; }

        public string[]? Labels { get; set; }
    }

    public class JiraService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public JiraService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        // Build a minimal Atlassian Document Format doc for a plain-text description
        object? BuildAdfFromText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new {
                        type = "paragraph",
                        content = new[]
                        {
                            new { type = "text", text = text }
                        }
                    }
                }
            };
        }

        public async Task<JiraCreateIssueResult> CreateIssueAsync(CreateJiraIssueDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.ProjectKey)) throw new ArgumentException("ProjectKey required", nameof(dto.ProjectKey));
            if (string.IsNullOrWhiteSpace(dto.Summary)) throw new ArgumentException("Summary required", nameof(dto.Summary));

            // Use when building payload
            var payload = new
            {
                fields = new Dictionary<string, object?>
                {
                    ["project"] = new { key = dto.ProjectKey },
                    ["summary"] = dto.Summary,
                    ["description"] = BuildAdfFromText(dto.Description),
                    ["issuetype"] = new { name = dto.IssueType },
                    ["priority"] = dto.Priority is null ? null : new { name = dto.Priority }
                }
            };

            if (!string.IsNullOrWhiteSpace(dto.Assignee))
            {
                // For cloud, use accountId in many instances; some setups accept "name" or "emailAddress"
                payload.fields["assignee"] = new { name = dto.Assignee };
            }

            if (dto.Labels is { Length: > 0 })
            {
                payload.fields["labels"] = dto.Labels;
            }

            var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/rest/api/3/issue", content, ct);

            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                // include body in exception to help debugging (don't leak tokens to logs)
                throw new InvalidOperationException($"Jira API error {(int)response.StatusCode}: {body}");
            }

            var result = JsonSerializer.Deserialize<JiraCreateIssueResult>(body, _jsonOptions)
                         ?? throw new InvalidOperationException("Unable to parse Jira response.");

            return result;
        }

        // Fetch all issues matching the JQL using the GET search endpoint (paged).
        // Returns a flat list of JiraIssue objects.
        public async Task<List<JiraIssue>> GetAllIssuesByJqlGetAsync(string jql, int pageSize = 50, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jql)) throw new ArgumentException("jql required", nameof(jql));
            if (pageSize <= 0 || pageSize > 1000) pageSize = 50; // sensible guard

            var all = new List<JiraIssue>();
            var startAt = 0;

            while (true)
            {
                var q = System.Web.HttpUtility.UrlEncode(jql);
                var uri = $"/rest/api/3/search?jql={q}&startAt={startAt}&maxResults={pageSize}&fields=summary,description,issuetype,priority,assignee,labels";
                using var response = await _http.GetAsync(uri, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                        throw new InvalidOperationException($"Jira API removed endpoint (410). Response: {body}");
                    throw new InvalidOperationException($"Jira API error {(int)response.StatusCode}: {body}");
                }

                var page = JsonSerializer.Deserialize<JiraSearchResult>(body, _jsonOptions)
                           ?? throw new InvalidOperationException("Unable to parse Jira search response.");

                if (page.Issues != null && page.Issues.Length > 0)
                {
                    all.AddRange(page.Issues);
                }

                startAt += page.Issues?.Length ?? 0;

                // stop when we've retrieved all reported by the server
                if (startAt >= page.Total) break;

                // safety: if server reports 0 total or no issues returned, break to avoid tight loop
                if (page.Total == 0 || page.Issues == null || page.Issues.Length == 0) break;
            }

            return all;
        }

        // Existing POST-based search (kept for compatibility)
        public async Task<JiraSearchResult?> GetIssuesByJqlAsync(string jql, int maxResults, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(jql)) throw new ArgumentException("jql required", nameof(jql));

            var payload = new
            {
                jql = jql,
                startAt = 0,
                maxResults = maxResults,
                fields = new[] { "summary", "description", "issuetype", "priority", "assignee", "labels" }
            };
            var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");

            using var req = new HttpRequestMessage(HttpMethod.Post, "/rest/api/3/search/jql")
            {
                Content = content
            };
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(req, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    // explicit guidance that endpoint is removed; caller can surface this.
                    throw new InvalidOperationException($"Jira API removed endpoint (410). Response: {body}");
                }

                throw new InvalidOperationException($"Jira API error {(int)response.StatusCode}: {body}");
            }

            var result = JsonSerializer.Deserialize<JiraSearchResult>(body, _jsonOptions);
            return result;
        }

        // Types for deserialization of search results (minimal)
        public class JiraSearchResult
        {
            public int StartAt { get; set; }
            public int MaxResults { get; set; }
            public int Total { get; set; }
            public JiraIssue[]? Issues { get; set; }
        }

        public class JiraIssue
        {
            public string Id { get; set; } = "";
            public string Key { get; set; } = "";
            public string Self { get; set; } = "";
            public JiraIssueFields? Fields { get; set; }
        }

        public class JiraIssueFields
        {
            public string? Summary { get; set; }
            public object? Description { get; set; }
            public object? Priority { get; set; }
            public object? Issuetype { get; set; }
            public object? Assignee { get; set; }
            public string[]? Labels { get; set; }
        }
    }
}