using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SampleBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            DoWork().GetAwaiter().GetResult();
            Console.WriteLine("Bye");
        }

        static async Task DoWork()
        {
            var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri("wss://localhost:44367/ia"), CancellationToken.None);
            Guid guid = Guid.NewGuid();
            var bytes = Encoding.UTF8.GetBytes(guid.ToString());
            await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

            var buffer = new byte[1024 * 4];
            while (true)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    if (result.Count > 1)
                    {
                        string command = System.Text.Encoding.UTF8.GetString(buffer, 0, 1);
                        byte value = buffer[1];
                        switch (command)
                        {
                            case "O": // OK, rien à faire
                                break;
                            case "T": // nouveau tour, attend le niveau de détection désiré
                                break;
                            case "I": // info sur détection, attend l'action à effectuer
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("[ERROR] " + Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                }

                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"End with code {result.CloseStatus}: {result.CloseStatusDescription}");
                    await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    break;
                }
            }

        }
    }
}
