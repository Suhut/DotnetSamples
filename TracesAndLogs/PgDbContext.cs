namespace TracesAndLogs;

using Microsoft.EntityFrameworkCore;

public class PgDbContext : DbContext
{
    public PgDbContext(DbContextOptions<PgDbContext> options)
        : base(options)
    {
    }

    public DbSet<PgMyEntity> MyEntities { get; set; }
}

public class PgMyEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
}
