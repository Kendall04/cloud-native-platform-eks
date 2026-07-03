using System.Security.Claims;
using System.Text;
using AuthService.Application.Common.Authorization;
using AuthService.Domain.Constants;
using AuthService.Domain.Entities;
using AuthService.Infrastructure;
using AuthService.Infrastructure.Configuration;
using AuthService.Infrastructure.Seeding;
using AuthService.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var swaggerEnabled = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled");
const string swaggerRoutePrefix = "auth/swagger";

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "auth-service API",
        Version = "v1",
        Description = "Authentication and authorization service for the logistics platform."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT",
        Description = "Provide the bearer token issued by auth-service."
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

var issuer = builder.Configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)}"]
    ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var audience = builder.Configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)}"]
    ?? throw new InvalidOperationException("Jwt:Audience is required.");
var secret = builder.Configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.Secret)}"]
    ?? throw new InvalidOperationException("Jwt:Secret is required.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var userIdValue = principal?.FindFirstValue(ClaimNames.UserId)
                    ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Access token does not contain a valid user identifier.");
                    return;
                }

                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.Id == userId, context.HttpContext.RequestAborted);

                if (user is null || !user.IsActive)
                {
                    context.Fail("User account is disabled or no longer exists.");
                }
            }
        };
    });

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
        options.SwaggerEndpoint("v1/swagger.json", "auth-service API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
