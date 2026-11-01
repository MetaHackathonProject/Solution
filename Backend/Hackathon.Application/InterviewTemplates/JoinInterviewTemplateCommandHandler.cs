using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Application.InterviewProgress;
using Hackathon.Application.InterviewSessions;
using Hackathon.Application.InterviewTasks;
using Hackathon.Domain.Accounts;
using Hackathon.Domain.InterviewSessions;
using Hackathon.Domain.InterviewTasks;
using Hackathon.Domain.InterviewTemplates;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class JoinInterviewTemplateCommandHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewTemplateStore templates,
    IInterviewAssignmentStore assignments,
    IInterviewSessionStore sessions,
    IOfferDetailsWriter offerDetails,
    IPendingInterviewTaskStore pendingTasks,
    IInterviewPlanWriter planWriter,
    IMediator mediator)
    : IRequestHandler<JoinInterviewTemplateCommand, JoinInterviewTemplateResult>
{
    public async Task<JoinInterviewTemplateResult> Handle(
        JoinInterviewTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Candidate,
            cancellationToken);

        var template = await templates.GetByInviteCodeAsync(request.InviteCode.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("Invite link is not valid.");

        if (string.Equals(template.EmployerUserId, userContext.UserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("You cannot join your own interview template.");
        }

        var existing = await assignments.GetByCandidateTemplateAsync(
            userContext.UserId,
            template.TemplateId,
            cancellationToken);
        if (existing is not null)
        {
            return new JoinInterviewTemplateResult(existing.SessionId, AlreadyJoined: true);
        }

        var definition = InterviewTemplateDefinition.Parse(template.DefinitionJson);
        var sessionId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var session = new InterviewSession(
            userContext.UserId,
            sessionId,
            $"{template.JobTitle} · {template.Company}",
            template.OfferUrl,
            new InterviewSessionSettings(template.Language, InterviewDifficulty.Auto),
            InterviewSessionStatus.Success,
            now,
            now,
            OverviewStatus.NotAvailable,
            InterviewSessionSource.Employer,
            template.EmployerUserId,
            template.TemplateId,
            RetakeAllowed: false,
            HideCameraOverview: true);

        await sessions.SaveAsync(session, cancellationToken);

        var overview = BuildVoiceOverview(template);
        await offerDetails.PutAsync(
            userContext.UserId,
            sessionId,
            template.JobTitle,
            template.Company,
            overview,
            cancellationToken);

        var plan = await WriteTasksAndPlanAsync(
            sessionId,
            definition,
            cancellationToken);

        await planWriter.PutAsync(userContext.UserId, sessionId, plan, cancellationToken);

        await mediator.Send(
            new SeedInterviewProgressCommand(userContext.UserId, sessionId),
            cancellationToken);

        var candidateEmail = FirstNonEmpty(userContext.Email, profile.Email);
        if (!string.IsNullOrWhiteSpace(candidateEmail) &&
            !string.Equals(profile.Email, candidateEmail, StringComparison.Ordinal))
        {
            await profiles.SaveAsync(
                profile with
                {
                    Email = candidateEmail,
                    UpdatedAt = now,
                },
                cancellationToken);
        }

        await assignments.SaveAsync(
            new InterviewAssignment(
                sessionId,
                template.EmployerUserId,
                userContext.UserId,
                template.TemplateId,
                template.Name,
                template.JobTitle,
                template.Company,
                now,
                candidateEmail),
            cancellationToken);

        return new JoinInterviewTemplateResult(sessionId, AlreadyJoined: false);
    }

    private async Task<InterviewPlan> WriteTasksAndPlanAsync(
        string sessionId,
        InterviewTemplateDefinition definition,
        CancellationToken cancellationToken)
    {
        var sections = new Dictionary<string, List<InterviewPlanQuestion>>(StringComparer.Ordinal);
        var moduleOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextOrder = 0;

        void EnsureSection(string module)
        {
            if (sections.ContainsKey(module))
            {
                return;
            }

            sections[module] = [];
            moduleOrder[module] = nextOrder++;
        }

        var spokenIndex = 0;
        foreach (var spoken in definition.SpokenQuestions)
        {
            EnsureSection(spoken.Module);
            sections[spoken.Module].Add(
                new InterviewPlanQuestion
                {
                    Id = string.IsNullOrWhiteSpace(spoken.Id) ? Guid.NewGuid().ToString() : spoken.Id,
                    Question = spoken.Question.Trim(),
                    AssessmentGoal = spoken.AssessmentGoal.Trim(),
                    Order = spokenIndex++,
                });
        }

        foreach (var task in definition.InteractiveTasks)
        {
            var taskId = string.IsNullOrWhiteSpace(task.Id) ? Guid.NewGuid().ToString() : task.Id;
            var content = task.QuestionContent.Trim();
            var formatted = string.IsNullOrWhiteSpace(task.QuestionContentFormatted)
                ? content
                : task.QuestionContentFormatted.Trim();

            await pendingTasks.PutAsync(
                new PendingInterviewTask(
                    sessionId,
                    taskId,
                    task.Module,
                    formatted,
                    content,
                    InteractiveTaskStatus.NotAsked,
                    task.ExpectedInput,
                    task.AssessmentGoal.Trim()),
                cancellationToken);

            EnsureSection(task.Module);
            sections[task.Module].Add(
                new InterviewPlanQuestion
                {
                    Id = taskId,
                    Question = content,
                    AssessmentGoal = task.AssessmentGoal.Trim(),
                    Order = spokenIndex++,
                    Type = "interactive",
                    TaskId = taskId,
                });
        }

        return new InterviewPlan
        {
            EstimatedDurationMinutes = Math.Max(20, (definition.SpokenQuestions.Count * 4) +
                (definition.InteractiveTasks.Count * 12)),
            Sections = sections
                .OrderBy(pair => moduleOrder[pair.Key])
                .Select(pair => new InterviewPlanSection
                {
                    Module = pair.Key,
                    ModuleOrder = moduleOrder[pair.Key],
                    EstimatedDurationMinutes = Math.Max(5, pair.Value.Count * 4),
                    Objective = $"Employer-authored {pair.Key} questions.",
                    Questions = pair.Value,
                })
                .ToList(),
        };
    }

    private static string BuildVoiceOverview(InterviewTemplate template)
    {
        var overview =
            $"Interview for {template.JobTitle} at {template.Company}. Language: {template.Language}.";
        if (!string.IsNullOrWhiteSpace(template.OfferUrl))
        {
            overview += $" Offer posting: {template.OfferUrl}.";
        }

        return overview;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
