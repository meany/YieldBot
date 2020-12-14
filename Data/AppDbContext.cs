using dm.YLD.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace dm.YLD.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Holder> Holders { get; set; }
        public DbSet<LPHolder> LPHolders { get; set; }
        public DbSet<Price> Prices { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Stat> Stats { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<LPTransaction> LPTransactions { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Holder>()
                .HasIndex(x => x.Value);
            modelBuilder.Entity<LPHolder>()
                .HasIndex(x => x.Value);
            modelBuilder.Entity<LPHolder>()
                .HasIndex(x => x.Pair);
            modelBuilder.Entity<Price>()
                .HasIndex(x => x.Group);
            modelBuilder.Entity<Price>()
                .HasIndex(x => x.Date);
            modelBuilder.Entity<Request>()
                .HasIndex(x => x.Date);
            modelBuilder.Entity<Request>()
                .HasIndex(x => new { x.Response, x.Type });
            modelBuilder.Entity<Stat>()
                .HasIndex(x => x.Date);
            modelBuilder.Entity<Transaction>()
                .HasIndex(x => x.TimeStamp);
            modelBuilder.Entity<LPTransaction>()
                .HasIndex(x => x.TimeStamp);
            modelBuilder.Entity<LPTransaction>()
                .HasIndex(x => x.Pair);
        }
    }

    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>();
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Config.Data.json", optional: true, reloadOnChange: true)
                .AddJsonFile("Config.Data.Local.json", optional: true, reloadOnChange: true)
                .Build();

            builder.UseSqlServer(configuration.GetConnectionString("Database"));
            return new AppDbContext(builder.Options);
        }
    }
}