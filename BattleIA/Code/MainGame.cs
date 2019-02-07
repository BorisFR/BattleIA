using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace BattleIA
{
    public static class MainGame
    {
        public static UInt16 MapWidth = 30;
        public static UInt16 MapHeight = 20;
        private static UInt16 percentEnergy = 10;
        public static CaseState[,] TheMap = null;

        public static Random RND = new Random();

        private static Object lockList = new Object();
        private static List<OneClient> allClients = new List<OneClient>();

        public static void InitNewMap()
        {

            TheMap = new CaseState[MapWidth, MapHeight];
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
            int energyToPlace = percentEnergy * availableCases / 100;
            for (int n = 0; n < energyToPlace; n++)
            {
                UInt16 x, y;
                SearchEmptyCase(out x, out y);
                TheMap[x, y] = CaseState.Energy;
            }
        }

        public static void SearchEmptyCase(out UInt16 x, out UInt16 y)
        {
            bool ok = false;
            do
            {
                x = (UInt16)(RND.Next(MapWidth - 2) + 1);
                y = (UInt16)(RND.Next(MapHeight - 2) + 1);
                if (TheMap[x, y] == CaseState.Empty)
                {
                    ok = true;
                }
            } while (!ok);
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
            lock (lockList)
            {
                // au cas où, on en profite pour faire le ménage
                foreach (OneClient o in allClients)
                {
                    if (o.State == BotState.Error || o.State == BotState.Disconnect)
                        toRemove.Add(o);
                }
                allClients.Add(client);
            };
            // fin du ménage
            foreach (OneClient o in toRemove)
                Remove(o.ClientGuid);
            System.Diagnostics.Debug.WriteLine($"#clients: {allClients.Count}");
            // on se met à l'écoute des messages de ce client
            await client.WaitReceive();
            // arrivé ici, c'est que le client s'est déconnecté
            // on se retire de la liste des clients websocket
            Remove(client.ClientGuid);
        }

        /// <summary>
        /// Retrait d'un client
        /// on a surement perdu sa conenction
        /// </summary>
        /// <param name="guid">l'id du client qu'il faut enlever</param>
        public static void Remove(Guid guid)
        {
            lock (lockList)
            {
                OneClient toRemove = null;
                foreach (OneClient o in allClients)
                {
                    if (o.ClientGuid == guid)
                    {
                        toRemove = o;
                        break;
                    }
                }
                if (toRemove != null)
                    allClients.Remove(toRemove);
            }
            System.Diagnostics.Debug.WriteLine($"#clients: {allClients.Count}");
        }

        /// <summary>
        /// Diffusion d'un message à l'ensemble des clients !
        /// Attention à ne pas boucler genre...
        /// je Broadcast un message quand j'en reçois un...
        /// Méthode "dangereuse" à peut-être supprimer
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static async Task Broadcast(string text)
        {
            lock (lockList)
            {
                foreach (OneClient o in allClients)
                {
                    o.SendMessage(text);
                }
            }
        }

    }
}
