using System.Net;
using System.Net.Sockets;
using Hackathon.Application.InterviewTemplates;

namespace Hackathon.Infrastructure.InterviewTemplates;

public sealed class HttpOfferPageReader(IHttpClientFactory httpClientFactory) : IOfferPageReader
{
    public async Task<ImportedOfferResult> ReadAsync(string offerUrl, CancellationToken cancellationToken)
    {
        var url = offerUrl.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Offer URL must be http or https.");
        }

        if (IsBlockedHost(uri))
        {
            throw new ArgumentException("That URL cannot be imported.");
        }

        var html = await TryDownloadHtmlAsync(uri, cancellationToken);
        var fromHtml = html is null ? (Title: (string?)null, Company: (string?)null) : OfferPageFacts.FromHtml(html, uri);
        var fromUrl = OfferPageFacts.FromUrl(uri);
        var title = FirstNonEmpty(fromHtml.Title, fromUrl.Title);
        var company = FirstNonEmpty(fromHtml.Company, fromUrl.Company);
        if (title is null && company is null)
        {
            throw new InvalidOperationException(
                "Could not read that posting. Enter title and company manually.");
        }

        return new ImportedOfferResult(title, company, uri.ToString());
    }

    private async Task<string?> TryDownloadHtmlAsync(Uri uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("OfferImport");
        try
        {
            using var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return OfferPageFacts.IsChallengeHtml(html) ? null : html;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private static bool IsBlockedHost(Uri uri)
    {
        var host = uri.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        return IPAddress.IsLoopback(address) ||
               address.IsIPv6LinkLocal ||
               IsPrivateIPv4(address);
    }

    private static bool IsPrivateIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168) ||
               (bytes[0] == 169 && bytes[1] == 254);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
