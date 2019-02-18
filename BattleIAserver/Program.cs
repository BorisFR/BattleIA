using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace BattleIAserver
{
    class Program
    {
        static void Main(string[] args)
        {

            //var ConsOut = Console.Out;  //Save the reference to the old out value (The terminal)
            //Console.SetOut(new StreamWriter(Stream.Null)); //Remove console output

            //Console.WriteLine(Directory.GetCurrentDirectory());
            var host = new WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "WebPages"))
            .UseStartup<Startup>()
            .Build();                     //Modify the building per your needs

            host.Start();                     //Start server non-blocking

            //Console.SetOut(ConsOut);          //Restore output

            //Regular console code
            while (true)
            {
                Console.WriteLine(Console.ReadLine());
            }
        }
    }
}