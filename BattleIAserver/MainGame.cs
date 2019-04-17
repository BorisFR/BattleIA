using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using BattleIA;

namespace BattleIAserver
{
    public static class MainGame
    {
        public static UInt16 MapWidth = 32;
        public static UInt16 MapHeight = 22;
        public static ushort StartEnergy = 100;

        private static UInt16 percentWall = 3;
        private static UInt16 percentEnergy = 5;

        /// <summary>
        /// Contient le terrain de simulation
        /// </summary>
        public static CaseState[,] TheMap = null;

        public static Random RND = new Random();

        /// <summary>
        /// Objet pour verrou lors de l'utilisation de la LIST car nous sommes en thread !
        /// </summary>
        private static Object lockList = new Object();

        /// <summary>
        /// L'ensemble des BOTs client connectés
        /// </summary>
        public static List<OneClient> AllBot = new List<OneClient>();

        /// <summary>
        /// Création d'un nouveau terrai de simulation, complet
        /// </summary>
        public static void InitNewMap()
        {
            TheMap = new CaseState[MapWidth, MapHeight];
            // les murs extérieurs
            for (int i = 0; i < MapWidth; i++)
            {
                TheMap[i, 0] = CaseState.Wall;
                TheMap[i, MapHeight - 1] = CaseState.Wall;
                for (int j = 0; j < MapHeight; j++)
                {
                    TheMap[0, j] = CaseState.Wall;
                    TheMap[MapWidth - 1, j] = CaseState.Wall;
                }
            }
            int availableCases = (MapWidth - 2) * (MapHeight - 2);
            int wallToPlace = percentWall * availableCases / 100;
            MapXY xy = new MapXY();
            // on ajoute quelques blocs à l'intérieur
            for (int n = 0; n < wallToPlace; n++)
            {
                xy = SearchEmptyCase();
                TheMap[xy.X, xy.Y] = CaseState.Wall;
            }
            // et on y place des cellules d'énergie
            RefuelMap();
        }

        /// <summary>
        /// On place des celulles d'énergie
        /// </summary>
        public static void RefuelMap()
        {
            int availableCases = (MapWidth - 2) * (MapHeight - 2);
            int energyToPlace = percentEnergy * availableCases / 100;
            int count = 0;
            for (int i = 0; i < MapWidth; i++)
            {
                for (int j = 0; j < MapHeight; j++)
                {
                    if (TheMap[i, j] == CaseState.Energy)
                        count++;
                }
            }
            energyToPlace -= count;

            MapXY xy = new MapXY();
            // et on y place des cellules d'énergie
            for (int n = 0; n < energyToPlace; n++)
            {
                xy = SearchEmptyCase();
                TheMap[xy.X, xy.Y] = CaseState.Energy;
                if (SimulatorThread.IsAlive)
                {
                    ViewerAddEnergy(xy.X, xy.Y);
                }
            }
        }

        /// <summary>
        /// Recherche une case vide dans le terrain de simulation
        /// </summary>
        /// <param name="x">Retourne le X de la case trouvée</param>
        /// <param name="y">Retourne le Y de la case trouvée</param>
        public static MapXY SearchEmptyCase()
        {
            bool ok = false;
            MapXY xy = new MapXY();
            do
            {
                xy.X = (byte)(RND.Next(MapWidth - 2) + 1);
                xy.Y = (byte)(RND.Next(MapHeight - 2) + 1);
                if (TheMap[xy.X, xy.Y] == CaseState.Empty)
                {
                    ok = true;
                }
            } while (!ok);
            return xy;
        }

        private static bool turnRunning = false;

        /// <summary>
        /// Exécute la simulation dans son ensemble !
        /// </summary>
        public static async void DoTurns()
        {
            if (turnRunning) return;
            turnRunning = true;
            System.Diagnostics.Debug.WriteLine("Démarrage de la simulation");
            while (turnRunning)
            {
                //System.Diagnostics.Debug.WriteLine("One turns...");
                OneClient[] bots = null;
                int count = 0;
                lock (lockList)
                {
                    count = AllBot.Count;
                    if (count > 0)
                    {
                        bots = new OneClient[count];
                        AllBot.CopyTo(bots);
                    }
                }
                if (count == 0)
                {
                    Console.WriteLine("Il n'y a plus de BOT, arrêt de la simulation.");
                    //Thread.Sleep(500);
                    turnRunning = false;
                }
                else
                {
                    for (int i = 0; i < bots.Length; i++)
                    {
                        await bots[i].StartNewTurn();
                        Thread.Sleep(500);
                    }
                    // on génère de l'énergie si nécessaire
                    MainGame.RefuelMap();
                }
            }
            Console.WriteLine("Fin de la simulation.");
        }

        private static Object lockListViewer = new Object();
        public static List<OneViewer> AllViewer = new List<OneViewer>();

        /// <summary>
        /// Un nouveau VIEWER de la simulation
        /// </summary>
        /// <param name="webSocket"></param>
        /// <returns></returns>
        public static async Task AddViewer(WebSocket webSocket)
        {
            // on en fait un vrai client
            OneViewer client = new OneViewer(webSocket);
            // on profite de faire le ménage au cas où
            List<OneViewer> toRemove = new List<OneViewer>();
            lock (lockListViewer)
            {
                foreach (OneViewer o in AllViewer)
                {
                    if (o.MustRemove)
                        toRemove.Add(o);
                }
                AllViewer.Add(client);
            };
            foreach (OneViewer o in toRemove)
                RemoveViewer(o.ClientGuid);
            Console.WriteLine($"#viewer: {AllViewer.Count}");
            // on se met à l'écoute des messages de ce client
            await client.WaitReceive();
            RemoveViewer(client.ClientGuid);
        }

        public static void RemoveViewer(Guid guid)
        {
            OneViewer toRemove = null;
            lock (lockListViewer)
            {
                foreach (OneViewer o in AllViewer)
                {
                    if (o.ClientGuid == guid)
                    {
                        toRemove = o;
                        break;
                    }
                }
                if (toRemove != null)
                    AllViewer.Remove(toRemove);
            }
            Console.WriteLine($"#viewer: {AllViewer.Count}");
        }

        /// <summary>
        /// Ajout d'un nouveau client avec sa websocket
        /// </summary>
        /// <param name="webSocket"></param>
        /// <returns></returns>
        public static async Task AddClient(WebSocket webSocket)
        {
            OneClient client = new OneClient(webSocket);
            List<OneClient> toRemove = new List<OneClient>();
            //Console.WriteLine("un peu de ménage");
            lock (lockList)
            {
                // au cas où, on en profite pour faire le ménage
                foreach (OneClient o in AllBot)
                {
                    if (o.State == BotState.Error || o.State == BotState.Disconnect)
                        toRemove.Add(o);
                }
                AllBot.Add(client);
            };
            // fin du ménage
            //Console.WriteLine("Do it!");
            foreach (OneClient o in toRemove)
                Remove(o.ClientGuid);
            Console.WriteLine($"#bots: {AllBot.Count}");

            /*Console.WriteLine("Starting thread");
            Thread t = new Thread(DoTurns);
            t.Start();*/

            // on se met à l'écoute des messages de ce client
            await client.WaitReceive();
            // arrivé ici, c'est que le client s'est déconnecté
            // on se retire de la liste des clients websocket
            Remove(client.ClientGuid);
        }

        public static Thread SimulatorThread = new Thread(DoTurns);

        public static void RunSimulator()
        {
            //Thread t = new Thread(DoTurns);
            if(SimulatorThread.IsAlive)
            {
                Console.WriteLine("La simulation est déjà en cours d'exécution.");
                return;
            }
            SimulatorThread = new Thread(DoTurns);
            SimulatorThread.Start();
        }

        public static void StopSimulator()
        {
            if (SimulatorThread.IsAlive)
            {
                turnRunning = false;
                //SimulatorThread.Abort();
            }
        }

        /// <summary>
        /// Retrait d'un client
        /// on a surement perdu sa conenction
        /// </summary>
        /// <param name="guid">l'id du client qu'il faut enlever</param>
        public static void Remove(Guid guid)
        {
            OneClient toRemove = null;
            lock (lockList)
            {
                foreach (OneClient o in AllBot)
                {
                    if (o.ClientGuid == guid)
                    {
                        toRemove = o;
                        break;
                    }
                }
                if (toRemove != null)
                    AllBot.Remove(toRemove);
            }
            if (toRemove != null)
            {
                RefreshViewer();
            }
            Console.WriteLine($"#clients: {AllBot.Count}");
        }

        /// <summary>
        /// Diffusion d'un message à l'ensemble des clients !
        /// Attention à ne pas boucler genre...
        /// je Broadcast un message quand j'en reçois un...
        /// Méthode "dangereuse" à peut-être supprimer
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static void Broadcast(string text)
        {
            lock (lockList)
            {
                foreach (OneClient o in AllBot)
                {
                    o.SendMessage(text);
                }
            }
        }

        public static void RefreshViewer()
        {
            lock (lockListViewer)
            {
                foreach (OneViewer o in AllViewer)
                {
                    o.SendMapInfo();
                }
            }
        }

        public static void ViewerMovePlayer(byte x1, byte y1, byte x2, byte y2)
        {
            lock (lockListViewer)
            {
                foreach (OneViewer o in AllViewer)
                {
                    o.SendMovePlayer(x1, y1, x2, y2);
                }
            }
        }

        public static void ViewerClearCase(byte x1, byte y1)
        {
            lock (lockListViewer)
            {
                foreach (OneViewer o in AllViewer)
                {
                    o.SendClearCase(x1, y1);
                }
            }
        }

        public static void ViewerAddEnergy(byte x1, byte y1)
        {
            lock (lockListViewer)
            {
                foreach (OneViewer o in AllViewer)
                {
                    o.SendAddEnergy(x1, y1);
                }
            }
        }

        public static void ViewerPlayerShield(byte x1, byte y1, byte s)
        {
            lock (lockListViewer)
            {
                foreach (OneViewer o in AllViewer)
                {
                    o.SendPlayerShield(x1, y1, s);
                }
            }
        }

        public static void ViewerPlayerCloak(byte x1, byte y1, byte s)
        {
            lock (lockListViewer)
            {
                foreach (OneViewer o in AllViewer)
                {
                    o.SendPlayerCloak(x1, y1, s);
                }
            }
        }

    }
}
