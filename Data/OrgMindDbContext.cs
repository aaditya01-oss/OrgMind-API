using Microsoft.EntityFrameworkCore;
using OrgMind.API.Models;

namespace OrgMind.API.Data;

public class OrgMindDbContext : DbContext
{
    public OrgMindDbContext(DbContextOptions<OrgMindDbContext> options) 
        : base(options)
    {
    }

    public DbSet<DocumentChunk> DocumentChunks { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<Invite> Invites { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    public DbSet<Ticket> Tickets { get; set; }

    public DbSet<FormTemplate> FormTemplates { get; set; }
    public DbSet<FormSubmission> FormSubmissions { get; set; }
    public DbSet<ThreadMessage> ThreadMessages { get; set; }

    public DbSet<StaffMessage> StaffMessages { get; set; }
    public DbSet<StaffRead> StaffReads { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.DocName).HasColumnName("doc_name");
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.Content).HasColumnName("content");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.Role).HasColumnName("role");
            entity.Property(e => e.Department).HasColumnName("department");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
        entity.ToTable("organizations");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Name).HasColumnName("name");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

modelBuilder.Entity<Invite>(entity =>
{
    entity.ToTable("invites");
    entity.HasKey(e => e.Code);
    entity.Property(e => e.Code).HasColumnName("code");
    entity.Property(e => e.OrgId).HasColumnName("org_id");
    entity.Property(e => e.Role).HasColumnName("role");
    entity.Property(e => e.Department).HasColumnName("department");
    entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
    entity.Property(e => e.UsedAt).HasColumnName("used_at");
    entity.Property(e => e.CreatedBy).HasColumnName("created_by");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
});
modelBuilder.Entity<AuditLog>(entity =>
{
    entity.ToTable("audit_logs");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.EventType).HasColumnName("event_type");
    entity.Property(e => e.UserId).HasColumnName("user_id");
    entity.Property(e => e.OrgId).HasColumnName("org_id");
    entity.Property(e => e.Details).HasColumnName("details").HasColumnType("jsonb");
    entity.Property(e => e.Severity).HasColumnName("severity");
    entity.Property(e => e.Timestamp).HasColumnName("timestamp")
          .HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
});
modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Issue).HasColumnName("issue");
            entity.Property(e => e.Priority).HasColumnName("priority");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Department).HasColumnName("department");
            entity.Property(e => e.Sentiment).HasColumnName("sentiment");
        });

        
        modelBuilder.Entity<FormTemplate>(entity =>
        {
            entity.ToTable("form_templates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Audience).HasColumnName("audience");
            entity.Property(e => e.Department).HasColumnName("department");
            entity.Property(e => e.Fields).HasColumnName("fields").HasColumnType("jsonb");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<FormSubmission>(entity =>
        {
            entity.ToTable("form_submissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.TemplateId).HasColumnName("template_id");
            entity.Property(e => e.TemplateName).HasColumnName("template_name");
            entity.Property(e => e.SubmittedBy).HasColumnName("submitted_by");
            entity.Property(e => e.Department).HasColumnName("department");
            entity.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.HandledBy).HasColumnName("handled_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.StatusNote).HasColumnName("status_note");
        });

        modelBuilder.Entity<ThreadMessage>(entity =>
        {
            entity.ToTable("thread_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.Kind).HasColumnName("kind");
            entity.Property(e => e.RefId).HasColumnName("ref_id");
            entity.Property(e => e.Author).HasColumnName("author");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.ReadByOwner).HasColumnName("read_by_owner");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at")
                  .HasDefaultValueSql("(NOW() AT TIME ZONE 'UTC')").ValueGeneratedOnAdd();
        });

            modelBuilder.Entity<StaffMessage>(entity =>
        {
            entity.ToTable("staff_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.Channel).HasColumnName("channel");
            entity.Property(e => e.Author).HasColumnName("author");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at")
                  .HasDefaultValueSql("(NOW() AT TIME ZONE 'UTC')").ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<StaffRead>(entity =>
        {
            entity.ToTable("staff_reads");
            entity.HasKey(e => new { e.OrgId, e.UserEmail, e.Channel });
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.UserEmail).HasColumnName("user_email");
            entity.Property(e => e.Channel).HasColumnName("channel");
            entity.Property(e => e.LastReadId).HasColumnName("last_read_id");
        });
    }
}