namespace Common.Configuration;

/// <summary>
/// Configuration settings for Qdrant vector database.
/// </summary>
public class QdrantSettings
{
    public const string SectionName = "Qdrant";
    
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334;
    public int HttpPort { get; set; } = 6333;
}
