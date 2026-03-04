namespace AI.Processor.Clients;

public interface IProjectionApiClient
{
    Task<ProjectionStatsResponse?> GetStatsAsync(CancellationToken cancellationToken = default);
}
