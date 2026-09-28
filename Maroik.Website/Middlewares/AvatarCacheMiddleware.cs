using Maroik.Core.Contract.Interfaces;

namespace Maroik.Website.Middlewares;

/// <summary>
/// Serves uploaded profile avatars in a replica-safe way. Avatars are stored in the shared
/// file-storage service (see <c>ProfileService.UploadAndUpdateAvatarAsync</c>), but the
/// <c>&lt;img src&gt;</c> in the views points at the plain static path
/// <c>/upload/Management/Profile/Avatar/&lt;file&gt;</c>. On a GET for that path, if the file is not
/// already present in this replica's <c>wwwroot</c>, it is fetched once from file storage (via
/// <see cref="IProfileService"/>) and written there; the static-file middleware that runs
/// immediately after then serves it (with its normal caching/ETag handling) on this and every later
/// request. A replica that never handled the upload therefore self-heals on the first render
/// instead of 404-ing.
/// </summary>
public sealed class AvatarCacheMiddleware(RequestDelegate next)
{
    private const string UrlPrefix = "/upload/Management/Profile/Avatar/";

    /// <summary>Lazily populates this replica's local avatar cache from file storage, then forwards the request.</summary>
    public async Task InvokeAsync(
        HttpContext context,
        IWebHostEnvironment env,
        IProfileService profileService,
        ILogger<AvatarCacheMiddleware> logger)
    {
        string? path = context.Request.Path.Value;

        // Case-sensitive: the deployment target is Linux (case-sensitive filesystem), the views emit
        // this exact-case path, and file storage matches exact case — anything else would gate a set
        // of URLs that doesn't line up with the ones that actually resolve to a file.
        if (!string.IsNullOrEmpty(env.WebRootPath)
            && path != null
            && HttpMethods.IsGet(context.Request.Method)
            && path.StartsWith(UrlPrefix, StringComparison.Ordinal))
        {
            // GetFileName drops any directory component, so a "../" in the URL cannot escape the cache dir.
            string fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName))
            {
                string localDir = Path.Combine(env.WebRootPath, "upload", "Management", "Profile", "Avatar");
                string localPath = Path.Combine(localDir, fileName);

                if (!File.Exists(localPath))
                {
                    try
                    {
                        byte[]? bytes = await profileService.DownloadAvatarAsync(fileName, context.RequestAborted);

                        if (bytes is { Length: > 0 })
                        {
                            Directory.CreateDirectory(localDir);
                            // Write to a temp file then move into place so a concurrent request for the
                            // same avatar never sees a half-written file. localPath is localDir plus
                            // Path.GetFileName(...) (directory components already stripped), so it
                            // cannot escape localDir — suppress the path-traversal false positive.
                            string tempPath = Path.Combine(localDir, $".{Guid.NewGuid():N}.tmp");
#pragma warning disable SCS0018
                            await File.WriteAllBytesAsync(tempPath, bytes, context.RequestAborted);
                            File.Move(tempPath, localPath, overwrite: true);
#pragma warning restore SCS0018
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fall through: the static-file middleware will 404 and the views carry their
                        // own default-avatar fallback image.
                        logger.LogWarning(ex, "Failed to cache avatar {FileName} locally", fileName);
                    }
                }
            }
        }

        await next(context);
    }
}
