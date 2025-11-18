using Microsoft.AspNetCore.Mvc;
using DefectLogSystem.Services;

namespace DefectLogSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JiraController : ControllerBase
    {
        private readonly Services.JiraService _jira;
        private readonly ILogger<JiraController> _logger;

        public JiraController(Services.JiraService jira, ILogger<JiraController> logger)
        {
            _jira = jira;
            _logger = logger;
        }

        // POST api/jira/issue
        [HttpPost("issue")]
        public async Task<IActionResult> CreateIssue([FromBody] CreateJiraIssueDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _jira.CreateIssueAsync(dto, cancellationToken);
                // Return the created issue key and id
                return Created($"{Request.Scheme}://{Request.Host}/api/jira/issue/{result.Key}", new { result.Key, result.Id, result.Self });
            }
            catch (ArgumentException aex)
            {
                return BadRequest(aex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Jira issue");
                return StatusCode(500, "Failed to create Jira issue.");
            }
        }

        // GET api/jira/issues/bugs?projectKey=COD
        [HttpPost("issues/bugs")]
        public async Task<IActionResult> GetBugs([FromQuery] string? projectKey, CancellationToken cancellationToken)
        {
            try
            {
                // Build JQL to fetch Bug issues; include project filter if provided
                var jql = string.IsNullOrWhiteSpace(projectKey)
                    ? "issuetype = Bug ORDER BY created DESC"
                    : $"project = {projectKey} AND issuetype = Bug ORDER BY created DESC";

                // Fetch all issues using the GET-based paged helper
                var issues = await _jira.GetAllIssuesByJqlGetAsync(jql, 50, cancellationToken);

                // Return flat list of issues to the client
                return Ok(issues);
            }
            catch (ArgumentException aex)
            {
                return BadRequest(aex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Jira bugs");
                return StatusCode(500, "Failed to fetch Jira issues.");
            }
        }

        // other endpoints (CreateIssue etc.) remain unchanged
    }
}