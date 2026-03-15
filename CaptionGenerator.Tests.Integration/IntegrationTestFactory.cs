using CaptionGenerator.API.Services;
using CaptionGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace CaptionGenerator.Tests.Integration;

public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .Build();

        await _container.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CaptionGeneration:BaseUrl"] = string.Empty,
                ["CaptionGeneration:GeneratePath"] = "/generate-caption"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_container!.GetConnectionString());
            });

            services.RemoveAll<ICaptionGenerationClient>();
            services.AddSingleton<ICaptionGenerationClient, StubCaptionGenerationClient>();

            // Apply migrations after services are configured
            var serviceProvider = services.BuildServiceProvider();
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.Migrate();
            }
        });
    }

    private sealed class StubCaptionGenerationClient : ICaptionGenerationClient
    {
        public Task<(bool Success, string? Caption, string? ErrorMessage)> GenerateCaptionAsync(string imageUrl, CancellationToken cancellationToken)
        {
            var suffix = Path.GetFileNameWithoutExtension(imageUrl);
            var caption = string.IsNullOrWhiteSpace(suffix)
                ? "Generated test caption"
                : $"Generated test caption for {suffix}";

            return Task.FromResult<(bool Success, string? Caption, string? ErrorMessage)>((true, caption[..Math.Min(100, caption.Length)], null));
        }

        public Task<(bool Success, string? Caption, string? ErrorMessage)> GenerateCaptionFromBytesAsync(byte[] imageBytes, string fileName, CancellationToken cancellationToken)
        {
            var suffix = Path.GetFileNameWithoutExtension(fileName);
            var caption = string.IsNullOrWhiteSpace(suffix)
                ? "Generated test caption"
                : $"Generated test caption for {suffix}";

            return Task.FromResult<(bool Success, string? Caption, string? ErrorMessage)>((true, caption[..Math.Min(100, caption.Length)], null));
        }
    }
}
