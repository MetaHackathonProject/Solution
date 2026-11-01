using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.StepFunctions;
using Hackathon.Application.Accounts;
using Hackathon.Application.InterviewOverview;
using Hackathon.Application.InterviewPreparation;
using Hackathon.Application.InterviewProgress;
using Hackathon.Application.InterviewQuestions;
using Hackathon.Application.InterviewSessions;
using Hackathon.Application.InterviewTasks;
using Hackathon.Application.InterviewTemplates;
using Hackathon.Application.VoiceAgent;
using Hackathon.Infrastructure.Accounts;
using Hackathon.Infrastructure.InterviewOverview;
using Hackathon.Infrastructure.InterviewPreparation;
using Hackathon.Infrastructure.InterviewProgress;
using Hackathon.Infrastructure.InterviewQuestions;
using Hackathon.Infrastructure.InterviewSessions;
using Hackathon.Infrastructure.InterviewTasks;
using Hackathon.Infrastructure.InterviewTemplates;
using Hackathon.Infrastructure.Options;
using Hackathon.Infrastructure.VoiceAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon.Infrastructure.Register;

public static class RegisterInDI
{
    public static void RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InterviewSessionsOptions>(configuration.GetSection(InterviewSessionsOptions.SectionName));
        services.Configure<InterviewPlannerOptions>(configuration.GetSection(InterviewPlannerOptions.SectionName));
        services.Configure<SolutionVisionReviewOptions>(configuration.GetSection(SolutionVisionReviewOptions.SectionName));
        services.Configure<InterviewOverviewOptions>(configuration.GetSection(InterviewOverviewOptions.SectionName));
        services.Configure<InterviewProgressOptions>(configuration.GetSection(InterviewProgressOptions.SectionName));
        services.Configure<PendingInterviewTasksOptions>(configuration.GetSection(PendingInterviewTasksOptions.SectionName));
        services.Configure<InterviewConversationsOptions>(configuration.GetSection(InterviewConversationsOptions.SectionName));
        services.Configure<UserProfilesOptions>(configuration.GetSection(UserProfilesOptions.SectionName));
        services.Configure<InterviewTemplatesOptions>(configuration.GetSection(InterviewTemplatesOptions.SectionName));
        services.Configure<ElevenLabsOptions>(configuration.GetSection(ElevenLabsOptions.SectionName));
        services.Configure<MetaModelApiOptions>(configuration.GetSection(MetaModelApiOptions.SectionName));

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client());
        services.AddSingleton<IAmazonStepFunctions>(_ => new AmazonStepFunctionsClient());
        services.AddSingleton<IUserProfileStore, DynamoDbUserProfileStore>();
        services.AddSingleton<IInterviewTemplateStore, DynamoDbInterviewTemplateStore>();
        services.AddSingleton<IInterviewAssignmentStore, DynamoDbInterviewAssignmentStore>();
        services.AddSingleton<IOfferPageReader, HttpOfferPageReader>();
        services.AddSingleton<IInterviewSessionAccess, InterviewSessionAccessResolver>();
        services.AddSingleton<IInterviewSessionStore, DynamoDbInterviewSessionStore>();
        services.AddSingleton<IInterviewSessionArtifactStore, InterviewSessionArtifactStore>();
        services.AddSingleton<IInterviewPreparationStarter, StepFunctionsInterviewPreparationStarter>();
        services.AddSingleton<ISolutionVisionReviewStarter, StepFunctionsSolutionVisionReviewStarter>();
        services.AddSingleton<IInterviewOverviewStarter, StepFunctionsInterviewOverviewStarter>();
        services.AddSingleton<IInterviewPlanReader, S3InterviewPlanReader>();
        services.AddSingleton<IInterviewPlanWriter, S3InterviewPlanWriter>();
        services.AddSingleton<IInterviewProgressStore, DynamoDbInterviewProgressStore>();
        services.AddSingleton<IPendingInterviewTaskStore, DynamoDbPendingInterviewTaskStore>();
        services.AddSingleton<IInteractiveAnswerStore, S3InteractiveAnswerStore>();
        services.AddSingleton<IInterviewRecordingStore, S3InterviewRecordingStore>();
        services.AddSingleton<IInteractiveAnswerReader, S3InteractiveAnswerReader>();
        services.AddSingleton<ISolutionVisionDescriber, MetaSolutionVisionDescriber>();
        services.AddSingleton<IOfferDetailsReader, S3OfferDetailsReader>();
        services.AddSingleton<IOfferDetailsWriter, S3OfferDetailsWriter>();
        services.AddSingleton<IElevenLabsConversationClient, ElevenLabsConversationClient>();
        services.AddSingleton<IInterviewConversationStore, DynamoDbInterviewConversationStore>();
        services.AddSingleton<IInterviewQuestionHelperStore, DynamoDbInterviewQuestionHelperStore>();
        services.AddSingleton<IInterviewOverviewArtifactStore, S3InterviewOverviewArtifactStore>();
        services.AddSingleton<IElevenLabsTranscriptClient, ElevenLabsTranscriptClient>();
        services.AddSingleton<IInteractiveSolutionJudge, MetaInteractiveSolutionJudge>();
        services.AddSingleton<FfmpegCameraMediaNormalizer>();
        services.AddSingleton<ICameraPresenceJudge, MetaCameraPresenceJudge>();
        services.AddSingleton<IInterviewOverviewComposer, MetaInterviewOverviewComposer>();

        var elevenLabs = configuration.GetSection(ElevenLabsOptions.SectionName).Get<ElevenLabsOptions>();
        services.AddHttpClient("ElevenLabs", client =>
        {
            var baseUrl = string.IsNullOrWhiteSpace(elevenLabs?.ApiUrl)
                ? "https://api.elevenlabs.io"
                : elevenLabs.ApiUrl.TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl);
        });

        var meta = configuration.GetSection(MetaModelApiOptions.SectionName).Get<MetaModelApiOptions>();
        services.AddHttpClient("MetaModelApi", client =>
        {
            var baseUrl = string.IsNullOrWhiteSpace(meta?.ApiUrl)
                ? "https://api.meta.ai/v1"
                : meta.ApiUrl.TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(270);
        });
        services.AddHttpClient("FfmpegDownload", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("hackathon-build-interview-overview");
        });
        services.AddHttpClient("OfferImport", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        });
    }
}
