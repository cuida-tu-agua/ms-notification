using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using SyWater.Notifications.Api.Composition;
using SyWater.Notifications.Api.Errors;
using SyWater.Notifications.Api.Security;
using SyWater.Notifications.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── Inbound adapters: HTTP and RabbitMQ ────────────────────────────────────────────────
// Enums travel as "VALVE_CHANGED", "CRITICAL"… (same style as the rest of the API)
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();

// ── Hexagon + adapters ──────────────────────────────────────────────────
builder.Services.AddNotificationsApplication(builder.Configuration);
builder.Services.AddNotificationsInfrastructure(builder.Configuration);
builder.Services.AddNotificationsBackground(builder.Configuration);

// ── Security: every endpoint requires a valid ms-iam token unless marked AllowAnonymous ──
builder.Services.AddIamJwtAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks().AddDbContextCheck<NotificationDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous(); // GET /openapi/v1.json
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>Lets the integration tests start the app (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
