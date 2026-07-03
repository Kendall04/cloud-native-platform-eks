using AuthSchemeOptions = Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using TrackingService.Api.Infrastructure;
using TrackingService.Application.Common.Authorization;
using TrackingService.Domain.Constants;
using TrackingService.Infrastructure;
using TrackingService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var swaggerEnabled = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled");
const string swaggerRoutePrefix = "tracking/swagger";

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "tracking-service API",
        Version = "v1",
        Description = "Tracking timeline owner service for the logistics platform."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Bearer token presented to API Gateway. In production the service trusts only API-Gateway-verified identity headers."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services
    .AddAuthentication("GatewayJwt")
    .AddScheme<AuthSchemeOptions, GatewayJwtAuthenticationHandler>("GatewayJwt", _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        PolicyNames.RequireUserRole,
        policy => policy.RequireRole(ApplicationRoles.User, ApplicationRoles.Admin));

    options.AddPolicy(
        PolicyNames.RequireAdminRole,
        policy => policy.RequireRole(ApplicationRoles.Admin));
});

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
    return;
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (swaggerEnabled)
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = $"{swaggerRoutePrefix}/{{documentName}}/swagger.json";
    });
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = swaggerRoutePrefix;
        options.SwaggerEndpoint("v1/swagger.json", "tracking-service API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
