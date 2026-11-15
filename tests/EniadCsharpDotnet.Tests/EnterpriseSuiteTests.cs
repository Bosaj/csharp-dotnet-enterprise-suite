using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EniadCsharpDotnet.Enterprise;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EniadCsharpDotnet.Tests;

[TestClass]
public sealed class EnterpriseSuiteTests
{
    [TestMethod]
    public async Task DependencyInjectionCachePersistenceAndTelemetryWorkEndToEnd()
    {
        EnterpriseServiceRegistry registry = new();
        EnterpriseTelemetry telemetry = new();
        registry.RegisterSingleton(telemetry);
        registry.RegisterSingleton<IDistributedComparisonCache>(new RedisDistributedComparisonCache());
        registry.RegisterSingleton<IComparisonRepository>(new InMemoryPostgreSqlComparisonRepository());
        registry.Register<IComparisonDomainService>(() =>
            new EnterpriseComparisonService(
                registry.Resolve<IDistributedComparisonCache>(),
                registry.Resolve<IComparisonRepository>()));

        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == "EniadCsharpDotnet.Enterprise",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        EnterpriseComparisonService handler = new(
            registry.Resolve<IDistributedComparisonCache>(),
            registry.Resolve<IComparisonRepository>());

        MediatorDispatcher<CompareIntegersQuery, ComparisonResultDto> mediator = new(
            handler,
            new IPipelineBehavior<CompareIntegersQuery, ComparisonResultDto>[]
            {
                new TelemetryLoggingBehavior<CompareIntegersQuery, ComparisonResultDto>(telemetry),
                new FluentComparisonValidationBehavior(-100_000, 100_000),
            });

        // First call warms distributed cache (ServedFromCache = false)
        ComparisonResultDto first = await mediator.SendAsync(new CompareIntegersQuery(42, 99, "corr-1"));
        Assert.AreEqual(99, first.LargerValue);
        Assert.IsFalse(first.ServedFromCache);
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.TraceId));

        // Second call hits distributed cache regardless of operand order (ServedFromCache = true)
        ComparisonResultDto second = await mediator.SendAsync(new CompareIntegersQuery(99, 42, "corr-2"));
        Assert.AreEqual(99, second.LargerValue);
        Assert.IsTrue(second.ServedFromCache);

        // Audit rows persisted
        IReadOnlyList<ComparisonAuditEntity> audits = await registry.Resolve<IComparisonRepository>().ListRecentAsync();
        Assert.AreEqual(2, audits.Count);

        // Prometheus metrics & healthy probe
        string metricsText = telemetry.FormatPrometheusMetrics();
        Assert.IsTrue(metricsText.Contains("eniad_comparisons_total 2", StringComparison.Ordinal));
        Assert.IsTrue(metricsText.Contains("eniad_comparison_cache_hits_total 1", StringComparison.Ordinal));
        HealthProbeResultDto health = telemetry.EvaluateHealth();
        Assert.AreEqual("Healthy", health.Status);
    }

    [TestMethod]
    public async Task CqrsValidationFilterRejectsOutOfRangeAndBlankCorrelation()
    {
        using EnterpriseTelemetry telemetry = new();
        EnterpriseComparisonService handler = new(
            new RedisDistributedComparisonCache(),
            new InMemoryPostgreSqlComparisonRepository());
        MediatorDispatcher<CompareIntegersQuery, ComparisonResultDto> mediator = new(
            handler,
            new IPipelineBehavior<CompareIntegersQuery, ComparisonResultDto>[]
            {
                new TelemetryLoggingBehavior<CompareIntegersQuery, ComparisonResultDto>(telemetry),
                new FluentComparisonValidationBehavior(-500, 500),
            });

        await Assert.ThrowsExceptionAsync<CqrsValidationException>(() =>
            mediator.SendAsync(new CompareIntegersQuery(9001, 10, "   ")));

        Assert.AreEqual("Degraded", telemetry.EvaluateHealth().Status);
    }

    [TestMethod]
    public async Task GrpcStreamingAndProto3WireSerializationRoundtrip()
    {
        ComparisonRequestDto req = new(120, -45, "grpc-req-77");
        byte[] wire = ComparisonGrpcStreamingService.SerializeRequestProto3(req);
        ComparisonRequestDto decoded = ComparisonGrpcStreamingService.DeserializeRequestProto3(wire);
        Assert.AreEqual(req, decoded);

        EnterpriseComparisonService svc = new(
            new RedisDistributedComparisonCache(),
            new InMemoryPostgreSqlComparisonRepository());
        ComparisonGrpcStreamingService grpc = new(svc);

        List<ComparisonResultDto> streamed = new();
        await foreach (ComparisonResultDto item in grpc.CompareStreamAsync(StreamInputsAsync()))
        {
            streamed.Add(item);
        }

        Assert.AreEqual(2, streamed.Count);
        Assert.AreEqual(500, streamed[0].LargerValue);
        Assert.AreEqual(12, streamed[1].LargerValue);
    }

    [TestMethod]
    public void RedisRespCommandEncoderProducesValidFrame()
    {
        byte[] frame = RedisDistributedComparisonCache.EncodeRespSetWithTtl("cmp:1:2", 2, 600);
        string text = Encoding.UTF8.GetString(frame);
        Assert.IsTrue(text.StartsWith("*4\r\n$5\r\nSETEX\r\n", StringComparison.Ordinal));
    }

    private static async IAsyncEnumerable<ComparisonRequestDto> StreamInputsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ComparisonRequestDto(100, 500, "s1");
        yield return new ComparisonRequestDto(12, 12, "s2");
    }
}
