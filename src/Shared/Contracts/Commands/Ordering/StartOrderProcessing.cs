namespace Contracts.Commands.Ordering;

// Created → InProgress
public record StartOrderProcessing
{
    public Guid OrderId { get; init; }
    public string? Notes { get; init; }
}
