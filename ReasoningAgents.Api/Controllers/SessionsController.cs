using Microsoft.AspNetCore.Mvc;
using ReasoningAgents.Api.Contracts.Requests;
using ReasoningAgents.Api.Contracts.Responses;
using ReasoningAgents.Api.Parsing;
using ReasoningAgents.Application.Services;
using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Api.Controllers
{
    [ApiController]
    [Route("api/sessions")]
    public sealed class SessionsController : ControllerBase
    {
        private readonly IAssessmentSessionService _service;

        public SessionsController(IAssessmentSessionService service) => _service = service;

        [HttpPost]
        public async Task<ActionResult<CreateSessionResponse>> Create([FromBody] CreateSessionRequest req, CancellationToken ct)
        {
            var goal = new CertificationGoal(req.Cert, req.Days, req.Minutes);
            var result = await _service.CreateSessionAsync(goal, req.Exam, ct);

            var questions = AssessmentParser.Parse(result.AssessmentText);

            return Ok(new CreateSessionResponse(
                SessionId: result.SessionId,
                Questions: questions,
                CreatedUtc: DateTimeOffset.UtcNow
            ));
        }

        [HttpPost("{id}/answers")]
        public async Task<ActionResult<SubmitAnswersResponse>> SubmitAnswers(string id, [FromBody] SubmitAnswersRequest req, CancellationToken ct)
        {
            var result = await _service.SubmitAnswersAsync(id, req.Answers, ct);

            return Ok(new SubmitAnswersResponse(
                Passed: result.Passed,
                Score: result.Score,
                Summary: result.Summary,
                Issues: result.Issues.ToArray(),
                Improvements: result.Improvements.ToArray(),
                LearningPathJson: result.LearningPathJson,
                StudyPlanJson: result.StudyPlanJson
            ));
        }
    }
}
