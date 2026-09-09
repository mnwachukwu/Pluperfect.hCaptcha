using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pluperfect.HCaptcha;

/// <summary>Registers this library with an application's container.</summary>
public static class ServiceCollectionExtensions
{
    private const string HCaptchaBaseAddress = "https://api.hcaptcha.com/";

    /// <summary>
    /// Adds <see cref="ICaptchaVerifier"/>, selecting a verifier from configuration.
    /// </summary>
    /// <remarks>
    /// The selection rule: the bypass wins when <c>HCaptcha:Bypass</c> is set <b>and</b>
    /// <paramref name="allowBypass"/> is true, and every token is accepted. Otherwise
    /// <c>HCaptcha:SecretKey</c> is required and tokens go to hCaptcha. A configuration with
    /// neither fails at startup, where systemd reports it, rather than on the first visitor.
    ///
    /// <paramref name="allowBypass"/> is a parameter rather than an environment check inside this
    /// library, because a library that reads the host environment is a library that behaves
    /// differently depending on who hosts it. The caller states the policy; this decides nothing
    /// about where it is running.
    /// </remarks>
    /// <param name="services">The container.</param>
    /// <param name="configuration">Configuration carrying the <c>HCaptcha</c> section.</param>
    /// <param name="allowBypass">
    /// Whether the environment permits the bypass at all. Pass <c>builder.Environment.IsDevelopment()</c>.
    /// Defaults to false, so forgetting it fails closed.
    /// </param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddPluperfectHCaptcha(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowBypass = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(PluperfectHCaptchaOptions.Section);

        services.AddOptions<PluperfectHCaptchaOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var options = section.Get<PluperfectHCaptchaOptions>() ?? new PluperfectHCaptchaOptions();

        if (options.Bypass && allowBypass)
        {
            services.TryAddSingleton<ICaptchaVerifier, BypassCaptchaVerifier>();
            return services;
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            // Named precisely, including the environment-variable spelling, because this message is
            // what somebody reads at three in the morning when a deploy will not start.
            throw new InvalidOperationException(
                "HCaptcha:SecretKey is not configured. Set it in user secrets for development, or "
                + "as the HCaptcha__SecretKey environment variable in production. Set "
                + "HCaptcha:Bypass instead to accept every token, which is honored only where the "
                + "application allows it.");
        }

        var secretKey = options.SecretKey;
        var timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        services.AddHttpClient(HCaptchaVerifier.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(HCaptchaBaseAddress);
            client.Timeout = timeout;
        });

        services.TryAddSingleton<ICaptchaVerifier>(provider => new HCaptchaVerifier(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(HCaptchaVerifier.HttpClientName),
            secretKey,
            provider.GetRequiredService<ILogger<HCaptchaVerifier>>()));

        return services;
    }
}
