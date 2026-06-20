using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using DARAK.Api.Authentication;
using DARAK.Api.Data;
using DARAK.Api.Extensions;
using DARAK.Api.Helpers;
using DARAK.Api.Identity;
using DARAK.Api.Middleware;
using DARAK.Api.Services.Notifications;
using DARAK.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((_, _, loggerConfiguration) =>
    {
        loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/darak-api-.log", rollingInterval: RollingInterval.Day);
    });

    StartupSecurityValidator.Validate(builder.Configuration, builder.Environment);

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

    var jwtOptions = GetJwtOptions(builder.Configuration);

    builder.Services.Configure<JwtOptions>(
        builder.Configuration.GetSection(JwtOptions.SectionName));

    builder.Services.Configure<NotificationOptions>(
        builder.Configuration.GetSection(NotificationOptions.SectionName));

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services
        .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddFixedWindowLimiter(RateLimitPolicyNames.AuthRegister, limiterOptions =>
        {
            limiterOptions.PermitLimit = 3;
            limiterOptions.Window = TimeSpan.FromMinutes(10);
            limiterOptions.QueueLimit = 0;
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        options.AddFixedWindowLimiter(RateLimitPolicyNames.AuthLogin, limiterOptions =>
        {
            limiterOptions.PermitLimit = 5;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        options.AddFixedWindowLimiter(RateLimitPolicyNames.AuthRefresh, limiterOptions =>
        {
            limiterOptions.PermitLimit = 10;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
    });
    builder.Services.AddDarakApplicationServices();

    builder.Services.AddControllers();
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => string.IsNullOrWhiteSpace(entry.Key) ? "Request" : entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The request value is invalid."
                            : error.ErrorMessage)
                        .ToArray());

            var response = ApiErrorResponseFactory.Create(
                context.HttpContext,
                "Validation failed.",
                errors);

            return new BadRequestObjectResult(response);
        };
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "DARAK API",
            Version = "v1"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter a JWT access token using the Bearer scheme."
        });
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("sqlserver");

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "DARAK API v1");
        });
    }

    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<MaintenanceModeMiddleware>();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
        .AllowAnonymous();
    app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }))
        .AllowAnonymous();
    app.MapHealthChecks("/health/ready")
        .AllowAnonymous();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "DARAK API terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

static JwtOptions GetJwtOptions(IConfiguration configuration)
{
    var jwtOptions = configuration
        .GetSection(JwtOptions.SectionName)
        .Get<JwtOptions>()
        ?? throw new InvalidOperationException("JWT configuration is missing.");

    if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
    {
        throw new InvalidOperationException("JWT issuer is not configured.");
    }

    if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
    {
        throw new InvalidOperationException("JWT audience is not configured.");
    }

    if (Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
    {
        throw new InvalidOperationException("JWT secret key must be at least 32 bytes.");
    }

    if (jwtOptions.AccessTokenMinutes <= 0)
    {
        throw new InvalidOperationException("JWT access token lifetime must be positive.");
    }

    if (jwtOptions.RefreshTokenDays <= 0)
    {
        throw new InvalidOperationException("JWT refresh token lifetime must be positive.");
    }

    return jwtOptions;
}

public partial class Program;
