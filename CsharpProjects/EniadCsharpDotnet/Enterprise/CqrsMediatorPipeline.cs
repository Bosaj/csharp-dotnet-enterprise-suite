using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EniadCsharpDotnet.Enterprise;

/// <summary>
/// Marker contract for MediatR-style CQRS commands and queries (#110).
/// </summary>
/// <typeparam name="TResponse">Expected response DTO type.</typeparam>
public interface ICqrsRequest<TResponse>
{
}

/// <summary>
/// Handler contract processing a CQRS request.
/// </summary>
/// <typeparam name="TRequest">CQRS request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public interface ICqrsHandler<in TRequest, TResponse> where TRequest : ICqrsRequest<TResponse>
{
    /// <summary>
    /// Executes domain logic for the supplied request.
    /// </summary>
    /// <param name="request">Command or query payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution response.</returns>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pipeline behavior middleware intercepting CQRS dispatch (#110).
/// </summary>
/// <typeparam name="TRequest">CQRS request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : ICqrsRequest<TResponse>
{
    /// <summary>
    /// Intercepts execution around the next handler or inner behavior.
    /// </summary>
    /// <param name="request">Incoming CQRS request.</param>
    /// <param name="nextStep">Delegate invoking the remainder of the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pipeline response.</returns>
    Task<TResponse> HandleAsync(
        TRequest request,
        Func<CancellationToken, Task<TResponse>> nextStep,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// CQRS query evaluating maximum of two integers.
/// </summary>
/// <param name="FirstValue">First integer operand.</param>
/// <param name="SecondValue">Second integer operand.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
public sealed record CompareIntegersQuery(int FirstValue, int SecondValue, string? CorrelationId = null)
    : ICqrsRequest<ComparisonResultDto>;

/// <summary>
/// Fluent validation rule exception thrown by validation pipeline filters (#110).
/// </summary>
public sealed class CqrsValidationException : ArgumentException
{
    /// <summary>
    /// Initializes a validation failure with an itemized list of rule violations.
    /// </summary>
    /// <param name="errors">Validation errors detected before handler invocation.</param>
    public CqrsValidationException(IReadOnlyList<string> errors)
        : base($"CQRS validation failed: {string.Join("; ", errors)}")
    {
        Errors = errors;
    }

    /// <summary>
    /// Validation error descriptions.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}

/// <summary>
/// Pre-processor fluent validation behavior rejecting degenerate inputs when policy prohibits them (#110).
/// </summary>
public sealed class FluentComparisonValidationBehavior : IPipelineBehavior<CompareIntegersQuery, ComparisonResultDto>
{
    private readonly int _minAllowedValue;
    private readonly int _maxAllowedValue;

    /// <summary>
    /// Creates a validation behavior bounding allowed integer operands.
    /// </summary>
    /// <param name="minAllowedValue">Minimum inclusive operand allowed.</param>
    /// <param name="maxAllowedValue">Maximum inclusive operand allowed.</param>
    public FluentComparisonValidationBehavior(int minAllowedValue = -1_000_000_000, int maxAllowedValue = 1_000_000_000)
    {
        _minAllowedValue = minAllowedValue;
        _maxAllowedValue = maxAllowedValue;
    }

    /// <inheritdoc />
    public Task<ComparisonResultDto> HandleAsync(
        CompareIntegersQuery request,
        Func<CancellationToken, Task<ComparisonResultDto>> nextStep,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(nextStep);

        List<string> errors = new();
        if (request.FirstValue < _minAllowedValue || request.FirstValue > _maxAllowedValue)
        {
            errors.Add($"FirstValue ({request.FirstValue}) must be within [{_minAllowedValue}, {_maxAllowedValue}].");
        }

        if (request.SecondValue < _minAllowedValue || request.SecondValue > _maxAllowedValue)
        {
            errors.Add($"SecondValue ({request.SecondValue}) must be within [{_minAllowedValue}, {_maxAllowedValue}].");
        }

        if (request.CorrelationId is not null && string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            errors.Add("CorrelationId cannot be blank whitespace when specified.");
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return nextStep(cancellationToken);
    }
}

/// <summary>
/// Telemetry and timing pipeline behavior wrapping CQRS handler execution.
/// </summary>
/// <typeparam name="TRequest">CQRS request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public sealed class TelemetryLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICqrsRequest<TResponse>
{
    private readonly EnterpriseTelemetry _telemetry;

    /// <summary>
    /// Initializes logging behavior with shared telemetry service.
    /// </summary>
    /// <param name="telemetry">Enterprise telemetry collector.</param>
    public TelemetryLoggingBehavior(EnterpriseTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        Func<CancellationToken, Task<TResponse>> nextStep,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nextStep);
        using Activity? span = _telemetry.StartSpan($"CQRS:{typeof(TRequest).Name}");
        long startStamp = Stopwatch.GetTimestamp();
        try
        {
            TResponse response = await nextStep(cancellationToken).ConfigureAwait(false);
            double elapsedMs = Stopwatch.GetElapsedTime(startStamp).TotalMilliseconds;
            bool cached = response is ComparisonResultDto dto && dto.ServedFromCache;
            _telemetry.RecordOperation(elapsedMs, cached);
            return response;
        }
        catch
        {
            _telemetry.RecordError();
            throw;
        }
    }
}

/// <summary>
/// MediatR-compatible CQRS dispatcher composing ordered pipeline behaviors around a handler (#110).
/// </summary>
/// <typeparam name="TRequest">CQRS request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public sealed class MediatorDispatcher<TRequest, TResponse> where TRequest : ICqrsRequest<TResponse>
{
    private readonly ICqrsHandler<TRequest, TResponse> _handler;
    private readonly IReadOnlyList<IPipelineBehavior<TRequest, TResponse>> _behaviors;

    /// <summary>
    /// Creates a CQRS pipeline dispatcher for <typeparamref name="TRequest"/>.
    /// </summary>
    /// <param name="handler">Terminal request handler.</param>
    /// <param name="behaviors">Ordered pipeline filters applied outside-in.</param>
    public MediatorDispatcher(
        ICqrsHandler<TRequest, TResponse> handler,
        IReadOnlyList<IPipelineBehavior<TRequest, TResponse>>? behaviors = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        _behaviors = behaviors ?? Array.Empty<IPipelineBehavior<TRequest, TResponse>>();
    }

    /// <summary>
    /// Dispatches <paramref name="request"/> through all registered behaviors into the handler.
    /// </summary>
    /// <param name="request">Incoming request DTO.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Produced response DTO.</returns>
    public Task<TResponse> SendAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task<TResponse>> pipeline = ct => _handler.HandleAsync(request, ct);
        for (int i = _behaviors.Count - 1; i >= 0; i--)
        {
            IPipelineBehavior<TRequest, TResponse> behavior = _behaviors[i];
            Func<CancellationToken, Task<TResponse>> currentNext = pipeline;
            pipeline = ct => behavior.HandleAsync(request, currentNext, ct);
        }

        return pipeline(cancellationToken);
    }
}
