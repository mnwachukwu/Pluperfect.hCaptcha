using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Pluperfect.HCaptcha;

/// <summary>Verifies tokens against hcaptcha.com.</summary>
/// <remarks>
/// Takes an <see cref="HttpClient"/> from the factory, configured at registration with the base
/// address and timeout. Constructing one per request exhausts sockets under any real load.
/// </remarks>
/// <param name="client">The configured client.</param>
/// <param name="secretKey">The secret half of the key pair.</param>
/// <param name="logger">Where failures are recorded.</param>
public sealed partial class HCaptchaVerifier(
    HttpClient client,
    string secretKey,
    ILogger<HCaptchaVerifier> logger) : ICaptchaVerifier
{
    /// <summary>The name this library registers its <see cref="HttpClient"/> under.</summary>
    public const string HttpClientName = "Pluperfect.HCaptcha";

    private const string VerifyPath = "siteverify";

    /// <inheritdoc />
    public async Task<CaptchaResult> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return CaptchaResult.Failed("missing-input-response");
        }

        // A form body rather than a query string: a secret in a URL ends up in the access log of
        // everything the request passes through.
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = secretKey,
            ["response"] = token,
        });

        VerifyResponse? result;

        try
        {
            using var response = await client.PostAsync(VerifyPath, form, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Status(logger, (int)response.StatusCode);
                return CaptchaResult.Failed($"http-{(int)response.StatusCode}");
            }

            result = await response.Content
                .ReadFromJsonAsync<VerifyResponse>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // hCaptcha being unreachable is not the visitor's fault, and it is also not a reason to
            // accept an unverified submission. Fail closed.
            Log.Unreachable(logger, exception);
            return CaptchaResult.Failed("unreachable");
        }

        if (result is { Success: true })
        {
            return CaptchaResult.Passed;
        }

        var codes = result?.ErrorCodes ?? [];
        Log.Rejected(logger, codes.Length > 0 ? string.Join(", ", codes) : "no error codes returned");

        return CaptchaResult.Failed(codes);
    }

    /// <summary>
    /// hCaptcha answers with a plain success flag. A confidence score is an Enterprise feature, so
    /// nothing here has a score threshold to tune.
    /// </summary>
    private sealed record VerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; init; }
    }

    /// <summary>
    /// Source-generated logging. CA1848 makes the alternative a build error, and these are on the
    /// path of every submission.
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "hCaptcha verification returned HTTP {StatusCode}.")]
        public static partial void Status(ILogger logger, int statusCode);

        [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Could not reach hCaptcha verification.")]
        public static partial void Unreachable(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "hCaptcha rejected a token: {Errors}")]
        public static partial void Rejected(ILogger logger, string errors);
    }
}

/// <summary>Accepts every token without asking hCaptcha.</summary>
/// <remarks>
/// The offline path. Registered only when configuration asks for it <b>and</b> the consuming
/// application says its environment allows it, so a setting copied onto a production box does not
/// switch verification off on its own.
/// </remarks>
/// <param name="logger">Where the warning goes on every call.</param>
public sealed partial class BypassCaptchaVerifier(ILogger<BypassCaptchaVerifier> logger) : ICaptchaVerifier
{
    /// <inheritdoc />
    public Task<CaptchaResult> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        // Warned on every call rather than once at startup. A log somebody scrolls past at boot is
        // a log nobody reads; this one keeps saying so for as long as it is switched on.
        Log.Bypassed(logger);
        return Task.FromResult(CaptchaResult.Passed);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Captcha verification bypassed: HCaptcha:Bypass is set and the environment allows it.")]
        public static partial void Bypassed(ILogger logger);
    }
}
