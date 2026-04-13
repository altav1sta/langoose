using Langoose.Core.Configuration;
using Langoose.Domain.Services;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Langoose.Worker.Services;

public sealed class EnrichmentBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<EnrichmentSettings> settings,
    IVariantFeatureManager featureManager,
    ILogger<EnrichmentBackgroundService> logger) : BackgroundService
{
    private const string FeatureFlag = "EnableAiEnrichment";

    private readonly EnrichmentSettings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EnrichmentBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await featureManager.IsEnabledAsync(FeatureFlag, stoppingToken))
                {
                    logger.LogDebug("Feature flag {Flag} is disabled, skipping poll.", FeatureFlag);

                    await Task.Delay(TimeSpan.FromSeconds(_settings.PollIntervalSeconds), stoppingToken);
                    continue;
                }

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEnrichmentProcessor>();

                await processor.ProcessPendingBatchAsync(
                    _settings.BatchSize, _settings.MaxRetries, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in enrichment poll cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.PollIntervalSeconds), stoppingToken);
        }

        logger.LogInformation("EnrichmentBackgroundService is stopping.");
    }
}
