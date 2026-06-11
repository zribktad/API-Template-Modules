using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileStorage.Services;

/// <summary>
///     Sweeps filesystem orphans produced by interrupted or timed-out uploads.
///     <para>
///         Two passes: (1) staging files older than <c>StagingTtlMinutes × 2</c> are deleted defensively —
///         successful saga timeouts already clean their own staging, so anything remaining is a crash
///         residue; (2) blobs whose content has no active <see cref="StoredFile" /> row within their
///         tenant and whose file mtime is older than <c>BlobRetentionHours</c> are deleted.
///     </para>
/// </summary>
public interface IOrphanBlobSweepService
{
    Task<OrphanBlobSweepResult> SweepAsync(CancellationToken ct);
}

public sealed record OrphanBlobSweepResult(int StagingDeleted, int BlobsDeleted);

internal sealed class OrphanBlobSweepService : IOrphanBlobSweepService
{
    // Batches the per-tenant refcount query to stay below Postgres parameter limits (default ~65535).
    private const int RefcountQueryBatchSize = 1000;

    private readonly FileStorageOptions _options;
    private readonly FileStorageDbContext _dbContext;
    private readonly ILogger<OrphanBlobSweepService> _logger;
    private readonly TimeProvider _timeProvider;

    public OrphanBlobSweepService(
        IOptions<FileStorageOptions> options,
        FileStorageDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<OrphanBlobSweepService> logger
    )
    {
        _options = options.Value;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<OrphanBlobSweepResult> SweepAsync(CancellationToken ct)
    {
        int stagingDeleted = SweepStaging();
        int blobsDeleted = await SweepOrphanBlobsAsync(ct);

        if (stagingDeleted > 0 || blobsDeleted > 0)
            _logger.LogInformation(
                "Orphan blob sweep: deleted {StagingDeleted} staging files and {BlobsDeleted} orphan blobs.",
                stagingDeleted,
                blobsDeleted
            );

        return new OrphanBlobSweepResult(stagingDeleted, blobsDeleted);
    }

    private int SweepStaging()
    {
        string stagingDir = _options.ResolveStagingPath();
        if (!Directory.Exists(stagingDir))
            return 0;

        DateTimeOffset cutoff = _timeProvider
            .GetUtcNow()
            .AddMinutes(-2 * _options.StagingTtlMinutes);

        int deleted = 0;
        DirectoryInfo dir = new(stagingDir);

        IEnumerable<FileInfo> candidates;
        try
        {
            candidates = dir.EnumerateFiles();
        }
        catch (DirectoryNotFoundException)
        {
            return 0;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to enumerate staging directory {StagingDir}",
                stagingDir
            );
            return 0;
        }

        foreach (FileInfo info in candidates)
        {
            try
            {
                if (info.LastWriteTimeUtc < cutoff.UtcDateTime)
                {
                    info.Delete();
                    deleted++;
                }
            }
            catch (FileNotFoundException) { }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to delete staging file {Path}", info.FullName);
            }
        }

        return deleted;
    }

    private async Task<int> SweepOrphanBlobsAsync(CancellationToken ct)
    {
        string blobsRoot = _options.ResolveBlobsPath();
        if (!Directory.Exists(blobsRoot))
            return 0;

        DateTimeOffset cutoff = _timeProvider.GetUtcNow().AddHours(-_options.BlobRetentionHours);

        int deleted = 0;

        IEnumerable<string> tenantDirs;
        try
        {
            tenantDirs = Directory.EnumerateDirectories(blobsRoot);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate blob root {BlobsRoot}", blobsRoot);
            return 0;
        }

        foreach (string tenantDir in tenantDirs)
        {
            if (!Guid.TryParse(Path.GetFileName(tenantDir), out Guid tenantId))
                continue;

            List<(string Path, string Sha)> candidates = CollectTenantBlobCandidates(
                tenantDir,
                cutoff.UtcDateTime
            );

            if (candidates.Count == 0)
                continue;

            HashSet<string> liveShas = await LoadLiveShasAsync(tenantId, candidates, ct);

            foreach ((string blobPath, string sha) in candidates)
            {
                ct.ThrowIfCancellationRequested();
                if (liveShas.Contains(sha))
                    continue;

                try
                {
                    File.Delete(blobPath);
                    deleted++;
                }
                catch (FileNotFoundException) { }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Failed to delete orphan blob {Path}", blobPath);
                }
            }
        }

        return deleted;
    }

    private static List<(string Path, string Sha)> CollectTenantBlobCandidates(
        string tenantDir,
        DateTime cutoffUtc
    )
    {
        List<(string, string)> result = new();

        IEnumerable<DirectoryInfo> shaPrefixDirs;
        try
        {
            shaPrefixDirs = new DirectoryInfo(tenantDir).EnumerateDirectories();
        }
        catch (DirectoryNotFoundException)
        {
            return result;
        }
        catch (IOException)
        {
            return result;
        }

        foreach (DirectoryInfo shaPrefixDir in shaPrefixDirs)
        {
            IEnumerable<FileInfo> blobs;
            try
            {
                blobs = shaPrefixDir.EnumerateFiles();
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (FileInfo info in blobs)
            {
                if (info.LastWriteTimeUtc >= cutoffUtc)
                    continue;

                result.Add((info.FullName, info.Name));
            }
        }

        return result;
    }

    private async Task<HashSet<string>> LoadLiveShasAsync(
        Guid tenantId,
        List<(string Path, string Sha)> candidates,
        CancellationToken ct
    )
    {
        HashSet<string> liveShas = new(StringComparer.Ordinal);
        List<string> allShas = candidates.Select(c => c.Sha).Distinct().ToList();

        for (int offset = 0; offset < allShas.Count; offset += RefcountQueryBatchSize)
        {
            int take = Math.Min(RefcountQueryBatchSize, allShas.Count - offset);
            List<string> chunk = allShas.GetRange(offset, take);

            // Background sweep runs with no HTTP scope; the tenant global query filter would
            // collapse to WHERE false and make every blob look orphaned (data loss). Bypass it
            // and scope explicitly by the tenant we are sweeping.
            List<string> live = await _dbContext
                .StoredFiles.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId && !f.IsDeleted && chunk.Contains(f.Sha256))
                .Select(f => f.Sha256)
                .ToListAsync(ct);

            foreach (string sha in live)
                liveShas.Add(sha);
        }

        return liveShas;
    }
}
