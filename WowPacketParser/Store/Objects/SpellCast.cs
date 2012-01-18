using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WowPacketParser.Misc;
using WowPacketParser.Enums;

namespace WowPacketParser.Store.Objects
{
    public sealed class SpellCast
    {
        public SpellCast(DateTime _time, int _number, int _spellId, TargetFlag _targetFlag, List<Misc.Guid> _hitTargets, List<Misc.Guid> _missTargets)
        {
            time = _time;
            number = _number;
            spellId = _spellId;
            targetFlag = _targetFlag;
            hitTargets = _hitTargets;
            missTargets = _missTargets;
        } 

        public DateTime time;
        public int number;
        public int spellId;
        public TargetFlag targetFlag = new TargetFlag();
        public List<Misc.Guid> hitTargets = new List<Misc.Guid>();
        public List<Misc.Guid> missTargets = new List<Misc.Guid>();
    }
}
