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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.DocName).HasColumnName("doc_name");
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.Content).HasColumnName("content");
        });
    }
}