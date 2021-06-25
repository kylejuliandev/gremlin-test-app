using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using GremlinTestApp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Net.WebSockets;

namespace GremlinTestApp.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGremlinClient(this IServiceCollection services)
        {
            services.AddTransient<IGremlinClient>((serviceProvider) =>
            {
                var gremlinOptions = serviceProvider.GetRequiredService<IOptions<GremlinOptions>>();
                var gremlinConfig = gremlinOptions.Value;

                return GetGremlinClient(gremlinConfig);
            });

            return services;
        }

        private static GremlinClient GetGremlinClient(GremlinOptions gremlinConfig)
        {
            var containerLink = $"/dbs/{gremlinConfig.Database}/colls/{gremlinConfig.Container}";
            var gremlinServer = new GremlinServer(gremlinConfig.Host, gremlinConfig.Port, enableSsl: true, username: containerLink, password: gremlinConfig.PrimaryKey);

            var connectionPoolSettings = new ConnectionPoolSettings()
            {
                MaxInProcessPerConnection = 32,
                PoolSize = 1
            };

            var webSocketConfiguration =
                new Action<ClientWebSocketOptions>(options =>
                {
                    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                });

            return new GremlinClient(
                gremlinServer,
                new GraphSON2Reader(),
                new GraphSON2Writer(),
                GremlinClient.GraphSON2MimeType,
                connectionPoolSettings,
                webSocketConfiguration
            );
        }
    }
}
