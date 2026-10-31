namespace Hackathon.Application.InterviewTemplates;

public interface IOfferPageReader
{
    Task<ImportedOfferResult> ReadAsync(string offerUrl, CancellationToken cancellationToken);
}
