using System;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace BattleIAserver
{
    class Program
    {
        static void Main(string[] args)
        {

            //var ConsOut = Console.Out;  //Save the reference to the old out value (The terminal)
            //Console.SetOut(new StreamWriter(Stream.Null)); //Remove console output

            MainGame.InitNewMap();

            //var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
            var currentDir = Directory.GetCurrentDirectory();
            var pathToContentRoot = Path.Combine(currentDir, "WebPages");
            //Console.WriteLine($"ContentRoot: {pathToContentRoot}");

            //var kso = new KestrelServerOptions();
            //kso.ListenLocalhost(2626);

            var host = new WebHostBuilder()
            .UseContentRoot(pathToContentRoot)
            .UseKestrel()
            .UseStartup<Startup>()
            .ConfigureKestrel((context, options) => { options.ListenAnyIP(2626); })
            .Build();                     //Modify the building per your needs

            //host.Run();
            host.Start();                     //Start server non-blocking

            //Console.SetOut(ConsOut);          //Restore output

            ShowHelp();
            bool exit = false;
            while (!exit)
            {
                Console.Write(">");
                var key = Console.ReadKey(true);
                //string command = Console.ReadLine().Trim().ToLower();
                switch (key.KeyChar.ToString().ToLower())
                {
                    case "h":
                        ShowHelp();
                        break;
                    case "q":
                        Console.WriteLine("Quit");
                        if (MainGame.AllBot.Count>0)
                        {
                            Console.WriteLine("Impossible car il y a au moins 1 BOT de connecté.");
                        } else
                        {
                            if(MainGame.AllViewer.Count > 0)
                            {
                                Console.WriteLine("Impossible car il y a au moins 1 VIEWER de connecté.");
                            } else
                            {
                                exit = true;
                            }
                        }
                        break;
                    case "g":
                        Console.WriteLine("GO!");
                        MainGame.RunSimulator();
                        break;
                    case "s":
                        Console.WriteLine("Stop");
                        MainGame.StopSimulator();
                        break;
                    case "x": // debug stuff to view shield
                        foreach (OneClient x in MainGame.AllBot)
                        {
                            x.bot.ShieldLevel++;
                            if (x.bot.ShieldLevel > 10)
                                x.bot.ShieldLevel = 0;
                            MainGame.ViewerPlayerShield(x.bot.X, x.bot.Y, x.bot.ShieldLevel);
                        }
                        break;
                    case "w": // debug stuff to view cloak
                        foreach (OneClient x in MainGame.AllBot)
                        {
                            x.bot.CloakLevel++;
                            if (x.bot.CloakLevel > 10)
                                x.bot.CloakLevel = 0;
                            MainGame.ViewerPlayerCloak(x.bot.X, x.bot.Y, x.bot.CloakLevel);
                        }
                        break;
                }
            }
            host.StopAsync();
        }

        public static void ShowHelp()
        {
            Console.WriteLine("Help");
            Console.WriteLine("h\t Affiche cette liste de commandes");
            Console.WriteLine("q\t Terminer le programme");
            Console.WriteLine("g\t Exécute la simulation");
            Console.WriteLine("s\t Arrêt de la simulation");
        }
    }
}