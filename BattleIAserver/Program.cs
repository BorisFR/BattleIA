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
            Console.WriteLine($"ContentRoot: {pathToContentRoot}");

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

            //Regular console code
            Console.WriteLine("Press [ENTER] to exit.");
            //while (true)
            {
                Console.WriteLine(Console.ReadLine());
            }
            host.StopAsync();
        }
    }
}