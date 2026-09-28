using Microsoft.AspNetCore.StaticFiles;

namespace Maroik.Website.Extensions;
/// <summary>
/// Extension methods for configuring static file serving in the ASP.NET Core pipeline.
/// </summary>
public static class StaticAssetExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// <para>
        /// Note: despite its name this is <c>UseStaticFiles</c> (files are read from disk at request
        /// time, so runtime-written files such as cached avatars are served), NOT ASP.NET Core's
        /// build-manifest-based <c>MapStaticAssets</c> endpoint API. The name is kept because
        /// <c>main</c> uses it; it is preferred over the framework overload only because this one takes
        /// no optional parameters.
        /// </para>
        /// <para>
        /// Registers <see cref="Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware"/> with explicit
        /// MIME-type mappings for .js and .css files, and adds the
        /// <c>X-Content-Type-Options: nosniff</c> security header to every static-file response.
        /// </para>
        /// </summary>
        public void MapStaticAssets()
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                // Explicitly map common web asset extensions to prevent MIME sniffing issues
                ContentTypeProvider = new FileExtensionContentTypeProvider
                {
                    Mappings =
                    {
                        [".js"] = "application/javascript",
                        [".css"] = "text/css"
                    }
                },
                // Add X-Content-Type-Options: nosniff to prevent browsers from MIME-sniffing responses
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
                }
            });
        }
    }
}
