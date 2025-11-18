using BunHunterAI.Models;
using BunHunterAI.Services;
using DefectLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BunHunterAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BugSummaryController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IOpenAiClientService _openAi;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly JiraService _jira;

        public BugSummaryController(IConfiguration config, IOpenAiClientService openAi, JiraService jira)
        {
            _config = config;
            _openAi = openAi;
            _jira = jira;
        }

        [HttpPost]
        public async Task<IActionResult> Post(string request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request))
                return BadRequest(new { error = "stackTrace is required" });

            var systemPrompt = "You are a smart assistant that extracts a concise title, a brif summary, and a severity (Low/Medium/High/Critical) from an error stack trace. Respond with a JSON object only, with keys: title, summary, severity.";
            var userPrompt = $"Stack trace:\n{request}\n\nReturn JSON only.";

            string contentNode;
            try
            {
                contentNode = await _openAi.GetChatCompletionAsync(systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error calling Azure OpenAI: " + ex.Message });
            }

            try
            {
                var summary = JsonSerializer.Deserialize<BugSummary>(contentNode, _jsonOptions);
                if (summary != null && !string.IsNullOrWhiteSpace(summary.Title))
                    return Ok(summary);

                return Ok(contentNode);
            }
            catch (Exception)
            {
                return Ok(contentNode);
            }
        }

        [HttpPost("issues/bug")]
        public async Task<IActionResult> createBugOnJira(string request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request))
                return BadRequest(new { error = "stackTrace is required" });

            var systemPrompt = "You are a smart assistant that extracts a concise title, a brif summary, and a severity (Low/Medium/High/Critical) from an error stack trace. Respond with a JSON object only, with keys: title, summary, severity.";
            var userPrompt = $"Stack trace:\n{request}\n\nReturn JSON only.";

            string contentNode;
            try
            {
                contentNode = await _openAi.GetChatCompletionAsync(systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error calling Azure OpenAI: " + ex.Message });
            }

            try
            {
                CreateJiraIssueDto dto = new CreateJiraIssueDto();
                dto.ProjectKey = "COD"; 
                dto.IssueType = "Bug";
                dto.Priority = "High";
                dto.Summary = "NullReferenceException in WeatherForecastController";
                dto.Description = "A NullReferenceException occurred in the WeatherForecastController at line 25 due to an object being accessed without proper initialization. This issue could disrupt functionality in the Weather Forecast feature.";
                dto.Assignee = "Akshara Urane";
                dto.Labels = new string[] { "bug" };
                var result = await _jira.CreateIssueAsync(dto);
                // Return the created issue key and id
                return Created($"{Request.Scheme}://{Request.Host}/api/jira/issue/{result.Key}", new { result.Key, result.Id, result.Self });
            }
            catch (Exception)
            {
                return Ok(contentNode);
            }
        }
    }
}
