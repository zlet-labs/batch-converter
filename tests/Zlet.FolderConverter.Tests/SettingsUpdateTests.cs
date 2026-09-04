using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Runtime.InteropServices;
using Zlet.FolderConverter.App;
using Zlet.FolderConverter.App.Settings;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Tests;

public sealed class SettingsUpdateTests
{
    private static string Entry(string tag, bool draft = false, bool prerelease = false, string? url = null) =>
        JsonSerializer.Serialize(new { tag_name = tag, draft, prerelease,
            html_url = url ?? GitHubUpdateChecker.RepositoryUrl + "/releases/tag/" + Uri.EscapeDataString(tag) });

    [Theory]
    [InlineData("0.0.9", "0.0.10", -1)]
    [InlineData("0.0.2", "v0.0.2", 0)]
    [InlineData("1.0.0", "0.9.9", 1)]
    [InlineData("v1.2.3+build.1", "1.2.3", 0)]
    public void Numeric_versions_compare(string a, string b, int expected)
    {
        Assert.True(GitHubUpdateChecker.TryParseVersion(a, out var left));
        Assert.True(GitHubUpdateChecker.TryParseVersion(b, out var right));
        Assert.Equal(expected, Math.Sign(left.CompareTo(right)));
    }

    [Theory]
    [InlineData("oops")][InlineData("1.2")][InlineData("1.2.3-beta")][InlineData("01.2.3")]
    [InlineData("999999999999999999999.0.0")][InlineData("")]
    public void Unusable_versions_are_ignored(string tag) => Assert.False(GitHubUpdateChecker.TryParseVersion(tag, out _));

    [Theory]
    [InlineData("v0.0.2", "UpdateCurrent")]
    [InlineData("v0.0.3", "UpdateAvailable")]
    [InlineData("v0.0.1", "UpdateCurrent")]
    public async Task Current_newer_and_older(string tag, string key)
    {
        using var client = Client("[" + Entry(tag) + "]");
        var result = await new GitHubUpdateChecker(client).CheckAsync("0.0.2");
        Assert.Equal(key, result.ResourceKey);
        if (key == "UpdateAvailable") Assert.Equal(GitHubUpdateChecker.RepositoryUrl + "/releases/tag/" + tag, result.Release!.Page.AbsoluteUri);
        else Assert.Null(result.Release);
    }

    [Fact]
    public async Task Selection_ignores_drafts_prereleases_malformed_entries_and_unsafe_urls()
    {
        var entries = new[] { Entry("v0.0.3"), Entry("v9.0.0", draft: true), Entry("v8.0.0", prerelease: true),
            Entry("bad"), Entry("7.0.0-beta"), "null", "42", "{}", "{\"draft\":\"false\"}",
            Entry("6.0.0", url: "https://evil.example/releases/tag/6.0.0"),
            Entry("5.0.0", url: GitHubUpdateChecker.RepositoryUrl + "/releases/tag/0.0.1"), Entry("0.0.1") };
        using var client = Client("[" + string.Join(",", entries) + "]");
        var result = await new GitHubUpdateChecker(client).CheckAsync("0.0.2");
        Assert.Equal(new Version(0, 0, 3), result.Release!.Version);
        Assert.EndsWith("/v0.0.3", result.Release.Page.AbsoluteUri);
    }

    [Theory]
    [InlineData("{broken", "UpdateMalformed")][InlineData("{}", "UpdateMalformed")]
    [InlineData("null", "UpdateMalformed")][InlineData("[]", "UpdateEmpty")]
    [InlineData("[null,{},42]", "UpdateEmpty")]
    public async Task Malformed_and_empty_responses(string body, string key)
    {
        using var client = Client(body);
        Assert.Equal(key, (await new GitHubUpdateChecker(client).CheckAsync("0.0.2")).ResourceKey);
    }

    [Theory]
    [InlineData(403, "UpdateRateLimit")][InlineData(429, "UpdateRateLimit")]
    [InlineData(500, "UpdateGitHubError")][InlineData(404, "UpdateGitHubError")]
    public async Task Http_failures(int status, string key)
    {
        using var client = Client("", (HttpStatusCode)status);
        Assert.Equal(key, (await new GitHubUpdateChecker(client).CheckAsync("0.0.2")).ResourceKey);
    }

    [Theory]
    [InlineData(true, "UpdateTimeout")][InlineData(false, "UpdateNetworkError")]
    public async Task Transport_failures(bool timeout, string key)
    {
        using var client = new HttpClient(new Handler((_, _) => throw (timeout ? new TaskCanceledException() : new HttpRequestException())));
        var result = await new GitHubUpdateChecker(client).CheckAsync("0.0.2");
        Assert.Equal(key, result.ResourceKey);
        foreach (var language in new[] { "en-US", "ru-RU" })
            Assert.False(string.IsNullOrWhiteSpace(LocalizationService.CreateStandalone(language).Get(result.ResourceKey)));
    }

    [Fact]
    public async Task Pagination_finds_stable_release_after_prereleases()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler((request, token) =>
        {
            calls++;
            Assert.True(token.CanBeCanceled);
            Assert.StartsWith(GitHubUpdateChecker.ApiUrl, request.RequestUri!.AbsoluteUri);
            Assert.NotEmpty(request.Headers.UserAgent);
            var body = calls == 1 ? "[" + string.Join(",", Enumerable.Repeat(Entry("1.0.0", prerelease: true), 100)) + "]" : "[" + Entry("0.0.3") + "]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }));
        Assert.Equal("UpdateAvailable", (await new GitHubUpdateChecker(client).CheckAsync("0.0.2")).ResourceKey);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Settings_creation_does_not_check_and_small_layout_remains_scrollable()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var fake = new CountingChecker();
                var window = new SettingsWindow(fake);
                Assert.Equal(0, fake.Calls);
                window.Width = 340; window.Height = 300;
                window.Measure(new System.Windows.Size(340, 300));
                window.Arrange(new System.Windows.Rect(0, 0, 340, 300));
                var scroll = Assert.IsType<System.Windows.Controls.ScrollViewer>(window.Content);
                Assert.Equal(System.Windows.Controls.ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        Assert.Null(failure);
    }

    [Theory]
    [InlineData("en-US")][InlineData("ru-RU")]
    public void Diagnostics_are_exactly_the_allowlisted_fields(string language)
    {
        var l = LocalizationService.CreateStandalone(language);
        var text = DiagnosticsText.Create(l, [new(OfficeApplicationKind.Word, true), new(OfficeApplicationKind.Excel, false), new(OfficeApplicationKind.PowerPoint, true)]);
        Assert.Equal(new[] { $"{ProductIdentity.Name} {ProductIdentity.Version}",
            l.Format("DiagnosticsWindows", Environment.OSVersion.Version),
            l.Format("DiagnosticsOsArch", RuntimeInformation.OSArchitecture),
            l.Format("DiagnosticsAppArch", RuntimeInformation.ProcessArchitecture),
            l.Format("DiagnosticsLanguage", language),
            "Word: " + l.Get("OfficeAvailable"), "Excel: " + l.Get("OfficeUnavailable"), "PowerPoint: " + l.Get("OfficeAvailable") }, text.Split(Environment.NewLine));
    }

    [Theory]
    [InlineData("{broken")][InlineData("{\"language\":\"en-US\",\"other\":42}")]
    public void Reset_requires_confirmation_and_only_deletes_store_file(string json)
    {
        var root = Path.Combine(Path.GetTempPath(), "zlet-reset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var settings = Path.Combine(root, "settings.json");
        var unrelated = Path.Combine(root, "result.zip");
        try
        {
            File.WriteAllText(settings, json); File.WriteAllText(unrelated, "untouched");
            var store = new AppSettingsStore(settings);
            Assert.False(store.TryReset(false).Success);
            Assert.Equal(json, File.ReadAllText(settings));
            using (File.Open(settings, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.False(store.TryReset(true).Success);
            Assert.Equal(json, File.ReadAllText(settings));
            Assert.True(store.TryReset(true).Success);
            Assert.False(File.Exists(settings));
            Assert.Null(store.LoadLanguage());
            Assert.True(StartupLanguageResolver.Resolve(null, store.LoadLanguage()).ChooserRequired);
            Assert.Equal("untouched", File.ReadAllText(unrelated));
            Assert.True(store.TryReset(true).Success);
        }
        finally { File.Delete(settings); File.Delete(unrelated); Directory.Delete(root); }
    }

    private static HttpClient Client(string body, HttpStatusCode status = HttpStatusCode.OK) => new(new Handler((_, _) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) })));
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class CountingChecker : IUpdateChecker
    {
        public int Calls { get; private set; }
        public Task<UpdateResult> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new UpdateResult("UpdateCurrent")); }
    }
}
