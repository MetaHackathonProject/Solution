using Hackathon.Application.InterviewTemplates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hackathon.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/interview-templates")]
public sealed class InterviewTemplatesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InterviewTemplateResponse>>> List(
        CancellationToken cancellationToken)
    {
        try
        {
            var templates = await mediator.Send(new ListInterviewTemplatesQuery(), cancellationToken);
            return Ok(templates.Select(ToResponse).ToList());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("{templateId}")]
    public async Task<ActionResult<InterviewTemplateResponse>> Get(
        string templateId,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = await mediator.Send(new GetInterviewTemplateQuery(templateId), cancellationToken);
            return template is null ? NotFound() : Ok(ToResponse(template));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<InterviewTemplateResponse>> Create(
        SaveInterviewTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await mediator.Send(
                new CreateInterviewTemplateCommand(
                    request.Name,
                    request.JobTitle,
                    request.Company,
                    request.Language,
                    request.OfferUrl,
                    ToDefinition(request)),
                cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPut("{templateId}")]
    public async Task<ActionResult<InterviewTemplateResponse>> Update(
        string templateId,
        SaveInterviewTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await mediator.Send(
                new UpdateInterviewTemplateCommand(
                    templateId,
                    request.Name,
                    request.JobTitle,
                    request.Company,
                    request.Language,
                    request.OfferUrl,
                    ToDefinition(request)),
                cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpDelete("{templateId}")]
    public async Task<IActionResult> Delete(string templateId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await mediator.Send(
                new DeleteInterviewTemplateCommand(templateId),
                cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("generate-from-offer")]
    public async Task<ActionResult<StartEmployerOfferGenerationResponse>> GenerateFromOffer(
        GenerateFromOfferRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var started = await mediator.Send(
                new StartEmployerOfferGenerationCommand(request.OfferUrl, request.Language),
                cancellationToken);
            return Ok(new StartEmployerOfferGenerationResponse(started.SessionId));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("generate-from-offer/{sessionId}")]
    public async Task<ActionResult<GeneratedTemplateFromOfferResponse>> GetGeneratedFromOffer(
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var generated = await mediator.Send(
                new GetGeneratedTemplateFromOfferQuery(sessionId),
                cancellationToken);
            return generated is null
                ? NotFound()
                : Ok(new GeneratedTemplateFromOfferResponse(
                    generated.SessionId,
                    generated.Status,
                    generated.Title,
                    generated.Company,
                    generated.OfferUrl,
                    generated.SpokenQuestions
                        .Select(question => new TemplateSpokenQuestionResponse(
                            question.Id,
                            question.Module,
                            question.Question,
                            question.AssessmentGoal))
                        .ToList(),
                    generated.InteractiveTasks
                        .Select(task => new TemplateInteractiveTaskResponse(
                            task.Id,
                            task.Module,
                            task.QuestionContent,
                            task.QuestionContentFormatted,
                            task.ExpectedInput,
                            task.AssessmentGoal))
                        .ToList()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("import-offer")]
    public async Task<ActionResult<ImportedOfferResponse>> ImportOffer(
        ImportOfferRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var imported = await mediator.Send(
                new ImportOfferFromUrlCommand(request.OfferUrl),
                cancellationToken);
            return Ok(new ImportedOfferResponse(imported.Title, imported.Company, imported.OfferUrl));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("join/{inviteCode}")]
    public async Task<ActionResult<InterviewInvitePreviewResponse>> Preview(
        string inviteCode,
        CancellationToken cancellationToken)
    {
        var preview = await mediator.Send(new GetInvitePreviewQuery(inviteCode), cancellationToken);
        return preview is null
            ? NotFound()
            : Ok(new InterviewInvitePreviewResponse(
                preview.InviteCode,
                preview.Name,
                preview.JobTitle,
                preview.Company,
                preview.Language));
    }

    [HttpPost("join/{inviteCode}")]
    public async Task<ActionResult<JoinInterviewTemplateResponse>> Join(
        string inviteCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new JoinInterviewTemplateCommand(inviteCode), cancellationToken);
            return Ok(new JoinInterviewTemplateResponse(result.SessionId, result.AlreadyJoined));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<IReadOnlyList<EmployerAssignmentResponse>>> Assignments(
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await mediator.Send(new ListEmployerAssignmentsQuery(), cancellationToken);
            return Ok(
                items.Select(item => new EmployerAssignmentResponse(
                    item.SessionId,
                    item.TemplateId,
                    item.TemplateName,
                    item.JobTitle,
                    item.Company,
                    item.CandidateUserId,
                    item.Status,
                    item.OverviewStatus,
                    item.CreatedAt))
                    .ToList());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    private static InterviewTemplateDefinition ToDefinition(SaveInterviewTemplateRequest request) =>
        new()
        {
            SpokenQuestions = request.SpokenQuestions
                .Select(question => new TemplateSpokenQuestion
                {
                    Id = question.Id,
                    Module = question.Module,
                    Question = question.Question,
                    AssessmentGoal = question.AssessmentGoal,
                })
                .ToList(),
            InteractiveTasks = request.InteractiveTasks
                .Select(task => new TemplateInteractiveTask
                {
                    Id = task.Id,
                    Module = task.Module,
                    QuestionContent = task.QuestionContent,
                    QuestionContentFormatted = task.QuestionContentFormatted,
                    ExpectedInput = task.ExpectedInput,
                    AssessmentGoal = task.AssessmentGoal,
                })
                .ToList(),
        };

    private static InterviewTemplateResponse ToResponse(InterviewTemplateResult template) =>
        new(
            template.TemplateId,
            template.Name,
            template.JobTitle,
            template.Company,
            template.Language,
            template.OfferUrl,
            template.InviteCode,
            template.Definition.SpokenQuestions
                .Select(question => new TemplateSpokenQuestionResponse(
                    question.Id,
                    question.Module,
                    question.Question,
                    question.AssessmentGoal))
                .ToList(),
            template.Definition.InteractiveTasks
                .Select(task => new TemplateInteractiveTaskResponse(
                    task.Id,
                    task.Module,
                    task.QuestionContent,
                    task.QuestionContentFormatted,
                    task.ExpectedInput,
                    task.AssessmentGoal))
                .ToList(),
            template.CreatedAt,
            template.UpdatedAt);
}

public sealed class SaveInterviewTemplateRequest
{
    public required string Name { get; init; }

    public required string JobTitle { get; init; }

    public required string Company { get; init; }

    public required string Language { get; init; }

    public string? OfferUrl { get; init; }

    public IReadOnlyList<TemplateSpokenQuestionRequest> SpokenQuestions { get; init; } = [];

    public IReadOnlyList<TemplateInteractiveTaskRequest> InteractiveTasks { get; init; } = [];
}

public sealed class TemplateSpokenQuestionRequest
{
    public string? Id { get; init; }

    public required string Module { get; init; }

    public required string Question { get; init; }

    public required string AssessmentGoal { get; init; }
}

public sealed class TemplateInteractiveTaskRequest
{
    public string? Id { get; init; }

    public required string Module { get; init; }

    public required string QuestionContent { get; init; }

    public string? QuestionContentFormatted { get; init; }

    public required string ExpectedInput { get; init; }

    public required string AssessmentGoal { get; init; }
}

public sealed class ImportOfferRequest
{
    public required string OfferUrl { get; init; }
}

public sealed class GenerateFromOfferRequest
{
    public required string OfferUrl { get; init; }

    public string Language { get; init; } = "English";
}

public sealed record StartEmployerOfferGenerationResponse(string SessionId);

public sealed record GeneratedTemplateFromOfferResponse(
    string SessionId,
    string Status,
    string? Title,
    string? Company,
    string OfferUrl,
    IReadOnlyList<TemplateSpokenQuestionResponse> SpokenQuestions,
    IReadOnlyList<TemplateInteractiveTaskResponse> InteractiveTasks);

public sealed record InterviewTemplateResponse(
    string TemplateId,
    string Name,
    string JobTitle,
    string Company,
    string Language,
    string? OfferUrl,
    string InviteCode,
    IReadOnlyList<TemplateSpokenQuestionResponse> SpokenQuestions,
    IReadOnlyList<TemplateInteractiveTaskResponse> InteractiveTasks,
    long CreatedAt,
    long UpdatedAt);

public sealed record TemplateSpokenQuestionResponse(
    string? Id,
    string Module,
    string Question,
    string AssessmentGoal);

public sealed record TemplateInteractiveTaskResponse(
    string? Id,
    string Module,
    string QuestionContent,
    string? QuestionContentFormatted,
    string ExpectedInput,
    string AssessmentGoal);

public sealed record InterviewInvitePreviewResponse(
    string InviteCode,
    string Name,
    string JobTitle,
    string Company,
    string Language);

public sealed record JoinInterviewTemplateResponse(string SessionId, bool AlreadyJoined);

public sealed record EmployerAssignmentResponse(
    string SessionId,
    string TemplateId,
    string TemplateName,
    string JobTitle,
    string Company,
    string CandidateUserId,
    string Status,
    string OverviewStatus,
    long CreatedAt);

public sealed record ImportedOfferResponse(string? Title, string? Company, string OfferUrl);
