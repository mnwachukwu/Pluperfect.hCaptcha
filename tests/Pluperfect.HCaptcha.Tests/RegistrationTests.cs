using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Pluperfect.HCaptcha;
using Shouldly;

namespace Pluperfect.HCaptcha.Tests;

/// <summary>
/// The registration rule is the whole safety story of this library, so it is what gets tested.
/// No network and no credentials: every case here is decided from configuration alone.
/// </summary>
[TestFixture]
public sealed class RegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings, bool allowBypass)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPluperfectHCaptcha(configuration, allowBypass);

        return services.BuildServiceProvider();
    }

    [Test]
    public void Bypass_is_used_when_configuration_asks_and_the_environment_allows()
    {
        var provider = Build(new() { ["HCaptcha:Bypass"] = "true" }, allowBypass: true);

        provider.GetRequiredService<ICaptchaVerifier>().ShouldBeOfType<BypassCaptchaVerifier>();
    }

    [Test]
    public void Bypass_alone_is_not_enough()
    {
        // The whole point of the second gate: a setting copied onto a box that does not allow it
        // must not switch verification off. With no secret to fall back to, this fails loudly.
        var act = () => Build(new() { ["HCaptcha:Bypass"] = "true" }, allowBypass: false);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("HCaptcha:SecretKey");
    }

    [Test]
    public void A_secret_key_selects_the_real_verifier()
    {
        var provider = Build(new() { ["HCaptcha:SecretKey"] = "not-a-real-secret" }, allowBypass: false);

        provider.GetRequiredService<ICaptchaVerifier>().ShouldBeOfType<HCaptchaVerifier>();
    }

    [Test]
    public void An_environment_that_allows_bypass_still_verifies_when_bypass_is_off()
    {
        var provider = Build(
            new() { ["HCaptcha:SecretKey"] = "not-a-real-secret", ["HCaptcha:Bypass"] = "false" },
            allowBypass: true);

        provider.GetRequiredService<ICaptchaVerifier>().ShouldBeOfType<HCaptchaVerifier>();
    }

    [Test]
    public void No_secret_and_no_bypass_fails_at_registration()
    {
        var act = () => Build([], allowBypass: false);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("HCaptcha__SecretKey");
    }
}

/// <summary>The bypass verifier is trivial, and its behavior is load bearing.</summary>
[TestFixture]
public sealed class BypassCaptchaVerifierTests
{
    [Test]
    public async Task Accepts_even_an_empty_token()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<BypassCaptchaVerifier>();

        var verifier = services.BuildServiceProvider().GetRequiredService<BypassCaptchaVerifier>();

        var result = await verifier.VerifyAsync(string.Empty, TestContext.CurrentContext.CancellationToken);

        result.Success.ShouldBeTrue();
        result.ErrorCodes.ShouldBeEmpty();
    }
}
