using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Zlet.FolderConverter.App.Settings;

public sealed record StableRelease(Version Version, Uri Page);
public sealed record UpdateResult(string ResourceKey, StableRelease? Release = null);

public interface IUpdateChecker
{
    Task<UpdateResult> CheckAsync(string currentVersion, CancellationToken cancellationToken = default);
}

public sealed class GitHubUpdateChecker(HttpClient client) : IUpdateChecker
{
    public const string RepositoryUrl = "https://github.com/zlet-labs/zlet-converter";
    public const string ApiUrl = "https://api.github.com/repos/zlet-labs/zlet-converter/releases";

    public static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (tag is null || !Regex.IsMatch(tag, @"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant)) return false;
        return Version.TryParse(tag.TrimStart('v').Split('+')[0], out version!);
    }

    public static StableRelease? SelectRelease(JsonElement entries)
    {
        if (entries.ValueKind != JsonValueKind.Array) throw new JsonException();
        StableRelease? latest = null;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
                !entry.TryGetProperty("prerelease", out var prerelease) || prerelease.ValueKind != JsonValueKind.False ||
                !entry.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String ||
                !TryParseVersion(tag.GetString(), out var version) ||
                !entry.TryGetProperty("html_url", out var url) || url.ValueKind != JsonValueKind.String ||
                !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var page) ||
                page.Scheme != Uri.UriSchemeHttps || page.Host != "github.com" || !page.IsDefaultPort ||
                page.UserInfo.Length != 0 || page.Query.Length != 0 || page.Fragment.Length != 0 ||
                page.AbsolutePath != "/zlet-labs/zlet-converter/releases/tag/" + Uri.EscapeDataString(tag.GetString()!)) continue;
            if (latest is null || version > latest.Version) latest = new(version, page);
        }
        return latest;
    }

    public async Task<UpdateResult> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (!TryParseVersion(currentVersion, out var current)) return new("UpdateInvalidVersion");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            StableRelease? latest = null;
            // Traverse pages so a page of prereleases cannot hide the stable release.
            for (var page = 1; page <= 10; page++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}?per_page=100&page={page}");
                request.Headers.UserAgent.ParseAdd("ZletConverter/" + currentVersion);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var response = await client.SendAsync(request, timeout.Token);
                if (!response.IsSuccessStatusCode)
                    return new((int)response.StatusCode is 403 or 429 ? "UpdateRateLimit" : "UpdateGitHubError");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                var selected = SelectRelease(json.RootElement);
                if (selected is not null && (latest is null || selected.Version > latest.Version)) latest = selected;
                if (json.RootElement.GetArrayLength() < 100)
                    return latest is null ? new("UpdateEmpty") : latest.Version > current
                        ? new("UpdateAvailable", latest) : new("UpdateCurrent");
            }
            return new("UpdateGitHubError"); // Do not claim up-to-date from an incomplete listing.
        }
        catch (OperationCanceledException) { return new("UpdateTimeout"); }
        catch (HttpRequestException) { return new("UpdateNetworkError"); }
        catch (System.IO.IOException) { return new("UpdateNetworkError"); }
        catch (JsonException) { return new("UpdateMalformed"); }
    }
}
