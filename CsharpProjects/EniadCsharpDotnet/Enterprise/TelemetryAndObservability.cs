using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading;

namespace EniadCsharpDotnet.Enterprise;

/// <summary>
/// OpenTelemetry-compatible <see cref="ActivitySource"/> and <see cref="Meter"/> instrumentation
/// plus Prometheus text exposition and health probes (#104, #109).
/// </summary>
public sealed class EnterpriseTelemetry : IDisposable
{
    private long _totalOperations;
    private long _errorCount;
    private long _cacheHits;
    private double _lastDurationMs;

    /// <summary>
    /// Distributed tracing source for OpenTelemetry exporters.
    /// </summary>
    public ActivitySource ActivitySource { get; } = new("EniadCsharpDotnet.Enterprise", "1.4.0");

    /// <summary>
    /// Metrics meter exporting comparison throughput and latency histograms.
    /// </summary>
    public Meter Meter { get; } = new("EniadCsharpDotnet.Enterprise", "1.4.0");

    private readonly Counter<long> _comparisonCounter;
    private readonly Histogram<double> _latencyHistogramMs;

    /// <summary>
    /// Initializes metric instruments bound to the enterprise meter.
    /// </summary>
    public EnterpriseTelemetry()
    {
        _comparisonCounter = Meter.CreateCounter<long>("eniad_comparisons_total", description: "Total comparisons executed.");
        _latencyHistogramMs = Meter.CreateHistogram<double>("eniad_comparison_duration_ms", unit: "ms", description: "Comparison execution latency.");
    }

    /// <summary>
    /// Starts an OpenTelemetry span for an enterprise comparison operation.
    /// </summary>
    /// <param name="operationName">Span name.</param>
    /// <returns>Started <see cref="Activity"/> or <c>null</c> if unsampled.</returns>
    public Activity? StartSpan(string operationName) =>
        ActivitySource.StartActivity(operationName, ActivityKind.Server);

    /// <summary>
    /// Records completion telemetry for a comparison request.
    /// </summary>
    /// <param name="durationMs">Elapsed execution duration in milliseconds.</param>
    /// <param name="servedFromCache">Whether distributed cache satisfied the request.</param>
    public void RecordOperation(double durationMs, bool servedFromCache)
    {
        Interlocked.Increment(ref _totalOperations);
        if (servedFromCache)
        {
            Interlocked.Increment(ref _cacheHits);
        }

        Volatile.Write(ref _lastDurationMs, Math.Max(0.0, durationMs));
        _comparisonCounter.Add(1, new KeyValuePair<string, object?>("cache_hit", servedFromCache));
        _latencyHistogramMs.Record(durationMs);
    }

    /// <summary>
    /// Increments the failure counter for degraded health detection.
    /// </summary>
    public void RecordError() => Interlocked.Increment(ref _errorCount);

    /// <summary>
    /// Evaluates current service readiness probe state (#104).
    /// </summary>
    /// <returns>Structured health check payload.</returns>
    public HealthProbeResultDto EvaluateHealth()
    {
        long ops = Interlocked.Read(ref _totalOperations);
        long errs = Interlocked.Read(ref _errorCount);
        string status = errs == 0 ? "Healthy" : "Degraded";
        return new HealthProbeResultDto(status, ops, errs, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Renders current metrics in Prometheus / OpenMetrics exposition format (#109).
    /// </summary>
    /// <returns>Multi-line Prometheus scrape payload.</returns>
    public string FormatPrometheusMetrics()
    {
        long ops = Interlocked.Read(ref _totalOperations);
        long errs = Interlocked.Read(ref _errorCount);
        long hits = Interlocked.Read(ref _cacheHits);
        double latency = Volatile.Read(ref _lastDurationMs);
        return string.Join(
            '\n',
            "# TYPE eniad_comparisons_total counter",
            $"eniad_comparisons_total {ops}",
            "# TYPE eniad_comparison_cache_hits_total counter",
            $"eniad_comparison_cache_hits_total {hits}",
            "# TYPE eniad_comparison_errors_total counter",
            $"eniad_comparison_errors_total {errs}",
            "# TYPE eniad_comparison_last_duration_ms gauge",
            string.Create(CultureInfo.InvariantCulture, $"eniad_comparison_last_duration_ms {latency:F4}"));
    }

    /// <summary>
    /// Disposes managed tracing and metric instruments.
    /// </summary>
    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}
