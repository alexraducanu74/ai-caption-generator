using CaptionGenerator.Application.Posts.Queries;
using CaptionGenerator.Domain.Enums;
using FluentAssertions;

namespace CaptionGenerator.Tests.Unit;

public class GetPostCommandHandlerTests
{
    [Fact]
    public void GetPostQuery_WithNullStatus_ShouldNotFilter()
    {
        var query = new GetPostQuery(null);
        query.Status.Should().BeNull();
    }

    [Fact]
    public void GetPostQuery_WithPublishedStatus_ShouldSetStatus()
    {
        var query = new GetPostQuery(PostStatus.Published);
        query.Status.Should().Be(PostStatus.Published);
    }

    [Fact]
    public void GetPostQuery_WithScheduledStatus_ShouldSetStatus()
    {
        var query = new GetPostQuery(PostStatus.Scheduled);
        query.Status.Should().Be(PostStatus.Scheduled);
    }

    [Fact]
    public void GetPostQuery_ShouldCreateValidQuery()
    {
        var query = new GetPostQuery(null);
        query.Should().NotBeNull();
    }
}


