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
    entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
    entity.Property(e => e.UsedAt).HasColumnName("used_at");
    entity.Property(e => e.CreatedBy).HasColumnName("created_by");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
});
    }
}