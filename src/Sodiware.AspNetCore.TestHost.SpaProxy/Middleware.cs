using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Sodiware.AspNetCore.TestHost.SpaProxy
{
    internal class StartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return (app) =>
            {
                var opts = app.ApplicationServices.GetRequiredService<IOptions<SpaDevelopmentServerOptions>>().Value;
                    usePublishDirectory(app, opts);
                next(app);
                //app.UseMiddleware<Middleware>();
            };
        }

        private void usePublishDirectory(IApplicationBuilder app, SpaDevelopmentServerOptions opts)
        {
            var dir = opts.ApplicationPublishDirectory;
            /*
            if (dir.IsPresent())
            {
                app.UseDefaultFiles();
                app.UseStaticFiles(new StaticFileOptions
                {
                    ContentTypeProvider = new FileExtensionContentTypeProvider(),
                    FileProvider = new PhysicalFileProvider(dir)
                });
            }
            */
        }
    }
    internal sealed class Middleware
    {
        private readonly RequestDelegate next;

        public Middleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var options = context.RequestServices.GetRequiredService<IOptions<SpaDevelopmentServerOptions>>().Value;

            await next.Invoke(context);
            /*
            if (!context.Response.HasStarted && context.Response.StatusCode == (int)HttpStatusCode.NotFound)
            {
                context.Response.Redirect(options.ServerUrl);
            }
            */
        }
    }
}
