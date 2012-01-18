using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace WowPacketParser.Store.Objects
{
    public sealed class UpdateObjectPacket
    {
        public UpdateObjectPacket(DateTime _time, int _number, List<string> _lines)
        {
            time = _time;
            number = _number;
            lines = _lines;
        }

        public DateTime time;
        public int number;
        public List<string> lines = new List<string>();
    }

    public sealed class UpdateObjectPackets
    {
        public UpdateObjectPackets(UpdateObjectPacket upObjPacket)
        {
            upObjPackets.Enqueue(upObjPacket);
        }

        public ConcurrentQueue<UpdateObjectPacket> upObjPackets = new 
            ConcurrentQueue<UpdateObjectPacket>();
    }
}
