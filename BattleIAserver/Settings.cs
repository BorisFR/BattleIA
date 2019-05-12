using System;

namespace BattleIAserver
{
    public class Settings
    {
        public int ServerPort = 2626;
        public UInt16 MapWidth = 32;
        public UInt16 MapHeight = 22;
        public UInt16 MapPercentWall = 3;
        public UInt16 MapPercentEnergy = 5;

        public UInt16 EnergyStart = 100;
        public UInt16 EnergyLostByTurn = 1;
        public UInt16 EnergyLostByShield = 1;
        public UInt16 EnergyLostByCloak = 1;
        public UInt16 EnergyLostByMove = 1;
        public UInt16 EnergyLostShot = 2;
        public UInt16 EnergyLostContactWall = 5;
        public UInt16 EnergyLostContactEnemy = 15;

        public UInt16 PointByTurn = 1;
        public UInt16 PointByEnergyFound = 8;
        public UInt16 PointByEnnemyTouch = 20;
        public UInt16 PointByEnnemyKill = 70;

    }
}
