using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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

        var client = httpClientFactory.CreateClient("OfferImport");
        using var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Could not read that posting. Enter title and company manually.");
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var title = FirstNonEmpty(
            MetaContent(html, "og:title"),
            MetaContent(html, "twitter:title"),
            TagText(html, "title"));
        var company = FirstNonEmpty(
            MetaContent(html, "og:site_name"),
            MetaContent(html, "application-name"));

        return new ImportedOfferResult(TrimTitle(title), TrimTitle(company), uri.ToString());
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

    private static string? MetaContent(string html, string property)
    {
        var pattern =
            $@"<meta[^>]+(?:property|name)\s*=\s*[""']{Regex.Escape(property)}[""'][^>]+content\s*=\s*[""']([^""']+)[""']|<meta[^>]+content\s*=\s*[""']([^""']+)[""'][^>]+(?:property|name)\s*=\s*[""']{Regex.Escape(property)}[""']";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }

    private static string? TagText(string html, string tag)
    {
        var match = Regex.Match(
            html,
            $@"<{tag}[^>]*>(.*?)</{tag}>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return match.Success
            ? WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "<.*?>", string.Empty))
            : null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(value => value?.Trim()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? TrimTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = Regex.Replace(value, @"\s+", " ").Trim();
        return trimmed.Length > 160 ? trimmed[..160] : trimmed;
    }
}
