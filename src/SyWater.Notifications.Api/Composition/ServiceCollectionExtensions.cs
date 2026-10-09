using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Api.Background;
using SyWater.Notifications.Api.Messaging;
using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.UseCases;
using SyWater.Notifications.Domain.Emails;
using SyWater.Notifications.Infrastructure.Email;
using SyWater.Notifications.Infrastructure.Http;
using SyWater.Notifications.Infrastructure.Persistence;

namespace SyWater.Notifications.Api.Composition;

/// <summary>Composition root: the ONLY place that knows which adapter implements each port.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The e-mail channel is on only when an SMTP host is configured (Mailpit in development).</summary>
    public static bool EmailEnabled(IConfiguration config) => !string.IsNullOrWhiteSpace(config[$"{SmtpOptions.Section}:Host"]);

    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new EmailSettings(EmailEnabled(config), config["App:PublicUrl"] ?? "http://localhost:8081"));
        services.AddSingleton(EmailRetryPolicy.Default);
        services.AddSingleton(new OfflineSettings(TimeSpan.FromMinutes(config.GetValue("Devices:OfflineAfterMinutes", 10))));
        services.AddScoped<IListMyNotificationsUseCase, ListMyNotificationsUseCase>();
        services.AddScoped<IGetUnreadCountUseCase, GetUnreadCountUseCase>();
        services.AddScoped<IMarkNotificationReadUseCase, MarkNotificationReadUseCase>();
        services.AddScoped<IMarkAllNotificationsReadUseCase, MarkAllNotificationsReadUseCase>();
        services.AddScoped<IGetMyPreferencesUseCase, GetMyPreferencesUseCase>();
        services.AddScoped<IUpdateMyPreferencesUseCase, UpdateMyPreferencesUseCase>();
        services.AddScoped<IRegisterPushTokenUseCase, RegisterPushTokenUseCase>();
        services.AddScoped<IUnregisterPushTokenUseCase, UnregisterPushTokenUseCase>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<IDeviceEventsHandler, DeviceEventsHandler>();
        services.AddScoped<ISendDueEmailsUseCase, SendDueEmailsUseCase>();
        services.AddScoped<IDetectOfflineDevicesUseCase, DetectOfflineDevicesUseCase>();
        return services;
    }

    /// <summary>RabbitMQ consumer (empty RabbitMq:Host = not started), the e-mail worker (only if Smtp:Host is set) and the offline-device detector.</summary>
    public static IServiceCollection AddNotificationsBackground(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RabbitMqOptions>(config.GetSection(RabbitMqOptions.Section));
        if (!string.IsNullOrWhiteSpace(config[$"{RabbitMqOptions.Section}:Host"]))
            services.AddHostedService<DeviceEventsConsumer>();
        if (EmailEnabled(config))
            services.AddHostedService<EmailDeliveryWorker>();
        services.AddHostedService<OfflineDeviceWorker>();
        return services;
    }

    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Notification");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Notification' (user-secrets).");

        services.AddDbContext<NotificationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<IPlaceDeviceRepository, EfPlaceDeviceRepository>();
        services.AddScoped<INotificationPreferenceRepository, EfNotificationPreferenceRepository>();
        services.AddScoped<IEmailOutboxRepository, EfEmailOutboxRepository>();
        services.AddScoped<IPushTokenRepository, EfPushTokenRepository>();

        if (EmailEnabled(config))
        {
            var iamUrl = Required(config, "Services:IamBaseUrl");
            var internalKey = config["Internal:ApiKey"];
            if (string.IsNullOrWhiteSpace(internalKey) || internalKey.Length < 24)
                throw new InvalidOperationException(
                    "Missing 'Internal:ApiKey' (user-secrets, at least 24 chars). It must be the SAME value as in the other services.");

            services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.Section));
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
            services.AddHttpClient(HttpUserContactDirectory.ServiceName, http => Configure(http, iamUrl))
                .AddTypedClient<IUserContactDirectory>((http, _) => new HttpUserContactDirectory(http, internalKey));
        }
        return services;
    }

    private static string Required(IConfiguration config, string key) =>
        string.IsNullOrWhiteSpace(config[key]) ? throw new InvalidOperationException($"Missing '{key}'.") : config[key]!;

    private static void Configure(HttpClient client, string baseUrl)
    {
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(5);
    }
}
