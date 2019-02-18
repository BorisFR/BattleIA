using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace BattleIAserver
{
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) { }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            /*var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
            System.Diagnostics.Debug.WriteLine($"ADDR: {string.Join(", ", serverAddressesFeature.Addresses)}");
            Console.WriteLine($"ADDR: {string.Join(", ", serverAddressesFeature.Addresses)}");*/

            // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-2.2

            // parametres pour réception des websocket
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30),
                ReceiveBufferSize = 4 * 1024
            };
            //webSocketOptions.AllowedOrigins.Add("http://ly0500");
            //webSocketOptions.AllowedOrigins.Add("wss://ly0500");
            //webSocketOptions.AllowedOrigins.Add("file://");

            app.UseWebSockets(webSocketOptions);

            app.Use(async (context, next) =>
            {
                //Console.WriteLine("Nouvelle connexion WS");
                // ouverture d'une websocket, un nouveau client se connecte
                if (context.Request.Path == "/ia")
                {
                    //Console.WriteLine("de type /ia");
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        //Console.WriteLine("AcceptWebSocketAsync");
                        // on l'ajoute à notre jeu !
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        //await Echo(context, webSocket);
                        //Console.WriteLine("Launch AddClient");
                        await MainGame.AddClient(webSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    //Console.WriteLine("de type /display");
                    if (context.Request.Path == "/display")
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            // on l'ajoute à notre jeu !
                            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                            await MainGame.AddViewer(webSocket);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else
                    {
                        await next();
                    }
                }
            });
        }

    }
}