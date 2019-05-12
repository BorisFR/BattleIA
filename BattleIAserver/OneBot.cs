using BattleIA;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace BattleIAserver
{
    public class OneBot
    {
        public BotState State { get; private set; } = BotState.Undefined;
        public Guid ClientGuid { get; }
        private WebSocket webSocket = null;
        public bool IsEnd { get; private set; } = false;


        public Bot bot = new Bot();

        /// <summary>
        /// Numéro de tour dans le jeu
        /// </summary>
        private UInt16 turn = 0;

        public OneBot(WebSocket webSocket)
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

            //Console.WriteLine("First, listen for GUID");
            // 1 - on attend la première data du client
            // qui doit etre son GUID

            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = null;
            try
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            catch (Exception err)
            {
                Console.WriteLine($"[ERROR] {err.Message}");
                //Console.WriteLine($"[ERROR] {err.Message}");
                IsEnd = true;
                State = BotState.Disconnect;
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Error waiting data", CancellationToken.None);
                }
                catch (Exception) { }
                return;
            }
            while (!result.CloseStatus.HasValue)
            {

                // 2 - réception du GUID

                if (State == BotState.WaitingGUID)
                {
                    if (result.Count != 38 && result.Count != 36 && result.Count != 32) // pas de GUID ?
                    {
                        IsEnd = true;
                        State = BotState.Disconnect;
                        if (result.Count > 0)
                        {
                            var temp = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                            Console.WriteLine($"[ERROR GUID] {temp}");
                        }
                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "No GUID", CancellationToken.None);
                        return;
                    }
                    var text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    // check que l'on a reçu un GUID
                    if (Guid.TryParse(text, out bot.GUID))
                    {
                        // et qu'il soit ok !
                        bot.Name = bot.GUID.ToString();
                        MapXY xy = MainGame.SearchEmptyCase();
                        MainGame.TheMap[xy.X, xy.Y] = CaseState.Ennemy;
                        bot.X = xy.X;
                        bot.Y = xy.Y;
                        bot.Energy = MainGame.Settings.EnergyStart;
                        bot.Score = 0;
                        Console.WriteLine($"[NEW BOT] {bot.GUID} @ {xy.X}/{xy.Y}");

                        State = BotState.Ready;
                        await SendMessage("OK");

                        MainGame.RefreshViewer();

                        SendPositionToCockpit();
                        //await StartNewTurn();
                    }
                    else
                    {
                        IsEnd = true;
                        State = BotState.Disconnect;
                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, $"[{text}] is not a GUID", CancellationToken.None);
                        return;
                    }
                } // réception GUID
                else
                { // exécute une action
                    if (result.Count < 1)
                    {
                        MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                        IsEnd = true;
                        State = BotState.Disconnect;
                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Missing data in answer", CancellationToken.None);
                        return;
                    }
                    // On reçoit une commande du BOT
                    switch (State)
                    {
                        case BotState.WaitingAnswerD: // le niveau de détection désiré pour ce tour
                            string command = System.Text.Encoding.UTF8.GetString(buffer, 0, 1);
                            if (command != "D")
                            {
                                MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                                IsEnd = true;
                                State = BotState.Disconnect;
                                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, $"[ERROR] Not the right answer, waiting D#, receive {command}", CancellationToken.None);
                                return;
                            }
                            // commande D sans niveau...
                            if (result.Count < 1)
                            {
                                MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                                IsEnd = true;
                                State = BotState.Disconnect;
                                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Missing data in answer 'D'", CancellationToken.None);
                                return;
                            }
                            // do a scan of size value and send answer
                            byte distance = buffer[1];
                            Console.WriteLine($"{bot.Name}: scan distance={distance}");
                            if (distance > 0)
                            {
                                if (distance < bot.Energy)
                                {
                                    bot.Energy -= distance;
                                    await SendChangeInfo();
                                    await DoScan(distance);
                                }
                                else
                                {
                                    bot.Energy = 0;
                                    State = BotState.IsDead;
                                    await SendChangeInfo();
                                    await SendDead();
                                }
                            }
                            else 
                            {
                                // ici cas simple du scan à 0, pas de conso d'énergie
                                await DoScan(distance);
                            }
                            break;
                        
                        case BotState.WaitingAction: // l'action a effectuer
                            BotAction action = (BotAction)buffer[0];
                            switch (action)
                            {
                                case BotAction.None: // None
                                    State = BotState.Ready;
                                    Console.WriteLine($"Bot {bot.Name} do nothing");
                                    await SendMessage("OK");
                                    break;
                                case BotAction.Move: // move
                                    if (result.Count < 2)
                                    {
                                        MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                                        IsEnd = true;
                                        State = BotState.Disconnect;
                                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Missing data in answer 'M'", CancellationToken.None);
                                        return;
                                    }
                                    byte direction = buffer[1];
                                    Console.WriteLine($"Bot {bot.Name} moving direction {(MoveDirection)direction}");
                                    await DoMove((MoveDirection)direction);
                                    State = BotState.Ready;
                                    await SendMessage("OK");
                                    break;
                                case BotAction.ShieldLevel: // shield
                                    if (result.Count < 3)
                                    {
                                        MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                                        IsEnd = true;
                                        State = BotState.Disconnect;
                                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Missing data in answer 'S'", CancellationToken.None);
                                        return;
                                    }
                                    UInt16 shieldLevel = (UInt16)(buffer[1] + (buffer[2] << 8));
                                    Console.WriteLine($"Bot {bot.Name} activate shield level {shieldLevel}");
                                    bot.Energy += bot.ShieldLevel;
                                    bot.ShieldLevel = shieldLevel;
                                    MainGame.ViewerPlayerShield(bot.X, bot.Y, (byte)bot.ShieldLevel);
                                    if (shieldLevel > bot.Energy)
                                    {
                                        bot.ShieldLevel = 0;
                                        bot.Energy = 0;
                                        await SendChangeInfo();
                                        await SendDead();
                                    }
                                    else
                                        bot.Energy -= shieldLevel;
                                    State = BotState.Ready;
                                    await SendMessage("OK");
                                    break;
                                case BotAction.CloakLevel: // cloak
                                    if (result.Count < 3)
                                    {
                                        MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                                        IsEnd = true;
                                        State = BotState.Disconnect;
                                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Missing data in answer 'C'", CancellationToken.None);
                                        return;
                                    }
                                    UInt16 cloakLevel = (UInt16)(buffer[1] + (buffer[2] << 8));
                                    Console.WriteLine($"Bot {bot.Name} activate cloak level {cloakLevel}");
                                    bot.Energy += bot.CloakLevel;
                                    bot.CloakLevel = cloakLevel;
                                    MainGame.ViewerPlayerCloak(bot.X, bot.Y, (byte)bot.CloakLevel);
                                    if (cloakLevel > bot.Energy)
                                    {
                                        bot.CloakLevel = 0;
                                        bot.Energy = 0;
                                        await SendChangeInfo();
                                        await SendDead();
                                    }
                                    else
                                        bot.Energy -= cloakLevel;
                                    State = BotState.Ready;
                                    await SendMessage("OK");
                                    break;
                                case BotAction.Shoot:
                                    if (result.Count < 2)
                                    {
                                        MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                                        IsEnd = true;
                                        State = BotState.Disconnect;
                                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Missing data in answer 'F'", CancellationToken.None);
                                        return;
                                    }
                                    byte fireDirection = buffer[1];
                                    Console.WriteLine($"Bot {bot.Name} shoot in direction {fireDirection}");
                                    // TODO: effectuer le tir :)
                                    if (bot.Energy > MainGame.Settings.EnergyLostShot)
                                        bot.Energy -= MainGame.Settings.EnergyLostShot;
                                    else
                                        bot.Energy = 0;
                                    if (bot.Energy == 0)
                                    {
                                        await SendChangeInfo();
                                        await SendDead();
                                    }
                                    State = BotState.Ready;
                                    await SendMessage("OK");
                                    break;
                                default:
                                    Console.WriteLine($"[ERROR] lost with command {action} for state Action");
                                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                                    IsEnd = true;
                                    State = BotState.Disconnect;
                                    await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, $"[ERROR] lost with command {action} for state Action", CancellationToken.None);
                                    return;
                            }
                            break;
                        default:
                            string cmd = System.Text.Encoding.UTF8.GetString(buffer, 0, 1);
                            // on reçoit le nom du BOT
                            if (cmd == "N" && result.Count > 1)
                            {
                                bot.Name = System.Text.Encoding.UTF8.GetString(buffer, 1, result.Count - 1);
                                Console.WriteLine($"Le BOT {bot.GUID} se nomme {bot.Name}");
                                State = BotState.Ready;
                                await SendMessage("OK");
                                MainGame.SendCockpitInfo(bot.GUID, "N" + bot.Name);
                                break;
                            }
                            Console.WriteLine($"[ERROR] lost with state {State}");
                            MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                            IsEnd = true;
                            State = BotState.Disconnect;
                            await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, $"[ERROR] lost with state {State}", CancellationToken.None);
                            return;
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
                //result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                try
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                catch (Exception err)
                {
                    Console.WriteLine($"[ERROR] {err.Message}");
                    if (MainGame.TheMap[bot.X, bot.Y] == CaseState.Ennemy)
                        MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                    IsEnd = true;
                    State = BotState.Disconnect;
                    try
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Error waiting data", CancellationToken.None);
                    }
                    catch (Exception) { }
                    return;
                }
            }
            if (MainGame.TheMap[bot.X, bot.Y] == CaseState.Ennemy)
                MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            IsEnd = true;
            State = BotState.Disconnect;
        }

        public async Task SendMessage(String text)
        {
            if (IsEnd) return;
            System.Diagnostics.Debug.WriteLine($"Sending {text} to {bot.GUID}");
            var buffer = System.Text.Encoding.UTF8.GetBytes(text);
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception err)
            {
                if (MainGame.TheMap[bot.X, bot.Y] == CaseState.Ennemy)
                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                Console.WriteLine($"[ERROR] {err.Message}");
                State = BotState.Error;
            }
        }

        /// <summary>
        /// Nouveau tour de simulation pour ce BOT
        /// </summary>
        /// <returns></returns>
        public async Task StartNewTurn()
        {
            if (IsEnd) return; // c'est déjà fini pour lui...
            if (State != BotState.Ready) return; // pas normal...
            bot.Score += MainGame.Settings.PointByTurn;
            if (turn > 0) // pas le premier tour, donc on applique la consommation d'énergie
            {
                if (bot.Energy > MainGame.Settings.EnergyLostByTurn)
                    bot.Energy -= MainGame.Settings.EnergyLostByTurn;
                else
                    bot.Energy = 0;
                if (bot.ShieldLevel > 0)
                    if (bot.Energy > MainGame.Settings.EnergyLostByShield)
                        bot.Energy -= MainGame.Settings.EnergyLostByShield;
                    else
                        bot.Energy = 0;
                if (bot.CloakLevel > 0)
                    if (bot.Energy > MainGame.Settings.EnergyLostByCloak)
                        bot.Energy -= MainGame.Settings.EnergyLostByCloak;
                    else
                        bot.Energy = 0;
            }
            // reste-t-il de l'énergie ?
            if (bot.Energy == 0)
            {
                await SendChangeInfo();
                await SendDead();
                return;
            }
            // activation du BOT pour connaitre le niveau de détection
            turn++;
            var buffer = new byte[(byte)MessageSize.Turn];
            buffer[0] = System.Text.Encoding.ASCII.GetBytes("T")[0];
            buffer[1] = (byte)turn;
            buffer[2] = (byte)(turn >> 8);
            buffer[3] = (byte)bot.Energy;
            buffer[4] = (byte)(bot.Energy >> 8);
            buffer[5] = (byte)bot.ShieldLevel;
            buffer[6] = (byte)(bot.ShieldLevel >> 8);
            buffer[7] = (byte)bot.CloakLevel;
            buffer[8] = (byte)(bot.CloakLevel >> 8);
            try
            {
                State = BotState.WaitingAnswerD;
                System.Diagnostics.Debug.WriteLine($"Sending 'T' to {bot.GUID}");
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception err)
            {
                if (MainGame.TheMap[bot.X, bot.Y] == CaseState.Ennemy)
                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                Console.WriteLine($"[ERROR] {err.Message}");
                State = BotState.Error;
            }
        }

        public async Task SendChangeInfo()
        {
            if (IsEnd) return;
            var buffer = new byte[(byte)MessageSize.Change + 2];
            buffer[0] = System.Text.Encoding.ASCII.GetBytes("C")[0];
            buffer[1] = (byte)bot.Energy;
            buffer[2] = (byte)(bot.Energy >> 8);
            buffer[3] = (byte)bot.ShieldLevel;
            buffer[4] = (byte)(bot.ShieldLevel >> 8);
            buffer[5] = (byte)bot.CloakLevel;
            buffer[6] = (byte)(bot.CloakLevel >> 8);
            buffer[7] = (byte)bot.Score;
            buffer[8] = (byte)(bot.Score >> 8);
            try
            {
                System.Diagnostics.Debug.WriteLine($"Sending 'C' to {bot.GUID}");
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                MainGame.SendCockpitInfo(bot.GUID, new ArraySegment<byte>(buffer, 0, buffer.Length)); 
            }
            catch (Exception err)
            {
                if (MainGame.TheMap[bot.X, bot.Y] == CaseState.Ennemy)
                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                Console.WriteLine($"[ERROR] {err.Message}");
                State = BotState.Error;
            }
        }

        public async Task SendDead()
        {
            if (IsEnd) return;
            State = BotState.IsDead;
            var buffer = new byte[(byte)MessageSize.Dead];
            buffer[0] = System.Text.Encoding.ASCII.GetBytes("D")[0];
            try
            {
                Console.WriteLine($"Bot {bot.Name} is dead!");
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception err)
            {
                Console.WriteLine($"[ERROR] {err.Message}");
                State = BotState.Error;
            }
            if (MainGame.TheMap[bot.X, bot.Y] == CaseState.Ennemy)
                MainGame.TheMap[bot.X, bot.Y] = CaseState.Energy;
            MainGame.SendCockpitInfo(bot.GUID, new ArraySegment<byte>(buffer, 0, buffer.Length));
        }

        public async Task DoScan(byte size)
        {
            if (IsEnd) return;
            int distance = 2 * size + 1;
            var buffer = new byte[2 + distance * distance];
            buffer[0] = System.Text.Encoding.ASCII.GetBytes("I")[0];
            buffer[1] = (byte)(distance);
            UInt16 posByte = 2;
            int posY = bot.Y + size;
            for (UInt16 j = 0; j < (2 * size + 1); j++)
            {
                int posX = bot.X - size;
                for (UInt16 i = 0; i < (2 * size + 1); i++)
                {
                    if (posX < 0 || posX >= MainGame.Settings.MapWidth || posY < 0 || posY >= MainGame.Settings.MapHeight)
                    {
                        buffer[posByte++] = (byte)CaseState.Wall;
                    }
                    else
                    {
                        CaseState cs = MainGame.TheMap[posX, posY];
                        switch (cs)
                        {
                            case CaseState.Empty:
                            case CaseState.Wall:
                            case CaseState.Energy:
                                buffer[posByte++] = (byte)cs;
                                break;
                            case CaseState.Ennemy:
                                buffer[posByte++] = (byte)MainGame.IsEnnemyVisible(bot.X, bot.Y, posX, posY);
                                break;
                            default:
                                buffer[posByte++] = (byte)cs;
                                break;
                        }
                    }
                    posX++;
                }
                posY--;
            }
            try
            {
                State = BotState.WaitingAction;
                System.Diagnostics.Debug.WriteLine($"Sending 'I' to {bot.GUID}");
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                if(size > 0)
                    MainGame.SendCockpitInfo(bot.GUID, new ArraySegment<byte>(buffer, 0, buffer.Length));
            }
            catch (Exception err)
            {
                if (MainGame.TheMap[bot.X, bot.Y] == CaseState.Ennemy)
                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                Console.WriteLine($"[ERROR] {err.Message}");
                State = BotState.Error;
            }

        }

        public async Task<UInt16> IsHit()
        {
            Console.WriteLine($"Bot {bot.Name} a été tamponné");
            if (bot.ShieldLevel > 0)
            {
                // le bouclier absorbe le choc
                bot.ShieldLevel--;
                MainGame.ViewerPlayerShield(bot.X, bot.Y, bot.ShieldLevel);
            }
            else
            {
                // pas de bouclier, perte directe d'énergie !
                if (bot.Energy > MainGame.Settings.EnergyLostContactEnemy)
                    bot.Energy -= MainGame.Settings.EnergyLostContactEnemy;
                else
                    bot.Energy = 0;
            }
            // perte du champ occultant
            if (bot.CloakLevel > 0)
            {
                bot.CloakLevel = 0;
                MainGame.ViewerPlayerCloak(bot.X, bot.Y, bot.CloakLevel);
            }
            await SendChangeInfo();
            if (bot.Energy == 0)
            {
                await SendDead();
                return MainGame.Settings.PointByEnnemyKill;
            }
            return 0;
        }

        public void SendPositionToCockpit()
        {
            if (IsEnd) return;
            var buffer = new byte[(byte)MessageSize.Position];
            buffer[0] = System.Text.Encoding.ASCII.GetBytes("P")[0];
            buffer[1] = (byte)bot.X;
            buffer[2] = (byte)bot.Y;
            MainGame.SendCockpitInfo(bot.GUID, new ArraySegment<byte>(buffer, 0, buffer.Length));
        }

        public async Task DoMove(MoveDirection direction)
        {
            if (bot.Energy > MainGame.Settings.EnergyLostByMove)
                bot.Energy -= MainGame.Settings.EnergyLostByMove;
            else
                bot.Energy = 0;
            int x = 0;
            int y = 0;
            switch (direction)
            {// TODO: check if it is ok for east/west. Ok for north/south
                // pour l'instant, on se déplace haut/bas/gauche/droite, pas de diagonale
                case MoveDirection.North: y = 1; break;
                case MoveDirection.South: y = -1; break;
                case MoveDirection.East: x = -1; break;
                case MoveDirection.West: x = 1; break;
                /*case MoveDirection.NorthWest: y = 1; x = 1; break;
                case MoveDirection.NorthEast: y = 1; x = -1; break;
                case MoveDirection.SouthWest: y = -1; x = 1; break;
                case MoveDirection.SouthEast: y = -1; x = -1; break;
                */
            }
            switch (MainGame.TheMap[bot.X + x, bot.Y + y])
            {
                case CaseState.Empty:
                    MainGame.ViewerMovePlayer(bot.X, bot.Y, (byte)(bot.X + x), (byte)(bot.Y + y));
                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                    bot.X = (byte)(bot.X + x);
                    bot.Y = (byte)(bot.Y + y);
                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Ennemy;
                    SendPositionToCockpit();
                    break;
                case CaseState.Energy:
                    MainGame.ViewerClearCase((byte)(bot.X + x), (byte)(bot.Y + y));
                    MainGame.ViewerMovePlayer(bot.X, bot.Y, (byte)(bot.X + x), (byte)(bot.Y + y));
                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Empty;
                    bot.X = (byte)(bot.X + x);
                    bot.Y = (byte)(bot.Y + y);
                    MainGame.TheMap[bot.X, bot.Y] = CaseState.Ennemy;
                    UInt16 temp = (UInt16)(MainGame.RND.Next(50) + 1);
                    bot.Energy += temp;
                    Console.WriteLine($"Bot {bot.Name} win energy: {temp}");
                    bot.Score += MainGame.Settings.PointByEnergyFound;
                    //MainGame.RefreshViewer();
                    SendPositionToCockpit();
                    break;
                case CaseState.Ennemy: // on tamponne un bot adverse
                    if (bot.ShieldLevel > 0)
                    {
                        bot.ShieldLevel--;
                        MainGame.ViewerPlayerShield(bot.X, bot.Y, bot.ShieldLevel);
                    }
                    else
                    {
                        if (bot.Energy > MainGame.Settings.EnergyLostContactEnemy)
                            bot.Energy -= MainGame.Settings.EnergyLostContactEnemy;
                        else
                            bot.Energy = 0;
                    }
                    // perte du champ occultant
                    if (bot.CloakLevel > 0)
                    {
                        bot.CloakLevel = 0;
                        MainGame.ViewerPlayerCloak(bot.X, bot.Y, bot.CloakLevel);
                    }
                    bot.Score += MainGame.Settings.PointByEnnemyTouch;
                    Console.WriteLine($"Bot {bot.Name} tamponne un bot ennemi !");
                    TouchEnemy((UInt16)(bot.X + x), (UInt16)(bot.Y + y));
                    break;
                case CaseState.Wall:
                    if (bot.ShieldLevel > 0)
                    {
                        bot.ShieldLevel--;
                        MainGame.ViewerPlayerShield(bot.X, bot.Y, bot.ShieldLevel);
                    }
                    else
                    {
                        if (bot.Energy > MainGame.Settings.EnergyLostContactWall)
                            bot.Energy -= MainGame.Settings.EnergyLostContactWall;
                        else
                            bot.Energy = 0;
                    }
                    // perte du champ occultant
                    if (bot.CloakLevel > 0)
                    {
                        bot.CloakLevel = 0;
                        MainGame.ViewerPlayerCloak(bot.X, bot.Y, bot.CloakLevel);
                    }
                    break;
            }
            await SendChangeInfo();
            if (bot.Energy == 0)
            {
                await SendDead();
            }
        } // DoMove

        private async void TouchEnemy(UInt16 x, UInt16 y)
        {
            foreach(OneBot client in MainGame.AllBot)
            {
                if(client.bot.X == x && client.bot.Y == y)
                {
                    var pts = await client.IsHit();
                    bot.Score += pts;
                    return;
                }
            }
        }
    }
}