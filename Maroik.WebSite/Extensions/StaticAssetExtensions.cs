using Microsoft.AspNetCore.StaticFiles;

namespace Maroik.WebSite.Extensions;
public static class StaticAssetExtensions
{
    public static IApplicationBuilder MapStaticAssets(this IApplicationBuilder app)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = new FileExtensionContentTypeProvider
            {
                Mappings =
                {
                    [".js"] = "application/javascript",
                    [".css"] = "text/css"
                }
            },
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            }
        });

        return app;
    }
}
