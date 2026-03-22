using CaptionGenerator.Domain.Entities;

namespace CaptionGenerator.Application.Abstractions;

public interface IPostsRepository
{
    Task AddAsync(Post post, CancellationToken cancellationToken = default);
    Task<Post?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Post post, CancellationToken cancellationToken = default);
    Task DeleteAsync(Post post, CancellationToken cancellationToken = default);
    IQueryable<Post> Query();
}
