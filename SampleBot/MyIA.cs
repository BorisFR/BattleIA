using System;
using BattleIA;

namespace SampleBot
{
    public class MyIA
    {
        public MyIA()
        {
        }

        public byte GetScanSurface()
        {
            return 10;
        }

        public byte[] GetAction()
        {
            //var ret = new byte[1];
            //ret[0] = (byte)BattleIA.Action.None;

            var ret = new byte[2];
            ret[0] = (byte)BattleIA.Action.Move;
            ret[1] = (byte)MoveDirection.North;

            //var ret = new byte[2];
            //ret[0] = (byte)BattleIA.Action.ShieldLevel;
            //ret[1] = 10;

            //var ret = new byte[2];
            //ret[0] = (byte)BattleIA.Action.CloackLevel;
            //ret[1] = 20;

            //var ret = new byte[2];
            //ret[0] = (byte)BattleIA.Action.Fire;
            //ret[1] = (byte)MoveDirection.NorthWest;

            return ret;
        }

    }
}