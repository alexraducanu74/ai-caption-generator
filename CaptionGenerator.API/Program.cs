using CaptionGenerator.API.Middleware;
using CaptionGenerator.Application.Posts.Commands;
using CaptionGenerator.Application.Posts.Validators;
using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Application.Behaviors;
using CaptionGenerator.Infrastructure.Persistence;
using CaptionGenerator.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Add Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.MigrationsAssembly("CaptionGenerator.Infrastructure")));

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterPostCommand).Assembly);
});

// FluentValidation
builder.Services.AddScoped<IValidator<RegisterPostCommand>, RegisterPostValidator>();

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
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.MapControllers();

// Database Migration - Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Starting database initialization...");

        // Create database if it doesn't exist
        await dbContext.Database.EnsureCreatedAsync();
        logger.LogInformation("✅ Database schema ensured from model.");

        // Try to apply any pending migrations (optional)
        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingMigrations.Any())
        {
            logger.LogInformation($"Applying {pendingMigrations.Count()} pending migrations...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("✅ Migrations applied.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to initialize database.");
        throw;
    }
}

app.Run();
