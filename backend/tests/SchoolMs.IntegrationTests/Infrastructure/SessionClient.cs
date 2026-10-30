using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using SchoolMs.Infrastructure.Email;
using SchoolMs.Infrastructure.Security;
using SchoolMs.Modules.Identity.Endpoints;

namespace SchoolMs.IntegrationTests.Infrastructure;

/// <summary>HTTP client with the cookie jar of one browser on one organisation's subdomain.</summary>
public sealed class SessionClient(HttpClient http, CookieContainer cookies, Uri baseAddress) : IDisposable
{
    public const string SessionPath = "/api/v1/identity/session";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HttpClient Http => http;

    public CookieContainer Cookies => cookies;

    public Uri BaseAddress => baseAddress;

    /// <summary>Loads the session as the web app does on start, which issues the antiforgery token.</summary>
    public async Task<CurrentUserResponse?> StartAsync() => await GetUserAsync();

    public async Task<CurrentUserResponse?> GetUserAsync()
    {
        var session = await http.GetFromJsonAsync<SessionResponse>(new Uri(SessionPath, UriKind.Relative), Ct);
        return session!.User;
    }

    public Task<HttpResponseMessage> PostAsync(string path, object body) =>
        http.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct);

    public Task<HttpResponseMessage> SignInAsync(string identifier, string password) =>
        PostAsync(SessionPath, new SignInRequest(identifier, password));

    public void Dispose() => http.Dispose();
}

/// <summary>Copies the readable XSRF-TOKEN cookie into the header on state-changing requests, like the web app.</summary>
internal sealed class AntiforgeryHeaderHandler(CookieContainer cookies) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get && cookies.GetCookies(request.RequestUri!)[ApiAntiforgery.RequestTokenCookie] is { } token)
        {
            request.Headers.Add(ApiAntiforgery.HeaderName, token.Value);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Collects outgoing email so tests can follow the links in it.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Waits for the background job to send an email to <paramref name="toAddress"/> matching the subject.</summary>
    public async Task<EmailMessage> WaitForAsync(string toAddress, string subjectContains, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < deadline)
        {
            var message = _sent.LastOrDefault(m => m.ToAddress == toAddress && m.Subject.Contains(subjectContains, StringComparison.Ordinal));
            if (message is not null)
            {
                return message;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No email '{subjectContains}' was sent to {toAddress}.");
    }

    /// <summary>The user id and token from the link's fragment: <c>#uid=…&amp;token=…</c>.</summary>
    public static (Guid UserId, string Token, Uri Link) ReadLink(EmailMessage message)
    {
        var match = LinkPattern().Match(message.TextBody);
        match.Success.ShouldBeTrue("the email contains a link");
        return (Guid.Parse(match.Groups["uid"].Value), Uri.UnescapeDataString(match.Groups["token"].Value), new Uri(match.Value));
    }

    [GeneratedRegex(@"http\S+#uid=(?<uid>[0-9a-f-]{36})&token=(?<token>\S+)")]
    private static partial Regex LinkPattern();
}
