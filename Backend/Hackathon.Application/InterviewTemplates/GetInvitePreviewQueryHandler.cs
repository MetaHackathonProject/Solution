using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class GetInvitePreviewQueryHandler(IInterviewTemplateStore templates)
    : IRequestHandler<GetInvitePreviewQuery, InterviewInvitePreview?>
{
    public async Task<InterviewInvitePreview?> Handle(
        GetInvitePreviewQuery request,
        CancellationToken cancellationToken)
    {
        var code = request.InviteCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var template = await templates.GetByInviteCodeAsync(code, cancellationToken);
        return template is null
            ? null
            : new InterviewInvitePreview(
                template.InviteCode,
                template.Name,
                template.JobTitle,
                template.Company,
                template.Language);
    }
}
