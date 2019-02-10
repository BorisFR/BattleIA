using System;
using BattleIA;

namespace SampleBot
{
    public class MyIA
    {
        public MyIA()
        {
            // setup
        }

        public byte GetScanSurface()
        {
            return 10;
        }

        public void AreaInformation(byte distance, byte[] informations)
        {
            Console.WriteLine($"Area: {distance}");
            int index = 0;
            for (int i = 0; i < distance; i++)
            {
                for (int j = 0; j < distance; j++)
                {
                    Console.Write(informations[index++]);
                }
                Console.WriteLine();
            }
        }

        public byte[] GetAction()
        {
            //var ret = new byte[1];
            //ret[0] = (byte)BotAction.None;

            var ret = new byte[2];
            ret[0] = (byte)BotAction.Move;
            ret[1] = (byte)MoveDirection.North;

            //var ret = new byte[2];
            //ret[0] = (byte)BotAction.ShieldLevel;
            //ret[1] = 10;

            //var ret = new byte[2];
            //ret[0] = (byte)BotAction.CloackLevel;
            //ret[1] = 20;

            //var ret = new byte[2];
            //ret[0] = (byte)BotAction.Fire;
            //ret[1] = (byte)MoveDirection.NorthWest;

            return ret;
        }

    }
}