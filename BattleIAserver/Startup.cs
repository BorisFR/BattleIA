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
using Microsoft.Extensions.FileProviders;
using System.IO;

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

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "WebPages")), RequestPath = "/WebPages" });

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

            // ICI on foonctionne en THREAD !
            app.Use(async (context, next) =>
            {
                Console.WriteLine("Nouvelle connexion WS");
                // ouverture d'une websocket, un nouveau client se connecte
                if (context.Request.Path == "/ia")
                {
                    Console.WriteLine("WS de type /ia");
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        //Console.WriteLine("AcceptWebSocketAsync");
                        // on l'ajoute à notre simulation !
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        //await Echo(context, webSocket);
                        Console.WriteLine("Nouveau BOT !");
                        // Démarrage d'un nouveau client. Si on revient c'est qu'il est mort !
                        await MainGame.AddClient(webSocket);
                        Console.WriteLine($"Il y a {MainGame.AllBot.Count} BOT");
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        Console.WriteLine("WS en erreur : Not a WebSocket establishment request.");
                    }
                }
                else
                {
                    if (context.Request.Path == "/display")
                    {
                        Console.WriteLine("WS de type /display");
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            // on l'ajoute à notre simulation !
                            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                            Console.WriteLine("Nouveau VIEWER !");
                            await MainGame.AddViewer(webSocket);
                            Console.WriteLine($"Il y a {MainGame.AllViewer.Count} VIEWER");
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            Console.WriteLine("WS en erreur : Not a WebSocket establishment request.");
                        }
                    }
                    else
                    {
                        if (context.Request.Path == "/console")
                        {
                            Console.WriteLine("WS de type /console");
                            if (context.WebSockets.IsWebSocketRequest)
                            {
                                // on l'ajoute à notre simulation !
                                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                                Console.WriteLine("Nouvelle CONSOLE !");
                                //await MainGame.AddViewer(webSocket);
                                //Console.WriteLine($"Il y a {MainGame.AllViewer.Count} BOT");
                            }
                            else
                            {
                                context.Response.StatusCode = 400;
                                Console.WriteLine("WS en erreur : Not a WebSocket establishment request.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"WS de type inconnu: {context.Request.Path}");
                            await next();
                        }
                    }
                }
            });
        }

    }
}