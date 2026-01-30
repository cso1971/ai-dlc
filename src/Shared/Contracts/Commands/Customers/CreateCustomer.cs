namespace Contracts.Commands.Customers;

public record CreateCustomer
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
