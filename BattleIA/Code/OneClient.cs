using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace BattleIA
{
    public class OneClient
    {
        public BotState State { get; private set; } = BotState.Undefined;
        public Guid ClientGuid { get; }
        private WebSocket webSocket = null;
        public bool IsEnd { get; private set; } = false;


        public Bot bot = new Bot();

        /// <summary>
        /// Numéro de tour dans le jeu
        /// </summary>
        private UInt32 turn = 0;

        public OneClient(WebSocket webSocket)
        {
            this.webSocket = webSocket;
            ClientGuid = Guid.NewGuid();
            State = BotState.WaitingGUID;
        }

        /// <summary>
        /// réception des messages
        /// fin si le client se déconnecte
        /// </summary>
        /// <returns></returns>
        public async Task WaitReceive()
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                if (State == BotState.WaitingGUID)
                {
                    if (result.Count != 38 && result.Count != 36 && result.Count != 32) // pas de GUID ?
                    {
                        IsEnd = true;
                        State = BotState.Disconnect;
                        if (result.Count > 0)
                        {
                            var temp = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                            System.Diagnostics.Debug.WriteLine($"[ERROR GUID] {temp}");
                        }
                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "No GUID", CancellationToken.None);
                        return;
                    }
                    var text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    // check que l'on a reçu un GUID
                    if (Guid.TryParse(text, out bot.GUID))
                    {
                        // et qu'il soit ok !
                        System.Diagnostics.Debug.WriteLine($"[NEW CLIENT] {bot.GUID}");
                        UInt16 x, y;
                        MainGame.SearchEmptyCase(out x, out y);
                        MainGame.TheMap[x, y] = CaseState.Ennemy;

                        State = BotState.Ready;
                        await SendMessage("OK");
                    }
                    else
                    {
                        IsEnd = true;
                        State = BotState.Disconnect;
                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, $"[{text}] is not a GUID", CancellationToken.None);
                        return;
                    }
                }
                else
                {
                    if (result.Count < 2)
                    {
                        IsEnd = true;
                        State = BotState.Disconnect;
                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Missing data in answer", CancellationToken.None);
                        return;
                    }
                    string command = System.Text.Encoding.UTF8.GetString(buffer, 0, 1);
                    byte value = buffer[1];
                    switch (State)
                    {
                        case BotState.WaitingAnswerD:
                            if (command != "D")
                            {
                                IsEnd = true;
                                State = BotState.Disconnect;
                                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, $"[ERROR] Not the right answer, waiting D#, receive {command}", CancellationToken.None);
                                return;
                            }
                            // do a scan of size value and send answer
                            await DoScan(value);
                            break;
                        case BotState.WaitingAction:
                            switch (command)
                            {
                                case "M": // move
                                    State = BotState.Ready;
                                    await SendMessage("OK");
                                    break;
                                case "S": // shield
                                    bot.Energy += bot.ShieldLevel;
                                    bot.ShieldLevel = value;
                                    if (value > bot.Energy)
                                        bot.Energy = 0;
                                    else
                                        bot.Energy -= value;
                                    State = BotState.Ready;
                                    await SendMessage("OK");
                                    break;
                                case "C": // cloack
                                    bot.Energy += bot.CloackLevel;
                                    bot.CloackLevel = value;
                                    if (value > bot.Energy)
                                        bot.Energy = 0;
                                    else
                                        bot.Energy -= value;
                                    State = BotState.Ready;
                                    await SendMessage("OK");
                                    break;
                                default:
                                    System.Diagnostics.Debug.WriteLine($"[ERROR] lost with command {command} for state Action");
                                    break;
                            }
                            break;
                        default:
                            System.Diagnostics.Debug.WriteLine($"[ERROR] lost with state {State}");
                            break;
                    }

                    /*var text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (text.IndexOf(ClientGuid.ToString()) < 0)
                        MainGame.Broadcast(text + " " + ClientGuid.ToString());
                    */

                    /*text = text + " et " + text + " :)";
                    buffer = System.Text.Encoding.UTF8.GetBytes(text);
                    await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    */
                }
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            IsEnd = true;
            State = BotState.Disconnect;
        }

        public async Task SendMessage(String text)
        {
            if (IsEnd) return;
            var buffer = System.Text.Encoding.UTF8.GetBytes(text);
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception err)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {err.Message}");
                State = BotState.Error;
            }
        }

        public async Task StartNewTurn()
        {
            if (IsEnd) return;
            if (turn > 0)
            {
                if (bot.Energy > 0)
                    bot.Energy--;
                if (bot.ShieldLevel > 0)
                    if (bot.Energy > 0)
                        bot.Energy--;
                if (bot.CloackLevel > 0)
                    if (bot.Energy > 0)
                        bot.Energy--;
            }
            turn++;
            var buffer = new byte[9];
            buffer[0] = System.Text.Encoding.ASCII.GetBytes("T")[0];
            buffer[1] = (byte)turn;
            buffer[2] = (byte)(turn >> 8);
            buffer[3] = (byte)bot.Energy;
            buffer[4] = (byte)(bot.Energy >> 8);
            buffer[5] = (byte)bot.ShieldLevel;
            buffer[6] = (byte)(bot.ShieldLevel >> 8);
            buffer[7] = (byte)bot.CloackLevel;
            buffer[8] = (byte)(bot.CloackLevel >> 8);
            try
            {
                State = BotState.WaitingAnswerD;
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception err)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {err.Message}");
                State = BotState.Error;
            }
        }

        public async Task DoScan(byte size)
        {
            if (IsEnd) return;
            var buffer = new byte[2 + size * size];
            buffer[0] = System.Text.Encoding.ASCII.GetBytes("I")[0];
            buffer[1] = size;
            UInt16 posByte = 2;
            int posX = bot.X - size;
            for (UInt16 i = 0; i < size; i++)
            {
                if (posX < 0 || posX >= MainGame.MapWidth)
                {
                    buffer[posByte++] = (byte)CaseState.Wall;
                }
                else
                {
                    int posY = bot.Y - size;
                    for (UInt16 j = 0; j < size; j++)
                    {
                        if (posY < 0 || posY >= MainGame.MapHeight)
                        {
                            buffer[posByte++] = (byte)CaseState.Wall;
                        }
                        else
                        {
                            buffer[posByte++] = (byte)MainGame.TheMap[posX, posY];
                        }
                    }
                }
                posX++;
            }

            try
            {
                State = BotState.WaitingAction;
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception err)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {err.Message}");
                State = BotState.Error;
            }

        }

    }
}
