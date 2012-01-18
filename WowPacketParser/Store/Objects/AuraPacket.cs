using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace WowPacketParser.Store.Objects
{
    public sealed class AuraPacket
    {
        public AuraPacket(DateTime _time, int _number, Aura _aura)
        {
            time = _time;
            number = _number;
            aura = _aura;
        }

        public DateTime time;
        public int number;
        public Aura aura;
    }

    public sealed class AuraPackets
    {
        public AuraPackets(AuraPacket aura)
        {
            auraPackets.Enqueue(aura);
        }

        public ConcurrentQueue<AuraPacket> auraPackets = 
            new ConcurrentQueue<AuraPacket>();
    }
}
