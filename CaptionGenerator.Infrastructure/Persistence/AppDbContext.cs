﻿using CaptionGenerator.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CaptionGenerator.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Post>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Caption).IsRequired().HasMaxLength(100);
            e.Property(p => p.Platform).IsRequired().HasConversion<int>();
            e.Property(p => p.ImageUrl).IsRequired();
            e.Property(p => p.CreatedAtUtc).IsRequired();
            e.Property(p => p.Status).IsRequired().HasConversion<int>();
        });
    }
}
