using System.Text.Json.Serialization;
using MediatR;

namespace Hackathon.Application.InterviewProgress;

public sealed record PopNextInterviewQuestionsCommand(string SessionId)
    : IRequest<PopNextInterviewQuestionsResult>;

public sealed record PopNextInterviewQuestionsResult(
    [property: JsonPropertyName("questions")] IReadOnlyList<InterviewQuestionResultItem> Questions,
    [property: JsonPropertyName("questionsRemaining")] int QuestionsRemaining);

public sealed record InterviewQuestionResultItem(
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("questionObjective")] string QuestionObjective,
    [property: JsonPropertyName("moduleObjective")] string ModuleObjective,
    [property: JsonPropertyName("module")] string Module);
