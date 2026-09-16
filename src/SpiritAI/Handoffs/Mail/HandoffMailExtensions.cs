using Resend;

namespace SpiritAI.Handoffs.Mail;

/// <summary>Registers the mailer a staff reply goes out through.</summary>
public static class HandoffMailExtensions
{
    /// <summary>
    /// Adds <see cref="IHandoffMailer"/>, bound from <see cref="HandoffMailOptions.SectionName"/>:
    /// Resend when mail is on, and a mailer that drops every reply when it is off. With mail on,
    /// the host refuses to start until the sender and the API key are set. Add it after
    /// <c>AddHandoffs</c>.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where the section is read from.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddHandoffMail(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(HandoffMailOptions.SectionName);

        services.AddOptions<HandoffMailOptions>()
            .Bind(section)
            .Validate(o => o.IsUsable(out _), FailureMessage(section))
            .ValidateOnStart();

        // Which mailer to register is decided here, from the bound section, the way Program.cs
        // reads PublicChatOptions: the choice is a registration, and a registration cannot wait
        // for the options to resolve.
        var settings = section.Get<HandoffMailOptions>() ?? new HandoffMailOptions();

        if (!settings.Enabled)
        {
            services.AddSingleton<IHandoffMailer, SilentHandoffMailer>();

            return services;
        }

        services.AddResend(resend => resend.ApiToken = settings.ApiKey);

        services.AddScoped<IHandoffMailer, ResendHandoffMailer>();

        return services;
    }

    /// <summary>Reads the problem once, at registration, so the start-up failure names it.</summary>
    private static string FailureMessage(IConfiguration section)
    {
        var options = new HandoffMailOptions();
        section.Bind(options);
        options.IsUsable(out var problem);

        return problem;
    }
}
