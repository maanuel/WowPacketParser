using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace WowPacketParser.Store.Objects
{
    public sealed class CombatState
    {
        public CombatState(DateTime _time, int _number, string _state)
        {
            time = _time;
            number = _number;
            state = _state;
        }

        public DateTime time;
        public int number;
        public string state;
    }

    public sealed class CombateStates
    {
        public CombateStates(CombatState combatState)
        {
            combateStates.Enqueue(combatState);
        }

        public ConcurrentQueue<CombatState> combateStates =
            new ConcurrentQueue<CombatState>();
    }
}
