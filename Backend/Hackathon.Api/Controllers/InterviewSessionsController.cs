using Hackathon.Application.InterviewOverview;
using Hackathon.Application.InterviewPreparation;
using Hackathon.Application.InterviewSessions;
using Hackathon.Application.InterviewTasks;
using Hackathon.Application.VoiceAgent;
using Hackathon.Domain.InterviewSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hackathon.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/interview-sessions")]
public sealed class InterviewSessionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InterviewSessionResponse>>> List(
        CancellationToken cancellationToken)
    {
        var sessions = await mediator.Send(new ListInterviewSessionsQuery(), cancellationToken);
        return Ok(sessions.Select(ToResponse).ToList());
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<InterviewSessionResponse>> Get(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var session = await mediator.Send(new GetInterviewSessionQuery(sessionId), cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(session));
    }

    [HttpGet("{sessionId}/overview")]
    public async Task<ActionResult<InterviewOverviewResponse>> GetOverview(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var overview = await mediator.Send(
            new GetInterviewOverviewQuery(sessionId),
            cancellationToken);

        if (overview is null)
        {
            return NotFound();
        }

        return Ok(ToOverviewResponse(overview));
    }

    [HttpPost]
    public async Task<StartInterviewPreparationResult> Start(
        StartInterviewPreparationRequest request,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new StartInterviewPreparationCommand(
                request.Name,
                request.OfferUrl,
                new InterviewSessionSettings(
                    request.Settings.RecruiterLanguage,
                    request.Settings.Difficulty)),
            cancellationToken);
    }

    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> Delete(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var deleted = await mediator.Send(
            new DeleteInterviewSessionCommand(sessionId),
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{sessionId}/retake")]
    public async Task<ActionResult<RetakeInterviewSessionResponse>> Retake(
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new RetakeInterviewSessionCommand(sessionId),
                cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(new RetakeInterviewSessionResponse(
                result.SessionId,
                result.QuestionsSeeded));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("{sessionId}/voice-context")]
    public async Task<ActionResult<VoiceAgentContextResponse>> GetVoiceContext(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var context = await mediator.Send(
            new GetVoiceAgentContextQuery(sessionId),
            cancellationToken);

        if (context is null)
        {
            return NotFound();
        }

        return Ok(new VoiceAgentContextResponse(
            context.JobTitle,
            context.Company,
            context.JobOverview,
            context.Language));
    }

    [HttpGet("{sessionId}/interactive-task")]
    public async Task<ActionResult<InteractiveTaskResponse>> GetActiveInteractiveTask(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var task = await mediator.Send(
            new GetActiveInteractiveTaskQuery(sessionId),
            cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        return Ok(ToInteractiveResponse(task));
    }

    [HttpGet("{sessionId}/interactive-task/{taskId}")]
    public async Task<ActionResult<InteractiveTaskResponse>> GetInteractiveTask(
        string sessionId,
        string taskId,
        CancellationToken cancellationToken)
    {
        var task = await mediator.Send(
            new GetInteractiveTaskQuery(sessionId, taskId),
            cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        return Ok(ToInteractiveResponse(task));
    }

    [HttpGet("{sessionId}/interactive-task/{taskId}/vision-review")]
    public async Task<ActionResult<VisionReviewResponse>> GetVisionReview(
        string sessionId,
        string taskId,
        CancellationToken cancellationToken)
    {
        var review = await mediator.Send(
            new GetVisionReviewQuery(sessionId, taskId),
            cancellationToken);

        if (review is null)
        {
            return NotFound();
        }

        return Ok(new VisionReviewResponse(review.TaskId, review.Status, review.Description));
    }

    [HttpPost("{sessionId}/interactive-task/{taskId}/answer")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<SubmitInteractiveAnswerResponse>> SubmitInteractiveAnswer(
        string sessionId,
        string taskId,
        IFormFile? file,
        [FromForm] string? text,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await BuildAnswerStreamAsync(file, text, cancellationToken);
            if (content.Stream is null)
            {
                return BadRequest(new { detail = "Provide either text or a file answer." });
            }

            await using (content.Stream)
            {
                var saved = await mediator.Send(
                    new SubmitInteractiveAnswerCommand(
                        sessionId,
                        taskId,
                        content.Stream,
                        content.ContentType,
                        content.Extension),
                    cancellationToken);

                if (saved is null)
                {
                    return NotFound();
                }

                return Ok(new SubmitInteractiveAnswerResponse(saved.VisionReviewStatus));
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    private static InteractiveTaskResponse ToInteractiveResponse(ActiveInteractiveTaskResult task) =>
        new(
            task.TaskId,
            task.Module,
            task.QuestionContentFormatted,
            task.QuestionContent,
            task.Status,
            task.ExpectedInput);

    private static async Task<AnswerPayload> BuildAnswerStreamAsync(
        IFormFile? file,
        string? text,
        CancellationToken cancellationToken)
    {
        if (file is { Length: > 0 })
        {
            var stream = file.OpenReadStream();
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bin";
            }

            return new AnswerPayload(
                stream,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                extension);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var stream = new MemoryStream();
            await using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                await writer.WriteAsync(text.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
            }

            stream.Position = 0;
            return new AnswerPayload(stream, "text/plain; charset=utf-8", ".txt");
        }

        return new AnswerPayload(null, string.Empty, string.Empty);
    }

    private sealed record AnswerPayload(Stream? Stream, string ContentType, string Extension);

    private static InterviewSessionResponse ToResponse(InterviewSession session) =>
        new(
            session.SessionId,
            session.Name,
            session.OfferUrl,
            session.Status,
            session.CreatedAt,
            session.UpdatedAt,
            new InterviewSessionSettingsResponse(
                session.Settings.RecruiterLanguage,
                session.Settings.Difficulty),
            session.OverviewStatus);

    private static InterviewOverviewResponse ToOverviewResponse(InterviewResultOverview overview) =>
        new(
            overview.Summary,
            overview.Score,
            overview.Questions
                .Select(question => new InterviewOverviewQuestionResponse(
                    question.Question,
                    question.Answer,
                    question.Opinion,
                    question.Recommendations))
                .ToList(),
            overview.Recommendations,
            overview.InteractiveTasks
                .Select(task => new InterviewOverviewInteractiveTaskResponse(
                    task.TaskId,
                    task.Kind,
                    task.Score,
                    task.Strengths,
                    task.Gaps,
                    task.Summary))
                .ToList());
}

public sealed class StartInterviewPreparationRequest
{
    public required string OfferUrl { get; init; }

    public required string Name { get; init; }

    public required InterviewSessionSettingsRequest Settings { get; init; }
}

public sealed class InterviewSessionSettingsRequest
{
    public required string RecruiterLanguage { get; init; }

    public required string Difficulty { get; init; }
}

public sealed record InterviewSessionResponse(
    string SessionId,
    string Name,
    string? OfferUrl,
    string Status,
    long CreatedAt,
    long UpdatedAt,
    InterviewSessionSettingsResponse Settings,
    string OverviewStatus);

public sealed record InterviewSessionSettingsResponse(
    string RecruiterLanguage,
    string Difficulty);

public sealed record InterviewOverviewResponse(
    string Summary,
    int Score,
    IReadOnlyList<InterviewOverviewQuestionResponse> Questions,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<InterviewOverviewInteractiveTaskResponse> InteractiveTasks);

public sealed record InterviewOverviewQuestionResponse(
    string Question,
    string Answer,
    string Opinion,
    IReadOnlyList<string> Recommendations);

public sealed record InterviewOverviewInteractiveTaskResponse(
    string TaskId,
    string Kind,
    int Score,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    string Summary);

public sealed record VoiceAgentContextResponse(
    string? JobTitle,
    string? Company,
    string? JobOverview,
    string? Language);

public sealed record InteractiveTaskResponse(
    string TaskId,
    string Module,
    string QuestionContentFormatted,
    string QuestionContent,
    string Status,
    string ExpectedInput);

public sealed record SubmitInteractiveAnswerResponse(string VisionReviewStatus);

public sealed record VisionReviewResponse(
    string TaskId,
    string Status,
    string? Description);

public sealed record RetakeInterviewSessionResponse(
    string SessionId,
    int QuestionsSeeded);
