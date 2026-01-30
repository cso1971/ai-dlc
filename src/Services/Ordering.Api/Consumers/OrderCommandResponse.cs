namespace Ordering.Api.Consumers;

public record OrderCommandResponse
{
    public Guid OrderId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
