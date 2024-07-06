namespace OpenTelemetryTraces;

using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MyEntity> MyEntities { get; set; }
}

public class MyEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
}
