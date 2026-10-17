namespace OrgMind.API.Models;

public class DocumentChunk
{
    public int Id { get; set; }
    public string OrgId { get; set; } = string.Empty;
    public string DocName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
}