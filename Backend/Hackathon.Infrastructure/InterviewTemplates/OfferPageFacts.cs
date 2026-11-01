using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hackathon.Infrastructure.InterviewTemplates;

internal static class OfferPageFacts
{
    public static bool IsChallengeHtml(string html) =>
        html.Contains("cf-mitigated", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("Attention Required! | Cloudflare", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase);

    public static (string? Title, string? Company) FromHtml(string html, Uri pageUrl)
    {
        var jsonLd = FromJsonLd(html);
        var heading = TagText(html, "h1");
        var documentTitle = TagText(html, "title");
        var hiring = FromHiringTitle(documentTitle);
        var ogTitle = MetaContent(html, "og:title");
        var twitterTitle = MetaContent(html, "twitter:title");
        var siteName = FirstNonEmpty(
            MetaContent(html, "og:site_name"),
            MetaContent(html, "application-name"));

        var title = FirstNonEmpty(
            jsonLd.Title,
            heading,
            hiring.Title,
            ogTitle,
            twitterTitle,
            StripSiteSuffix(documentTitle, siteName));

        var company = FirstNonEmpty(
            jsonLd.Company,
            hiring.Company,
            IsBoardName(siteName, pageUrl) ? null : siteName);

        return (TrimTitle(title), TrimTitle(company));
    }

    public static (string? Title, string? Company) FromUrl(Uri pageUrl)
    {
        if (!IsPilotsGlobal(pageUrl.Host))
        {
            return (null, null);
        }

        var match = Regex.Match(
            pageUrl.AbsolutePath,
            @"^/job/(?<slug>.+)-(?<country>[A-Za-z]{2})-(?<id>[A-Za-z0-9]{6,})/?$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return (null, null);
        }

        var body = match.Groups["slug"].Value;
        var separator = body.LastIndexOf('-');
        if (separator <= 0 || separator >= body.Length - 1)
        {
            return (TrimTitle(HumanizeSlug(body)), null);
        }

        return (
            TrimTitle(HumanizeSlug(body[..separator])),
            TrimTitle(HumanizeSlug(body[(separator + 1)..])));
    }

    private static (string? Title, string? Company) FromJsonLd(string html)
    {
        foreach (Match block in Regex.Matches(
                     html,
                     @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant))
        {
            var json = WebUtility.HtmlDecode(block.Groups[1].Value).Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (TryReadJobPosting(document.RootElement, out var title, out var company))
                {
                    return (title, company);
                }
            }
            catch (JsonException)
            {
                // Some pages embed invalid JSON-LD; fall through to HTML tags.
            }
        }

        return (null, null);
    }

    private static bool TryReadJobPosting(JsonElement element, out string? title, out string? company)
    {
        title = null;
        company = null;
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryReadJobPosting(item, out title, out company))
                {
                    return true;
                }
            }

            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (element.TryGetProperty("@graph", out var graph) &&
            TryReadJobPosting(graph, out title, out company))
        {
            return true;
        }

        if (!IsType(element, "JobPosting"))
        {
            return false;
        }

        title = ReadString(element, "title") ?? ReadString(element, "name");
        if (element.TryGetProperty("hiringOrganization", out var org))
        {
            company = ReadString(org, "name");
        }

        return title is not null || company is not null;
    }

    private static bool IsType(JsonElement element, string type)
    {
        if (!element.TryGetProperty("@type", out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return string.Equals(value.GetString(), type, StringComparison.OrdinalIgnoreCase);
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().Any(item =>
                string.Equals(item.GetString(), type, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static (string? Title, string? Company) FromHiringTitle(string? documentTitle)
    {
        if (string.IsNullOrWhiteSpace(documentTitle))
        {
            return (null, null);
        }

        var match = Regex.Match(
            documentTitle,
            @"^(?<company>.+?)\s+is hiring\s+(?<title>.+?)(?:\s*[-|].*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return (null, null);
        }

        return (match.Groups["title"].Value, match.Groups["company"].Value);
    }

    private static string? StripSiteSuffix(string? title, string? siteName)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        if (!string.IsNullOrWhiteSpace(siteName))
        {
            title = Regex.Replace(
                title,
                $@"\s*[-|]\s*{Regex.Escape(siteName.Trim())}\s*$",
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return title;
    }

    private static bool IsBoardName(string? name, Uri pageUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var brand = pageUrl.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? pageUrl.Host[4..]
            : pageUrl.Host;
        var hostBrand = brand.Split('.')[0];
        var compact = Regex.Replace(name, "[^A-Za-z0-9]", string.Empty);
        return string.Equals(compact, hostBrand, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPilotsGlobal(string host) =>
        host.Equals("pilotsglobal.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("www.pilotsglobal.com", StringComparison.OrdinalIgnoreCase);

    private static string HumanizeSlug(string slug)
    {
        var parts = slug.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(
            ' ',
            parts.Select(part =>
                part.Any(char.IsDigit)
                    ? part.ToUpperInvariant()
                    : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
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
