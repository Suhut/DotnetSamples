namespace TracesAndLogs;

using Microsoft.EntityFrameworkCore;

public class SqlServerDbContext : DbContext
{
    public SqlServerDbContext(DbContextOptions<SqlServerDbContext> options)
        : base(options)
    {
    }

    public DbSet<SqlServerMyEntity> MyEntities { get; set; }
}

public class SqlServerMyEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
}
