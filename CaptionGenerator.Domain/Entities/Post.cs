﻿using CaptionGenerator.Domain.Enums;

namespace CaptionGenerator.Domain.Entities;

public class Post
{
    private Post()
    {

    }

    public static Post Create(string caption, SocialPlatform platform)
    {
        if (string.IsNullOrEmpty(caption))
        {
            throw new ArgumentNullException("caption");
        }

        return new Post
        {
            Id = Guid.NewGuid(),
            Caption = caption,
            Platform = platform,
            Status = PostStatus.Scheduled
        };
    }
    public Guid Id { get; private set; }
    public string Caption { get; private set; }
    public SocialPlatform Platform { get; private set; }
    public PostStatus Status { get; private set; }

    public void MarkAsPublished()
    {
        Status = PostStatus.Published;
    }
}
