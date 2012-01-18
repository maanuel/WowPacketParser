using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using Vector3 = WowPacketParser.Misc.Vector3;

namespace WowPacketParser.Store.Objects
{
    public sealed class MovementPacket
    {
        public MovementPacket(int _packetId, DateTime _time, List<string> _waypoints)
        {
            number = _packetId;
            time = _time;
            waypoints = _waypoints;
        }

        public int number;
        public DateTime time;

        public List<string> waypoints =
            new List<string>();
    }

    public sealed class MovementPackets
    {
        public MovementPackets(MovementPacket movPacket)
        {
            movementPackets.Enqueue(movPacket);
        }

        public ConcurrentQueue<MovementPacket> movementPackets =
            new ConcurrentQueue<MovementPacket>();
    }
}
