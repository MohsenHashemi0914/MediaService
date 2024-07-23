using Microsoft.EntityFrameworkCore;

namespace Media.Infrastructure.Context;

public sealed class MediaDbContext : DbContext
{
    public MediaDbContext(DbContextOptions<MediaDbContext> options) : base(options) { }

    public DbSet<UrlToken> Tokens { get; set; }
}

public sealed class UrlToken
{
    public Guid Id { get; set; }
    public required string BucketName { get; set; }
    public required string ObjectName { get; set; }
    public required string ContentType { get; set; }
}