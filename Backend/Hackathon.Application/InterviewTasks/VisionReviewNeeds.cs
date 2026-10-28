using Hackathon.Domain.InterviewTasks;

namespace Hackathon.Application.InterviewTasks;

public static class VisionReviewNeeds
{
    public static bool RequiresVisionReview(
        string expectedInput,
        string contentType,
        string fileExtension)
    {
        if (expectedInput is ExpectedInput.Diagram or ExpectedInput.Schema)
        {
            return true;
        }

        var type = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (type.StartsWith("image/", StringComparison.Ordinal) ||
            type is "application/pdf")
        {
            return true;
        }

        var ext = (fileExtension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        return ext is "png" or "jpg" or "jpeg" or "gif" or "webp" or "pdf" or "drawio.png";
    }

    public static string BuildAnswerKey(
        string userId,
        string sessionId,
        string taskId,
        string fileExtension)
    {
        var extension = string.IsNullOrWhiteSpace(fileExtension)
            ? "bin"
            : fileExtension.TrimStart('.');
        return $"{userId}/{sessionId}/interactive-tasks/{taskId}/answer.{extension}";
    }
}
