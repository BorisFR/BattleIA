using System;
using System.Collections.Generic;
using System.Text;

namespace BattleIA
{

    public enum BotState : byte
    {
        Undefined = 0,
        WaitingGUID = 1,
        ErrorGUID = 2,
        Ready = 3,
        Error = 4,
        Disconnect = 5,
        WaitingAnswerD = 6,
        WaitingAction = 7,
        IsDead = 8,
    }

    public enum CaseState : byte
    {
        Empty = 0,
        OurBot = 1,
        Wall = 2,
        Energy = 3,
        Ennemy = 4,
    }

    public enum BotAction : byte
    {
        None = 0,
        Move = 1,
        ShieldLevel = 2,
        CloakLevel = 3,
        Fire = 4,
    }

    public enum MessageSize : byte
    {
        Dead = 1,
        OK = 2,
        Turn = 9,
        Change = 7,
    }

    public enum MoveDirection : byte
    {
        North = 1,
        NorthWest = 2,
        West = 3,
        SouthWest = 4,
        South = 5,
        SouthEast = 6,
        East = 7,
        NorthEast = 8,
    }

}
