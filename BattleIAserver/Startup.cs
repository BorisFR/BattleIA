﻿using System;
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

            // ICI on fonctionne en THREAD !
            app.Use(async (context, next) =>
            {
                Console.WriteLine("New WebSocket connection");
                // ouverture d'une websocket, un nouveau bot se connecte
                if (context.Request.Path == "/bot")
                {
                    Console.WriteLine("WebSocket /bot");
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        //Console.WriteLine("AcceptWebSocketAsync");
                        // on l'ajoute à notre simulation !
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        //await Echo(context, webSocket);
                        Console.WriteLine("A new BOT in arena!");
                        // Démarrage d'un nouveau bot. Si on revient c'est qu'il est mort !
                        await MainGame.AddBot(webSocket);
                        Console.WriteLine($"#BOTS: {MainGame.AllBot.Count}");
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        Console.WriteLine("WebSocket ERROR : Not a WebSocket establishment request.");
                    }
                }
                else
                {
                    if (context.Request.Path == "/display")
                    {
                        Console.WriteLine("WebSocket /display");
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            // on l'ajoute à notre simulation !
                            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                            Console.WriteLine("New DISPLAY!");
                            await MainGame.AddViewer(webSocket);
                            Console.WriteLine($"#DISPLAY: {MainGame.AllViewer.Count}");
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            Console.WriteLine("WebSocket ERROR : Not a WebSocket establishment request.");
                        }
                    }
                    else
                    {
                        if (context.Request.Path == "/cockpit")
                        {
                            Console.WriteLine("WebSocket /cockpit");
                            if (context.WebSockets.IsWebSocketRequest)
                            {
                                // on l'ajoute à notre simulation !
                                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                                Console.WriteLine("New COCKPIT!");
                                await MainGame.AddCockpit(webSocket);
                                Console.WriteLine($"#COCKPIT: {MainGame.AllCockpit.Count}");
                            }
                            else
                            {
                                context.Response.StatusCode = 400;
                                Console.WriteLine("WebSocket ERROR : Not a WebSocket establishment request.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Unknown WebSocket: {context.Request.Path}");
                            await next();
                        }
                    }
                }
            }); // app.Use
        } // Configure

    }
}