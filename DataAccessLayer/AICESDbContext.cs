﻿using Data.Entities;
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

        public virtual DbSet<BannerConfig> BannerConfigs { get; set; }
        // User & Auth Related
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<Profile> Profiles { get; set; }
        public virtual DbSet<LoginProvider> LoginProviders { get; set; }
        public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
        
        // Company Related
        public virtual DbSet<Company> Companies { get; set; }
        public virtual DbSet<CompanyUser> CompanyUsers { get; set; }
        public virtual DbSet<Subscription> Subscriptions { get; set; }
        public virtual DbSet<CompanySubscription> CompanySubscriptions { get; set; }
        public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<CompanyDocument> CompanyDocuments { get; set; }
        
        // Job Related
        public virtual DbSet<Job> Jobs { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<EmploymentType> EmploymentTypes { get; set; }
        public virtual DbSet<JobCategory> JobCategories { get; set; }
        public virtual DbSet<JobEmploymentType> JobEmploymentTypes { get; set; }
        public virtual DbSet<Criteria> Criterias { get; set; }
        
        // Resume Screening
        public virtual DbSet<ParsedResumes> ParsedResumes { get; set; }
        public virtual DbSet<ParsedCandidates> ParsedCandidates { get; set; }
        public virtual DbSet<AIScores> AIScores { get; set; }
        public virtual DbSet<AIScoreDetail> AIScoreDetails { get; set; }
        public virtual DbSet<RankingResults> RankingResults { get; set; }
        
        // Communication & Reporting
        public virtual DbSet<Notification> Notifications { get; set; }
        public virtual DbSet<Reports> Reports { get; set; }

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
            // ===== USER & AUTH RELATIONSHIPS =====

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

            // ===== COMPANY RELATIONSHIPS =====
            
            // User - CompanyUser (one-to-one)
            modelBuilder.Entity<User>()
                .HasOne(u => u.CompanyUser)
                .WithOne(cu => cu.User)
                .HasForeignKey<CompanyUser>(cu => cu.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Company>()
                .HasMany(c => c.CompanyUsers)
                .WithOne(cu => cu.Company)
                .HasForeignKey(cu => cu.CompanyId)
                .IsRequired(false)  // ✅ CompanyId is now optional
                .OnDelete(DeleteBehavior.NoAction);

            // Company - CompanySubscription
            modelBuilder.Entity<Company>()
                .HasMany(c => c.CompanySubscriptions)
                .WithOne(cs => cs.Company)
                .HasForeignKey(cs => cs.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Subscription - CompanySubscription
            modelBuilder.Entity<Subscription>()
                .HasMany(s => s.CompanySubscriptions)
                .WithOne(cs => cs.Subscription)
                .HasForeignKey(cs => cs.SubcriptionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Company - Payment
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Payments)
                .WithOne(p => p.Company)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Company - CompanyDocuments
            modelBuilder.Entity<Company>()
                .HasMany(c => c.CompanyDocuments)
                .WithOne(cd => cd.Company)
                .HasForeignKey(cd => cd.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== JOB RELATIONSHIPS =====
            
            // Job - Category (Many-to-Many through JobCategory)
            modelBuilder.Entity<JobCategory>()
                .HasOne(jc => jc.Job)
                .WithMany(j => j.JobCategories)
                .HasForeignKey(jc => jc.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<JobCategory>()
                .HasOne(jc => jc.Category)
                .WithMany(c => c.JobCategories)
                .HasForeignKey(jc => jc.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - EmploymentType (Many-to-Many through JobEmploymentType)
            modelBuilder.Entity<JobEmploymentType>()
                .HasOne(jet => jet.Job)
                .WithMany(j => j.JobEmploymentTypes)
                .HasForeignKey(jet => jet.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<JobEmploymentType>()
                .HasOne(jet => jet.EmploymentType)
                .WithMany(et => et.JobEmploymentTypes)
                .HasForeignKey(jet => jet.EmployTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            // CompanyUser - Jobs
            modelBuilder.Entity<CompanyUser>()
                .HasMany(cu => cu.Jobs)
                .WithOne(j => j.CompanyUser)
                .HasForeignKey(j => j.ComUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Company - Jobs
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Jobs)
                .WithOne(j => j.Company)
                .HasForeignKey(j => j.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - Criteria
            modelBuilder.Entity<Job>()
                .HasMany(j => j.Criteria)
                .WithOne(c => c.Job)
                .HasForeignKey(c => c.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== RESUME SCREENING RELATIONSHIPS =====
            
            // Company - ParsedResumes
            modelBuilder.Entity<Company>()
                .HasMany(c => c.ParsedResumes)
                .WithOne(pr => pr.Company)
                .HasForeignKey(pr => pr.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - ParsedResumes
            modelBuilder.Entity<Job>()
                .HasMany(j => j.ParsedResumes)
                .WithOne(pr => pr.Job)
                .HasForeignKey(pr => pr.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            // ParsedResumes - ParsedCandidates
            modelBuilder.Entity<ParsedResumes>()
                .HasMany(pr => pr.ParsedCandidates)
                .WithOne(pc => pc.ParsedResumes)
                .HasForeignKey(pc => pc.ResumeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - ParsedCandidates
            modelBuilder.Entity<Job>()
                .HasMany(j => j.ParsedCandidates)
                .WithOne(pc => pc.Job)
                .HasForeignKey(pc => pc.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            // AIScores - ParsedCandidates (one-to-one)
            modelBuilder.Entity<AIScores>()
                .HasOne(s => s.ParsedCandidate)
                .WithOne(pc => pc.AIScores)
                .HasForeignKey<ParsedCandidates>(pc => pc.ScoreId)
                .OnDelete(DeleteBehavior.NoAction);

            // AIScores - AIScoreDetail
            modelBuilder.Entity<AIScores>()
                .HasMany(s => s.AIScoreDetails)
                .WithOne(sd => sd.AIScores)
                .HasForeignKey(sd => sd.ScoreId)
                .OnDelete(DeleteBehavior.NoAction);

            // Criteria - AIScoreDetail
            modelBuilder.Entity<Criteria>()
                .HasMany(c => c.AIScoreDetails)
                .WithOne(sd => sd.Criteria)
                .HasForeignKey(sd => sd.CriteriaId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - RankingResults
            modelBuilder.Entity<Job>()
                .HasMany(j => j.RankingResults)
                .WithOne(rr => rr.Job)
                .HasForeignKey(rr => rr.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            // ParsedCandidates - RankingResults (one-to-one)
            modelBuilder.Entity<ParsedCandidates>()
                .HasOne(pc => pc.RankingResult)
                .WithOne(rr => rr.ParsedCandidate)
                .HasForeignKey<RankingResults>(rr => rr.CandidateId)
                .OnDelete(DeleteBehavior.NoAction);

            // AIScores - RankingResults
            modelBuilder.Entity<AIScores>()
                .HasMany(s => s.RankingResults)
                .WithOne(rr => rr.AIScores)
                .HasForeignKey(rr => rr.ScoreId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== COMMUNICATION & REPORTING RELATIONSHIPS =====
            
            // User - Notifications
            modelBuilder.Entity<User>()
                .HasMany(u => u.Notifications)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // User - Reports
            modelBuilder.Entity<User>()
                .HasMany(u => u.Reports)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - Reports
            modelBuilder.Entity<Job>()
                .HasMany(j => j.Reports)
                .WithOne(r => r.Job)
                .HasForeignKey(r => r.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== GLOBAL CASCADE DELETE PREVENTION =====
            
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
                }
            }

            // Configure enum for AuthProvider to store as string
            modelBuilder.Entity<LoginProvider>()
                .Property(lp => lp.AuthProvider)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Configure enum conversions for new enums
            modelBuilder.Entity<CompanyUser>()
                .Property(cu => cu.JoinStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Reports>()
                .Property(r => r.Type)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Reports>()
                .Property(r => r.ExportFormat)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Company>()
                .Property(c => c.CompanyStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<CompanySubscription>()
                .Property(cs => cs.SubscriptionStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Payment>()
                .Property(p => p.PaymentStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Notification>()
                .Property(n => n.Type)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Job>()
                .Property(j => j.JobStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            // ===== SEED DATA =====

            // Seed Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = 1, RoleName = "System_Admin" },
                new Role { RoleId = 2, RoleName = "System_Manager" },
                new Role { RoleId = 3, RoleName = "System_Staff" },
                new Role { RoleId = 4, RoleName = "HR_Manager" },
                new Role { RoleId = 5, RoleName = "HR_Recruiter" }
            );
            
            base.OnModelCreating(modelBuilder);
        }
    }
}
