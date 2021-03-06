using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Counter;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Airbag
{
    public static class AuthMiddleware
    {
        private static async Task<bool> IsAuthenticated(HttpContext ctx, IEnumerable<string> authSchemas)
        {
            foreach (var schema in authSchemas)
            {
                try
                {
                    var res = await ctx.AuthenticateAsync(schema);
                    if (res != null && res.Succeeded)
                    {
                        ctx.Request.HttpContext.User = res.Principal;
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        public static void UseAuthenticatedRoutes(this IApplicationBuilder app)
        {
            var authSchemes = app.ApplicationServices.GetServices<Provider>().Select(provider => provider.Name);
            var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
            var validateRoutes = configuration.GetValue("AUTHORIZED_ROUTES_ENABLED", true);

            if (!validateRoutes) return;

            var metrics = app.ApplicationServices.GetService<IMetrics>();
            var shouldCollectMetrics = configuration.GetValue<bool>("COLLECT_METRICS");
            
            app.Use(async (ctx, next) =>
            {
                if (!await IsAuthenticated(ctx, authSchemes))
                {
                    ctx.Response.StatusCode = 403;
                    if (shouldCollectMetrics)
                    {
                        metrics.Measure.Counter.Increment(new CounterOptions() {Name = "client_failed_to_authenticate"});
                    }
                    Console.WriteLine("Failed to authenticate");
                    return;
                }

                await next();
            });
        }
    }
}