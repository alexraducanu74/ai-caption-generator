using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CaptionGenerator.API.Services;

public class ScheduledPostPublisherService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledPostPublisherService> _logger;

    public ScheduledPostPublisherService(IServiceScopeFactory scopeFactory, ILogger<ScheduledPostPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPostsRepository>();
                var nowUtc = DateTime.UtcNow;

                var duePosts = await repository.Query()
                    .Where(p => p.Status == PostStatus.Scheduled && p.ScheduledForUtc <= nowUtc)
                    .ToListAsync(stoppingToken);

                foreach (var post in duePosts)
                {
                    post.MarkAsPendingConfirmation();
                    await repository.UpdateAsync(post, stoppingToken);
                }

                if (duePosts.Count > 0)
                {
                    _logger.LogInformation("Moved {Count} scheduled post(s) to pending confirmation.", duePosts.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled post publisher iteration failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
