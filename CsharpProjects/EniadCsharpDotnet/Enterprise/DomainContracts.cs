using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EniadCsharpDotnet.Enterprise;

/// <summary>
/// Data transfer object representing a pair of integers to compare.
/// </summary>
/// <param name="FirstValue">The first 32-bit integer operand.</param>
/// <param name="SecondValue">The second 32-bit integer operand.</param>
/// <param name="CorrelationId">Optional caller-supplied request identifier.</param>
public sealed record ComparisonRequestDto(int FirstValue, int SecondValue, string? CorrelationId = null);

/// <summary>
/// Data transfer object containing comparison outcome and latency metadata.
/// </summary>
/// <param name="LargerValue">Maximum of the two operands.</param>
/// <param name="AreEqual">True when both operands are equal.</param>
/// <param name="Delta">Absolute difference between the operands as 64-bit integer.</param>
/// <param name="ServedFromCache">Indicates whether the response hit distributed cache.</param>
/// <param name="TraceId">W3C Activity trace identifier when tracing is active.</param>
public sealed record ComparisonResultDto(
    int LargerValue,
    bool AreEqual,
    long Delta,
    bool ServedFromCache,
    string TraceId);

/// <summary>
/// Structured readiness report emitted by health probe evaluations.
/// </summary>
/// <param name="Status">Health status label (<c>Healthy</c> or <c>Degraded</c>).</param>
/// <param name="TotalOperations">Total comparison requests processed.</param>
/// <param name="ErrorCount">Total unhandled or validation failures observed.</param>
/// <param name="TimestampUtc">UTC timestamp when the probe was sampled.</param>
public sealed record HealthProbeResultDto(
    string Status,
    long TotalOperations,
    long ErrorCount,
    DateTimeOffset TimestampUtc);

/// <summary>
/// Core domain contract for integer comparison operations (#103).
/// </summary>
public interface IComparisonDomainService
{
    /// <summary>
    /// Evaluates two operands and produces a typed comparison DTO.
    /// </summary>
    /// <param name="request">Input operands and optional correlation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Computed comparison result.</returns>
    Task<ComparisonResultDto> CompareAsync(
        ComparisonRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimalist enterprise dependency injection container decoupling interfaces from concrete providers (#103).
/// </summary>
public sealed class EnterpriseServiceRegistry
{
    private readonly Dictionary<Type, Func<object>> _registrations = new();

    /// <summary>
    /// Registers a transient factory for service contract <typeparamref name="TService"/>.
    /// </summary>
    /// <typeparam name="TService">Service abstraction type.</typeparam>
    /// <param name="factory">Factory delegate instantiating the implementation.</param>
    public void Register<TService>(Func<TService> factory) where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _registrations[typeof(TService)] = () => factory();
    }

    /// <summary>
    /// Registers a singleton instance for service contract <typeparamref name="TService"/>.
    /// </summary>
    /// <typeparam name="TService">Service abstraction type.</typeparam>
    /// <param name="instance">Concrete singleton instance.</param>
    public void RegisterSingleton<TService>(TService instance) where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _registrations[typeof(TService)] = () => instance;
    }

    /// <summary>
    /// Resolves a registered service instance.
    /// </summary>
    /// <typeparam name="TService">Service abstraction type.</typeparam>
    /// <returns>Resolved service implementation.</returns>
    public TService Resolve<TService>() where TService : class
    {
        if (_registrations.TryGetValue(typeof(TService), out Func<object>? factory))
        {
            return (TService)factory();
        }

        throw new InvalidOperationException($"Service '{typeof(TService).Name}' is not registered.");
    }
}
