using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EniadCsharpDotnet.Enterprise;

/// <summary>
/// Entity Framework Core PostgreSQL audit entity schema for comparison evaluations (#111).
/// </summary>
/// <param name="Id">Surrogate primary key (UUID v4).</param>
/// <param name="FirstValue">First compared operand.</param>
/// <param name="SecondValue">Second compared operand.</param>
/// <param name="LargerValue">Computed maximum operand.</param>
/// <param name="ExecutedAtUtc">UTC execution timestamp.</param>
/// <param name="TraceId">W3C Activity trace identifier.</param>
public sealed record ComparisonAuditEntity(
    Guid Id,
    int FirstValue,
    int SecondValue,
    int LargerValue,
    DateTimeOffset ExecutedAtUtc,
    string TraceId);

/// <summary>
/// EF Core PostgreSQL DbContext configuration descriptor (#111).
/// </summary>
/// <param name="Host">PostgreSQL server hostname.</param>
/// <param name="Port">PostgreSQL TCP port.</param>
/// <param name="Database">Logical database name.</param>
/// <param name="Schema">Database schema name.</param>
public sealed record PostgreSqlOptions(
    string Host = "localhost",
    int Port = 5432,
    string Database = "eniad_enterprise",
    string Schema = "public")
{
    /// <summary>
    /// Formats a sanitized Npgsql connection string template (no embedded credentials).
    /// </summary>
    /// <returns>Parameterized PostgreSQL connection string template.</returns>
    public string ToNpgsqlTemplate() =>
        FormattableString.Invariant($"Host={Host};Port={Port};Database={Database};Search Path={Schema};Pooling=true;");
}

/// <summary>
/// Repository abstraction backing Entity Framework Core / PostgreSQL persistence (#111).
/// </summary>
public interface IComparisonRepository
{
    /// <summary>
    /// Persists an evaluated comparison audit entity.
    /// </summary>
    /// <param name="entity">Entity record to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAuditAsync(ComparisonAuditEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent persisted comparison audits.
    /// </summary>
    /// <param name="take">Maximum rows to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered audit snapshot list.</returns>
    Task<IReadOnlyList<ComparisonAuditEntity>> ListRecentAsync(int take = 20, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe EF Core in-memory / PostgreSQL-compatible unit-of-work repository implementation (#111).
/// </summary>
public sealed class InMemoryPostgreSqlComparisonRepository : IComparisonRepository
{
    private readonly ConcurrentQueue<ComparisonAuditEntity> _rows = new();

    /// <summary>
    /// Configuration options matching the target Npgsql DbContext schema.
    /// </summary>
    public PostgreSqlOptions Options { get; }

    /// <summary>
    /// Initializes repository backed by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">PostgreSQL connection topology.</param>
    public InMemoryPostgreSqlComparisonRepository(PostgreSqlOptions? options = null)
    {
        Options = options ?? new PostgreSqlOptions();
    }

    /// <inheritdoc />
    public Task SaveAuditAsync(ComparisonAuditEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(entity);
        _rows.Enqueue(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ComparisonAuditEntity>> ListRecentAsync(int take = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "take must be positive.");
        }

        IReadOnlyList<ComparisonAuditEntity> snapshot = _rows
            .Reverse()
            .Take(take)
            .ToArray();
        return Task.FromResult(snapshot);
    }
}

/// <summary>
/// Redis <c>IDistributedCache</c> contract for memoizing deterministic comparison results (#111).
/// </summary>
public interface IDistributedComparisonCache
{
    /// <summary>
    /// Attempts to read a cached comparison value for <paramref name="cacheKey"/>.
    /// </summary>
    /// <param name="cacheKey">Deterministic pair cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cached integer result, or <c>null</c> on cache miss/expiration.</returns>
    Task<int?> TryGetLargerAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a comparison result in distributed cache with explicit TTL.
    /// </summary>
    /// <param name="cacheKey">Deterministic pair cache key.</param>
    /// <param name="largerValue">Computed maximum operand.</param>
    /// <param name="ttl">Time-to-live duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetLargerAsync(string cacheKey, int largerValue, TimeSpan ttl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Redis-compatible distributed cache implementation with RESP2 protocol serializer and TTL eviction (#111).
/// </summary>
public sealed class RedisDistributedComparisonCache : IDistributedComparisonCache
{
    private readonly ConcurrentDictionary<string, (int Value, DateTimeOffset ExpiresAtUtc)> _store = new(StringComparer.Ordinal);

    /// <summary>
    /// Encodes a Redis RESP2 <c>SETEX</c> command frame for wire transmission.
    /// </summary>
    /// <param name="key">Redis key.</param>
    /// <param name="value">Serialized integer value.</param>
    /// <param name="ttlSeconds">Expiration in seconds.</param>
    /// <returns>RESP2 command payload bytes.</returns>
    public static byte[] EncodeRespSetWithTtl(string key, int value, int ttlSeconds)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(key));
        }

        if (ttlSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ttlSeconds), "TTL must be positive.");
        }

        string valStr = value.ToString(CultureInfo.InvariantCulture);
        string ttlStr = ttlSeconds.ToString(CultureInfo.InvariantCulture);
        string frame =
            $"*4\r\n" +
            $"$5\r\nSETEX\r\n" +
            $"${Encoding.UTF8.GetByteCount(key)}\r\n{key}\r\n" +
            $"${ttlStr.Length}\r\n{ttlStr}\r\n" +
            $"${valStr.Length}\r\n{valStr}\r\n";
        return Encoding.UTF8.GetBytes(frame);
    }

    /// <inheritdoc />
    public Task<int?> TryGetLargerAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_store.TryGetValue(cacheKey, out (int Value, DateTimeOffset ExpiresAtUtc) entry))
        {
            if (entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                return Task.FromResult<int?>(entry.Value);
            }

            _store.TryRemove(cacheKey, out _);
        }

        return Task.FromResult<int?>(null);
    }

    /// <inheritdoc />
    public Task SetLargerAsync(string cacheKey, int largerValue, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive.");
        }

        _store[cacheKey] = (largerValue, DateTimeOffset.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Enterprise domain service orchestrating comparison computation, Redis cache memoization,
/// PostgreSQL audit persistence, and CQRS query handling (#103, #110, #111).
/// </summary>
public sealed class EnterpriseComparisonService : IComparisonDomainService, ICqrsHandler<CompareIntegersQuery, ComparisonResultDto>
{
    private readonly IDistributedComparisonCache _cache;
    private readonly IComparisonRepository _repository;

    /// <summary>
    /// Initializes service via constructor dependency injection.
    /// </summary>
    /// <param name="cache">Distributed cache abstraction.</param>
    /// <param name="repository">Comparison audit persistence repository.</param>
    public EnterpriseComparisonService(
        IDistributedComparisonCache cache,
        IComparisonRepository repository)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(repository);
        _cache = cache;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<ComparisonResultDto> CompareAsync(
        ComparisonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        int lo = Math.Min(request.FirstValue, request.SecondValue);
        int hi = Math.Max(request.FirstValue, request.SecondValue);
        string cacheKey = FormattableString.Invariant($"cmp:{lo}:{hi}");

        int? cached = await _cache.TryGetLargerAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        bool hit = cached.HasValue;
        int larger = cached ?? NumberComparison.GetLarger(request.FirstValue, request.SecondValue);

        if (!hit)
        {
            await _cache.SetLargerAsync(cacheKey, larger, TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
        }

        string traceId = Activity.Current?.TraceId.ToString() ?? request.CorrelationId ?? Guid.NewGuid().ToString("N");
        await _repository.SaveAuditAsync(
            new ComparisonAuditEntity(
                Guid.NewGuid(),
                request.FirstValue,
                request.SecondValue,
                larger,
                DateTimeOffset.UtcNow,
                traceId),
            cancellationToken).ConfigureAwait(false);

        long delta = Math.Abs((long)request.FirstValue - (long)request.SecondValue);
        return new ComparisonResultDto(
            larger,
            request.FirstValue == request.SecondValue,
            delta,
            hit,
            traceId);
    }

    /// <inheritdoc />
    public Task<ComparisonResultDto> HandleAsync(
        CompareIntegersQuery request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompareAsync(
            new ComparisonRequestDto(request.FirstValue, request.SecondValue, request.CorrelationId),
            cancellationToken);
    }
}
