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
    }

    public enum CaseState : byte
    {
        Empty = 0,
        OurBot = 1,
        Wall = 2,
        Energy = 3,
        Ennemy = 4,
    }
}
