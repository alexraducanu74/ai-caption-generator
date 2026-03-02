using CaptionGenerator.Application.Abstractions;
using CaptionGenerator.Domain.Entities;
using CaptionGenerator.Infrastructure.Persistence;

namespace CaptionGenerator.Infrastructure.Repositories;

public class PostsRepository : IPostsRepository
{
    private readonly AppDbContext _context;

    public PostsRepository(AppDbContext context)
    {
        _context = context;
    }
    public async Task AddAsync(Post post, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(post, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Post?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Posts.FindAsync(new object[] { id }, cancellationToken).AsTask();
    }

    public async Task DeleteAsync(Post post, CancellationToken cancellationToken = default)
    {
        _context.Posts.Remove(post);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public IQueryable<Post> Query()
    {
        return _context.Posts.AsQueryable();
    }
}
