using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Api.Messaging;
using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.UseCases;
using SyWater.Notifications.Infrastructure.Persistence;

namespace SyWater.Notifications.Api.Composition;

/// <summary>Composition root: the ONLY place that knows which adapter implements each port.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IListMyNotificationsUseCase, ListMyNotificationsUseCase>();
        services.AddScoped<IGetUnreadCountUseCase, GetUnreadCountUseCase>();
        services.AddScoped<IMarkNotificationReadUseCase, MarkNotificationReadUseCase>();
        services.AddScoped<IMarkAllNotificationsReadUseCase, MarkAllNotificationsReadUseCase>();
        services.AddScoped<IGetMyPreferencesUseCase, GetMyPreferencesUseCase>();
        services.AddScoped<IUpdateMyPreferencesUseCase, UpdateMyPreferencesUseCase>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<IDeviceEventsHandler, DeviceEventsHandler>();
        return services;
    }

    /// <summary>RabbitMQ consumer (empty RabbitMq:Host = not started: the API still serves the stored inbox).</summary>
    public static IServiceCollection AddNotificationsBackground(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RabbitMqOptions>(config.GetSection(RabbitMqOptions.Section));
        if (!string.IsNullOrWhiteSpace(config[$"{RabbitMqOptions.Section}:Host"]))
            services.AddHostedService<DeviceEventsConsumer>();
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
        return services;
    }
}
