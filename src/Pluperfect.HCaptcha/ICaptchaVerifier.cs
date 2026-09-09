namespace Pluperfect.HCaptcha;

/// <summary>
/// How an application decides a submission came from a person.
/// </summary>
/// <remarks>
/// One method, deliberately. Which verifier answers is a deployment decision expressed in
/// configuration, not a choice the call site makes. That is what lets the same code path run
/// offline, where every token passes, and in production against hCaptcha.
///
/// Nothing here throws for a failed challenge, because a failed challenge is an ordinary outcome
/// rather than an error. A verifier that cannot reach hCaptcha reports failure too: an
/// unverifiable submission is not a verified one.
/// </remarks>
public interface ICaptchaVerifier
{
    /// <summary>Verifies one token.</summary>
    /// <param name="token">The token the browser widget produced.</param>
    /// <param name="cancellationToken">Cancels the verification attempt.</param>
    /// <returns>Whether the token passed, and the provider error codes when it did not.</returns>
    Task<CaptchaResult> VerifyAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>The outcome of verifying one token.</summary>
/// <remarks>
/// Richer than a bool because the error codes name configuration faults as readily as failed
/// challenges: an invalid secret reads as <c>invalid-input-secret</c>. Losing that distinction
/// turns a misconfigured box into "visitors keep failing the captcha", which is a much longer
/// afternoon.
/// </remarks>
/// <param name="Success">Whether the token was accepted.</param>
/// <param name="ErrorCodes">Provider error codes. Empty when <paramref name="Success"/> is true.</param>
public sealed record CaptchaResult(bool Success, IReadOnlyList<string> ErrorCodes)
{
    /// <summary>A token that was accepted.</summary>
    public static CaptchaResult Passed { get; } = new(true, []);

    /// <summary>A token that was not accepted.</summary>
    /// <param name="errorCodes">Why, in the provider's own vocabulary.</param>
    /// <returns>A failed result.</returns>
    public static CaptchaResult Failed(params string[] errorCodes) => new(false, errorCodes);
}
