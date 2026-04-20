using System.Text;
using System.Text.Json.Serialization;
using CaptionGenerator.API.Middleware;
using CaptionGenerator.API.Models;
using CaptionGenerator.API.Services;
using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Application.Behaviors;
using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Posts.Validators;
using CaptionGenerator.Infrastructure.Persistence;
using CaptionGenerator.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<CaptionGenerationOptions>(builder.Configuration.GetSection("CaptionGeneration"));

// Add Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.MigrationsAssembly("CaptionGenerator.Infrastructure")));

builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? throw new InvalidOperationException("Jwt settings are missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    throw new InvalidOperationException("Jwt:Key must be configured.");
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterPostCommand).Assembly);
});

// FluentValidation
builder.Services.AddScoped<IValidator<RegisterPostCommand>, RegisterPostValidator>();
builder.Services.AddScoped<IValidator<UpdatePostCommand>, UpdatePostValidator>();

// MediatR validation pipeline
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Add Repositories
builder.Services.AddScoped<IPostsRepository, PostsRepository>();

// Add OpenAPI
builder.Services.AddOpenApi();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:7080", "http://localhost:5003")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddHttpClient<ICaptionGenerationClient, PythonCaptionGenerationClient>(client =>
{
    // First LLaVA request can take longer while the model downloads/loads.
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddHostedService<ScheduledPostPublisherService>();

var app = builder.Build();

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Database Migration - Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Starting database initialization...");

        await dbContext.Database.MigrateAsync();

        // Guard against legacy dev DBs where migration history exists but Posts table is missing.
        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Posts"" (
                ""Id"" uuid NOT NULL,
                ""Caption"" character varying(280) NOT NULL,
                ""Platform"" integer NOT NULL,
                ""ImageUrl"" text NOT NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL,
                ""ScheduledForUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""Status"" integer NOT NULL,
                CONSTRAINT ""PK_Posts"" PRIMARY KEY (""Id"")
            );
        ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Posts""
            ADD COLUMN IF NOT EXISTS ""ScheduledForUtc"" timestamp with time zone NOT NULL DEFAULT NOW();
        ");

        logger.LogInformation("Database migrated successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize database.");
        throw;
    }
}

app.Run();
