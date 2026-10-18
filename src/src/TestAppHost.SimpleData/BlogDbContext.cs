using Microsoft.EntityFrameworkCore;

namespace Aspire.Hosting.AspireC4.TestAppHost.SimpleData;

/// <summary>
/// The simplest shape AspireC4 supports: one context, convention-only configuration, no fluent API.
/// </summary>
/// <remarks>
/// Everything here is recovered by convention — table names from the <see cref="DbSet{TEntity}"/>
/// property names, primary keys from the <c>Id</c> naming rule, and the foreign key from the
/// <c>Blog</c> navigation plus its matching <c>BlogId</c> property.
/// </remarks>
public sealed class BlogDbContext(DbContextOptions<BlogDbContext> options) : DbContext(options)
{
	public DbSet<Blog> Blogs => Set<Blog>();

	public DbSet<Post> Posts => Set<Post>();
}

/// <summary>A blog, owning many posts.</summary>
public sealed class Blog
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	public ICollection<Post> Posts { get; } = [];
}

/// <summary>A post belonging to exactly one blog.</summary>
public sealed class Post
{
	public int Id { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public bool IsPublished { get; set; }

	public DateTimeOffset? PublishedAt { get; set; }

	public int BlogId { get; set; }

	public Blog? Blog { get; set; }
}
