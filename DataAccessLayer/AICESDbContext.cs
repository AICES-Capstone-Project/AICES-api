using Data.Entities;
using Data.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace DataAccessLayer
{
    public class AICESDbContext : DbContext
    {
        public AICESDbContext(DbContextOptions<AICESDbContext> options) : base(options)
        {
        }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<Profile> Profiles { get; set; }
        public virtual DbSet<LoginProvider> LoginProviders { get; set; }
        public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Connection string not found or invalid in appsettings.json.");
                }
                optionsBuilder.UseSqlServer(connectionString);
            }
        }

        private string GetConnectionString()
        {
            try
            {
                
                DotNetEnv.Env.Load();

                return Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTIONSTRING") ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading connection string: {ex.Message}");
                return string.Empty;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<Profile>(p => p.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<User>()
                .HasMany(u => u.LoginProviders)
                .WithOne(lp => lp.User)
                .HasForeignKey(lp => lp.UserId);

            modelBuilder.Entity<User>()
                .HasMany(u => u.RefreshTokens)
                .WithOne(rt => rt.User)
                .HasForeignKey(rt => rt.UserId);

            // Ngăn cascade delete cho tất cả foreign keys
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
                }
            }

            // Seed dữ liệu cho Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "Manager" },
                new Role { RoleId = 3, RoleName = "Recruiter" },
                new Role { RoleId = 4, RoleName = "Candidate" }
            );

            // Configure enum for AuthProvider to store as string
            modelBuilder.Entity<LoginProvider>()
                .Property(lp => lp.AuthProvider)
                .HasConversion<string>()
                .HasMaxLength(50);

            base.OnModelCreating(modelBuilder);
        }
    }
}