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
        public virtual DbSet<Transaction> Transactions { get; set; }
        public virtual DbSet<Skill> Skills { get; set; }
        public virtual DbSet<JobSkill> JobSkills { get; set; }
        
        // Job Related
        public virtual DbSet<Job> Jobs { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<EmploymentType> EmploymentTypes { get; set; }
        public virtual DbSet<Specialization> Specializations { get; set; }
        public virtual DbSet<JobEmploymentType> JobEmploymentTypes { get; set; }
        public virtual DbSet<Criteria> Criterias { get; set; }
        public virtual DbSet<Level> Levels { get; set; }
        public virtual DbSet<Language> Languages { get; set; }
        public virtual DbSet<JobLanguage> JobLanguages { get; set; }
        public virtual DbSet<Campaign> Campaigns { get; set; }
        public virtual DbSet<JobCampaign> JobCampaigns { get; set; }
        
        // Resume Screening
        public virtual DbSet<Resume> Resumes { get; set; }
        public virtual DbSet<ResumeApplication> ResumeApplications { get; set; }
        public virtual DbSet<Candidate> Candidates { get; set; }
        public virtual DbSet<ScoreDetail> ScoreDetails { get; set; }
        public virtual DbSet<Comparison> Comparisons { get; set; }
        public virtual DbSet<ApplicationComparison> ApplicationComparisons { get; set; }
        
        // Usage Tracking
        public virtual DbSet<UsageCounter> UsageCounters { get; set; }
        
        // Communication & Reporting
        public virtual DbSet<Notification> Notifications { get; set; }
        public virtual DbSet<Blog> Blogs { get; set; }
        public virtual DbSet<Invitation> Invitations { get; set; }
        public virtual DbSet<Feedback> Feedbacks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Connection string not found or invalid in appsettings.json.");
                }
                optionsBuilder.UseNpgsql(connectionString);
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
                .HasForeignKey(cs => cs.SubscriptionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Company - Payment
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Payments)
                .WithOne(p => p.Company)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // CompanySubscription - Payment
            modelBuilder.Entity<CompanySubscription>()
                .HasMany(cs => cs.Payments)
                .WithOne(p => p.CompanySubscription)
                .HasForeignKey(p => p.ComSubId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Payment - Transactions
            modelBuilder.Entity<Payment>()
                .HasMany(p => p.Transactions)
                .WithOne(t => t.Payment)
                .HasForeignKey(t => t.PaymentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - JobSkills
            modelBuilder.Entity<Job>()
                .HasMany(j => j.JobSkills)
                .WithOne(js => js.Job)
                .HasForeignKey(js => js.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            // Skill - JobSkills
            modelBuilder.Entity<Skill>()
                .HasMany(s => s.JobSkills)
                .WithOne(js => js.Skill)
                .HasForeignKey(js => js.SkillId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure composite key for JobSkill
            modelBuilder.Entity<JobSkill>()
                .HasKey(js => new { js.JobId, js.SkillId });

            // Company - CompanyDocuments
            modelBuilder.Entity<Company>()
                .HasMany(c => c.CompanyDocuments)
                .WithOne(cd => cd.Company)
                .HasForeignKey(cd => cd.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== JOB RELATIONSHIPS =====

            // Category - Specialization (One-to-Many)
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Specializations)
                .WithOne(s => s.Category)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - Specialization (Many-to-One)
            modelBuilder.Entity<Job>()
                .HasOne(j => j.Specialization)
                .WithMany(s => s.Jobs!)
                .HasForeignKey(j => j.SpecializationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - Level (Many-to-One)
            modelBuilder.Entity<Job>()
                .HasOne(j => j.Level)
                .WithMany(l => l.Jobs)
                .HasForeignKey(j => j.LevelId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - Languages (Many-to-Many through JobLanguage)
            modelBuilder.Entity<JobLanguage>()
                .HasOne(jl => jl.Job)
                .WithMany(j => j.JobLanguages)
                .HasForeignKey(jl => jl.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<JobLanguage>()
                .HasOne(jl => jl.Language)
                .WithMany(l => l.JobLanguages)
                .HasForeignKey(jl => jl.LanguageId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure composite key for JobLanguage
            modelBuilder.Entity<JobLanguage>()
                .HasKey(jl => new { jl.JobId, jl.LanguageId });

            // Company - Campaigns
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Campaigns)
                .WithOne(ca => ca.Company)
                .HasForeignKey(ca => ca.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - Campaigns (Many-to-Many through JobCampaign)
            modelBuilder.Entity<JobCampaign>()
                .HasOne(jc => jc.Job)
                .WithMany(j => j.JobCampaigns)
                .HasForeignKey(jc => jc.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<JobCampaign>()
                .HasOne(jc => jc.Campaign)
                .WithMany(ca => ca.JobCampaigns)
                .HasForeignKey(jc => jc.CampaignId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure composite key for JobCampaign
            modelBuilder.Entity<JobCampaign>()
                .HasKey(jc => new { jc.JobId, jc.CampaignId });

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

            // Configure composite key for JobEmploymentType
            modelBuilder.Entity<JobEmploymentType>()
                .HasKey(jet => new { jet.JobId, jet.EmployTypeId });

            // CompanyUser - Jobs
            modelBuilder.Entity<CompanyUser>()
                .HasMany(cu => cu.Jobs)
                .WithOne(j => j.CompanyUser)
                .HasForeignKey(j => j.ComUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // CompanyUser - Feedbacks
            modelBuilder.Entity<CompanyUser>()
                .HasMany(cu => cu.Feedbacks)
                .WithOne(f => f.CompanyUser)
                .HasForeignKey(f => f.ComUserId)
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

            // Company - Comparisons
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Comparisons)
                .WithOne(cmp => cmp.Company)
                .HasForeignKey(cmp => cmp.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - Comparisons
            modelBuilder.Entity<Job>()
                .HasMany(j => j.Comparisons)
                .WithOne(cmp => cmp.Job)
                .HasForeignKey(cmp => cmp.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            // Campaign - Comparisons
            modelBuilder.Entity<Campaign>()
                .HasMany(ca => ca.Comparisons)
                .WithOne(cmp => cmp.Campaign)
                .HasForeignKey(cmp => cmp.CampaignId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== RESUME SCREENING RELATIONSHIPS =====
            
            // Company - Resumes
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Resumes)
                .WithOne(r => r.Company)
                .HasForeignKey(r => r.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Candidate - Resumes (one-to-many)
            modelBuilder.Entity<Candidate>()
                .HasMany(c => c.Resumes)
                .WithOne(r => r.Candidate)
                .HasForeignKey(r => r.CandidateId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Resume - ResumeApplications (one-to-many)
            modelBuilder.Entity<Resume>()
                .HasMany(r => r.ResumeApplications)
                .WithOne(ra => ra.Resume)
                .HasForeignKey(ra => ra.ResumeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Campaign - ResumeApplications (one-to-many)
            modelBuilder.Entity<Campaign>()
                .HasMany(c => c.ResumeApplications)
                .WithOne(ra => ra.Campaign)
                .HasForeignKey(ra => ra.CampaignId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Job - ResumeApplications (one-to-many)
            modelBuilder.Entity<Job>()
                .HasMany(j => j.ResumeApplications)
                .WithOne(ra => ra.Job)
                .HasForeignKey(ra => ra.JobId)
                .OnDelete(DeleteBehavior.NoAction);

            // ResumeApplication - ScoreDetails (one-to-many)
            modelBuilder.Entity<ResumeApplication>()
                .HasMany(ra => ra.ScoreDetails)
                .WithOne(sd => sd.ResumeApplication)
                .HasForeignKey(sd => sd.ApplicationId)
                .OnDelete(DeleteBehavior.NoAction);

            // Criteria - ScoreDetails (one-to-many)
            modelBuilder.Entity<Criteria>()
                .HasMany(c => c.ScoreDetails)
                .WithOne(sd => sd.Criteria)
                .HasForeignKey(sd => sd.CriteriaId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure composite key for ScoreDetail
            modelBuilder.Entity<ScoreDetail>()
                .HasKey(sd => new { sd.CriteriaId, sd.ApplicationId });

            // ResumeApplication - ApplicationComparisons (one-to-many)
            modelBuilder.Entity<ResumeApplication>()
                .HasMany(ra => ra.ApplicationComparisons)
                .WithOne(ac => ac.ResumeApplication)
                .HasForeignKey(ac => ac.ApplicationId)
                .OnDelete(DeleteBehavior.NoAction);

            // Comparison - ApplicationComparisons (one-to-many)
            modelBuilder.Entity<Comparison>()
                .HasMany(c => c.ApplicationComparisons)
                .WithOne(ac => ac.Comparison)
                .HasForeignKey(ac => ac.ComparisonId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure composite key for ApplicationComparison
            modelBuilder.Entity<ApplicationComparison>()
                .HasKey(ac => new { ac.ApplicationId, ac.ComparisonId });

            // ===== COMMUNICATION & REPORTING RELATIONSHIPS =====
            
            // User - Notifications
            modelBuilder.Entity<User>()
                .HasMany(u => u.Notifications)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // User - Created Jobs/Campaigns
            modelBuilder.Entity<User>()
                .HasMany(u => u.CreatedJobs)
                .WithOne(j => j.Creator)
                .HasForeignKey(j => j.CreatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<User>()
                .HasMany(u => u.CreatedCampaigns)
                .WithOne(ca => ca.Creator)
                .HasForeignKey(ca => ca.CreatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Candidate - ResumeApplications
            modelBuilder.Entity<Candidate>()
                .HasMany(c => c.ResumeApplications)
                .WithOne(ra => ra.Candidate)
                .HasForeignKey(ra => ra.CandidateId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // User - ResumeApplications (AdjustedBy)
            modelBuilder.Entity<User>()
                .HasMany(u => u.AdjustedResumeApplications)
                .WithOne(ra => ra.AdjustedByUser)
                .HasForeignKey(ra => ra.AdjustedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // User - Blogs
            modelBuilder.Entity<User>()
                .HasMany(u => u.Blogs)
                .WithOne(b => b.User)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // User - Invitations (as Sender)
            modelBuilder.Entity<Invitation>()
                .HasOne(i => i.Sender)
                .WithMany(u => u.SentInvitations)
                .HasForeignKey(i => i.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            // User - Invitations (as Receiver)
            modelBuilder.Entity<Invitation>()
                .HasOne(i => i.Receiver)
                .WithMany(u => u.ReceivedInvitations)
                .HasForeignKey(i => i.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);

            // Company - Invitations
            modelBuilder.Entity<Invitation>()
                .HasOne(i => i.Company)
                .WithMany(c => c.Invitations)
                .HasForeignKey(i => i.CompanyId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Invitation - Notifications
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Invitation)
                .WithMany(i => i.Notifications)
                .HasForeignKey(n => n.InvitationId)
                .IsRequired(false)
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

            modelBuilder.Entity<User>()
                .Property(u => u.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Resume>()
                .Property(r => r.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<ResumeApplication>()
                .Property(ra => ra.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Campaign>()
                .Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Configure enum conversion for TransactionGateway
            modelBuilder.Entity<Transaction>()
                .Property(t => t.Gateway)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Configure enum conversion for InvitationStatus
            modelBuilder.Entity<Invitation>()
                .Property(i => i.InvitationStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Configure enum conversion for ComparisonStatus
            modelBuilder.Entity<Comparison>()
                .Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Configure enum conversion for ProcessingMode
            modelBuilder.Entity<ResumeApplication>()
                .Property(ra => ra.ProcessingMode)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<ResumeApplication>()
                .Property(ra => ra.ErrorType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired(false);

            // Configure enum conversion for DurationEnum
            modelBuilder.Entity<Subscription>()
                .Property(s => s.Duration)
                .HasConversion<string>()
                .HasMaxLength(50);

            // ===== USAGE COUNTER RELATIONSHIPS =====
            
            // Company - UsageCounters (one-to-many)
            modelBuilder.Entity<Company>()
                .HasMany(c => c.UsageCounters!)
                .WithOne(u => u.Company)
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // CompanySubscription - UsageCounters (one-to-many)
            modelBuilder.Entity<CompanySubscription>()
                .HasMany(cs => cs.UsageCounters!)
                .WithOne(u => u.CompanySubscription)
                .HasForeignKey(u => u.CompanySubscriptionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure enum conversion for UsageType
            modelBuilder.Entity<UsageCounter>()
                .Property(u => u.UsageType)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Configure enum conversion for UsageCounterStatus (store as string)
            modelBuilder.Entity<UsageCounter>()
                .Property(u => u.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            // ===== USAGE COUNTER INDEXES =====
            
            // Unique constraint: một company chỉ có 1 counter cho mỗi usageType + period với Status = Active và IsActive = true
            // CRITICAL: Index này bắt buộc để atomic check-and-increment hoạt động
            modelBuilder.Entity<UsageCounter>()
                .HasIndex(u => new { u.CompanyId, u.UsageType, u.PeriodStartDate, u.PeriodEndDate })
                .IsUnique()
                .HasFilter("\"IsActive\" = true AND \"Status\" = 'Active'")
                .HasDatabaseName("IX_UsageCounters_CompanyId_UsageType_Period_Unique");

            // Index for fast lookup by Status (only for non-deleted records)
            modelBuilder.Entity<UsageCounter>()
                .HasIndex(u => new { u.CompanyId, u.UsageType, u.Status })
                .HasFilter("\"IsActive\" = true AND \"Status\" IN ('Active', 'Archived')")
                .HasDatabaseName("IX_UsageCounters_CompanyId_UsageType_Status");

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
