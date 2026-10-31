using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Application.InterviewProgress;
using Hackathon.Application.InterviewSessions;
using Hackathon.Application.InterviewTasks;
using Hackathon.Domain.Accounts;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class GetGeneratedTemplateFromOfferQueryHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewSessionStore sessions,
    IInterviewPlanReader planReader,
    IPendingInterviewTaskStore pendingTasks,
    IOfferDetailsReader offerDetails)
    : IRequestHandler<GetGeneratedTemplateFromOfferQuery, GeneratedTemplateFromOfferResult?>
{
    public async Task<GeneratedTemplateFromOfferResult?> Handle(
        GetGeneratedTemplateFromOfferQuery request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        var session = await sessions.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (session.Status != InterviewSessionStatus.Success)
        {
            return new GeneratedTemplateFromOfferResult(
                session.SessionId,
                session.Status,
                null,
                null,
                session.OfferUrl ?? string.Empty,
                [],
                []);
        }

        var details = await offerDetails.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);
        var plan = await planReader.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken)
            ?? throw new InvalidOperationException("Generated plan was not found.");

        var spoken = plan.Sections
            .OrderBy(section => section.ModuleOrder)
            .SelectMany(section => section.Questions
                .OrderBy(question => question.Order)
                .Where(question => !question.IsInteractive)
                .Select(question => new TemplateSpokenQuestion
                {
                    Id = question.Id,
                    Module = section.Module,
                    Question = question.Question,
                    AssessmentGoal = question.AssessmentGoal,
                }))
            .ToList();

        var tasks = (await pendingTasks.ListBySessionAsync(request.SessionId, cancellationToken))
            .Select(task => new TemplateInteractiveTask
            {
                Id = task.TaskId,
                Module = task.Module,
                QuestionContent = task.QuestionContent,
                QuestionContentFormatted = task.QuestionContentFormatted,
                ExpectedInput = task.ExpectedInput,
                AssessmentGoal = task.AssessmentGoal ?? string.Empty,
            })
            .ToList();

        if (spoken.Count == 0)
        {
            throw new InvalidOperationException("Generation finished without spoken questions.");
        }

        return new GeneratedTemplateFromOfferResult(
            session.SessionId,
            session.Status,
            details?.Title,
            details?.CompanyName,
            session.OfferUrl ?? string.Empty,
            spoken,
            tasks);
    }
}
