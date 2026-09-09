using System.ComponentModel.DataAnnotations;

namespace Pluperfect.HCaptcha;

/// <summary>Configuration for hCaptcha verification, bound to the <c>HCaptcha</c> section.</summary>
public sealed class PluperfectHCaptchaOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string Section = "HCaptcha";

    /// <summary>
    /// The secret half of the key pair. Never the site key, which is public and ships in the
    /// client bundle.
    /// </summary>
    /// <remarks>
    /// Required unless <see cref="Bypass"/> is in force. Not marked <c>[Required]</c> here because
    /// the offline path needs no secret; the registration checks it where it matters and names the
    /// key when it is missing.
    /// </remarks>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Accept every token without asking hCaptcha.
    /// </summary>
    /// <remarks>
    /// The offline path, and the counterpart of Pluperfect.Mail's pickup directory: a contact form
    /// stays fully exercisable with no network and no credentials.
    ///
    /// Honored only when the consuming application also passes <c>allowBypass</c> at registration,
    /// which it derives from its host environment. Never gate a bypass on a setting alone: a
    /// setting can be copied onto a production box by accident, and an environment cannot be as
    /// easily.
    /// </remarks>
    public bool Bypass { get; set; }

    /// <summary>How long to wait on hCaptcha. Defaults to 10.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;
}
