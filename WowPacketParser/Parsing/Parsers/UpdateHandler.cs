using System;
using System.Collections;
using System.Collections.Generic;
using WowPacketParser.Enums;
using WowPacketParser.Enums.Version;
using WowPacketParser.Misc;
using WowPacketParser.Store.Objects;
using Guid=WowPacketParser.Misc.Guid;

namespace WowPacketParser.Parsing.Parsers
{
    public static class UpdateHandler
    {
        public static readonly Dictionary<uint, Dictionary<Guid, WoWObject>> Objects =
            new Dictionary<uint, Dictionary<Guid, WoWObject>>();

        [Parser(Opcode.SMSG_UPDATE_OBJECT)]
        public static void HandleUpdateObject(Packet packet)
        {
            uint map = MovementHandler.CurrentMapId;
            if (ClientVersion.AddedInVersion(ClientVersionBuild.V4_0_1_13164))
                map = packet.ReadUInt16("Map");

            var count = packet.ReadUInt32("Count");

            if (ClientVersion.RemovedInVersion(ClientVersionBuild.V3_0_2_9056))
                packet.ReadBoolean("Has Transport");

            for (var i = 0; i < count; i++)
            {
                var type = packet.ReadByte();
                var typeString = ClientVersion.AddedInVersion(ClientType.Cataclysm) ? ((UpdateTypeCataclysm)type).ToString() : ((UpdateType)type).ToString();

                packet.Writer.WriteLine("[" + i + "] UpdateType: " + typeString);
                switch (typeString)
                {
                    case "Values":
                    {
                        var guid = packet.ReadPackedGuid("GUID", i);

                        WoWObject obj;
                        var updates = ReadValuesUpdateBlock(ref packet, guid.GetObjectType(), i);

                        if (packet.SniffFileInfo.Stuffing.Objects.TryGetValue(guid, out obj))
                        {
                            if (obj.ChangedUpdateFieldsList == null)
                                obj.ChangedUpdateFieldsList = new List<Dictionary<int, UpdateField>>();
                            obj.ChangedUpdateFieldsList.Add(updates);
                        }

                        if (guid.HasEntry() && guid.GetObjectType() == ObjectType.Unit)
                        {
                            List<string> lines = new List<string>();
                            lines.Add("UpdateType: Values");
                            foreach (var data in updates)
                                lines.Add("Block Value " + UpdateFields.GetUpdateFieldName(data.Key,"UnitField") + ": " + data.Value.Int32Value + "/" + data.Value.SingleValue);

                            if (packet.SniffFileInfo.Stuffing.upObjPackets.ContainsKey(guid))
                                packet.SniffFileInfo.Stuffing.upObjPackets[guid].upObjPackets.Enqueue(new UpdateObjectPacket(packet.Time, packet.Number, lines));
                            else
                                packet.SniffFileInfo.Stuffing.upObjPackets.TryAdd(guid, new UpdateObjectPackets(new UpdateObjectPacket(packet.Time, packet.Number, lines)));
                        }
                        break;
                    }
                    case "Movement":
                    {
                        var guid = ClientVersion.AddedInVersion(ClientVersionBuild.V3_1_2_9901) ? packet.ReadPackedGuid("GUID", i) : packet.ReadGuid("GUID", i);
                        ReadMovementUpdateBlock(ref packet, guid, i);
                        // Should we update Stuffing.Object?
                        break;
                    }
                    case "CreateObject1":
                    case "CreateObject2": // Might != CreateObject1 on Cata
                    {
                        var guid = packet.ReadPackedGuid("GUID", i);
                        ReadCreateObjectBlock(ref packet, guid, map, i);
                        break;
                    }
                    case "FarObjects":
                    case "NearObjects":
                    case "DestroyObjects":
                    {
                        ReadObjectsBlock(ref packet, i, typeString);
                        break;
                    }
                }
            }
        }

        private static void ReadCreateObjectBlock(ref Packet packet, Guid guid, uint map, int index)
        {
            var objType = packet.ReadEnum<ObjectType>("Object Type", TypeCode.Byte, index);
            var moves = ReadMovementUpdateBlock(ref packet, guid, index);
            var updates = ReadValuesUpdateBlock(ref packet, objType, index);

            var obj = new WoWObject {Type = objType, Movement = moves, UpdateFields = updates, Map = map, Area = WorldStateHandler.CurrentAreaId, PhaseMask = (uint) MovementHandler.CurrentPhaseMask};
            if (guid.HasEntry() && guid.GetObjectType() == ObjectType.Unit)            
            {
                List<string> lines = new List<string>();
                lines.Add("UpdateType: Values");
                foreach (var data in updates)
                    lines.Add("Block Value " + UpdateFields.GetUpdateFieldName(data.Key, "UnitField") + ": " + data.Value.Int32Value + "/" + data.Value.SingleValue);
            
                if (packet.SniffFileInfo.Stuffing.upObjPackets.ContainsKey(guid))
                    packet.SniffFileInfo.Stuffing.upObjPackets[guid].upObjPackets.Enqueue(new UpdateObjectPacket(packet.Time, packet.Number, lines));
                else
                    packet.SniffFileInfo.Stuffing.upObjPackets.TryAdd(guid, new UpdateObjectPackets(new UpdateObjectPacket(packet.Time, packet.Number, lines)));
            }

            packet.SniffFileInfo.Stuffing.Objects.TryAdd(guid, obj);

            if (guid.HasEntry() && (objType == ObjectType.Unit || objType == ObjectType.GameObject))
                packet.AddSniffData(Utilities.ObjectTypeToStore(objType), (int)guid.GetEntry(), "SPAWN");
        }

        public static void ReadObjectsBlock(ref Packet packet, int index, string typeString)
        {
            var objCount = packet.ReadInt32("Object Count", index);

            List<string> lines = new List<string>();
            lines.Add("UpdateType: " + typeString);

            for (var j = 0; j < objCount; j++)
            {
                Guid guid = packet.ReadPackedGuid("Object GUID", index, j);
                if (guid.HasEntry() && guid.GetObjectType() == ObjectType.Unit)
                {
                    lines.Add("ObjectGuid: " + guid.GetLow());

                    if (packet.SniffFileInfo.Stuffing.upObjPackets.ContainsKey(guid))
                        packet.SniffFileInfo.Stuffing.upObjPackets[guid].upObjPackets.Enqueue(new UpdateObjectPacket(packet.Time, packet.Number, lines));
                    else
                        packet.SniffFileInfo.Stuffing.upObjPackets.TryAdd(guid, new UpdateObjectPackets(new UpdateObjectPacket(packet.Time, packet.Number, lines)));
                }
            }

        }

        private static Dictionary<int, UpdateField> ReadValuesUpdateBlock(ref Packet packet, ObjectType type, int index)
        {
            var maskSize = packet.ReadByte();

            var updateMask = new int[maskSize];
            for (var i = 0; i < maskSize; i++)
                updateMask[i] = packet.ReadInt32();

            var mask = new BitArray(updateMask);
            var dict = new Dictionary<int, UpdateField>();

            int objectEnd = UpdateFields.GetUpdateField(ObjectField.OBJECT_END);

            for (var i = 0; i < mask.Count; i++)
            {
                if (!mask[i])
                    continue;

                var blockVal = packet.ReadUpdateField();
                string key = "Block Value " + i;
                string value = blockVal.Int32Value + "/" + blockVal.SingleValue;

                if (i < objectEnd)
                    key = UpdateFields.GetUpdateFieldName(i, "ObjectField");
                else
                {
                    switch (type)
                    {
                        case ObjectType.Container:
                        {
                            if (i < UpdateFields.GetUpdateField(ItemField.ITEM_END))
                                goto case ObjectType.Item;

                            key = UpdateFields.GetUpdateFieldName(i, "ContainerField");
                            break;
                        }
                        case ObjectType.Item:
                        {
                            key = UpdateFields.GetUpdateFieldName(i, "ItemField");
                            break;
                        }
                        case ObjectType.Player:
                        {
                            if (i < UpdateFields.GetUpdateField(UnitField.UNIT_END))
                                goto case ObjectType.Unit;

                            key = UpdateFields.GetUpdateFieldName(i, "PlayerField");
                            break;
                        }
                        case ObjectType.Unit:
                        {
                            key = UpdateFields.GetUpdateFieldName(i, "UnitField");
                            break;
                        }
                        case ObjectType.GameObject:
                        {
                            key = UpdateFields.GetUpdateFieldName(i, "GameObjectField");
                            break;
                        }
                        case ObjectType.DynamicObject:
                        {
                            key = UpdateFields.GetUpdateFieldName(i, "DynamicObjectField");
                            break;
                        }
                        case ObjectType.Corpse:
                        {
                            key = UpdateFields.GetUpdateFieldName(i, "CorpseField");
                            break;
                        }
                    }
                }
                packet.Writer.WriteLine("[" + index + "] " + key + ": " + value);
                dict.Add(i, blockVal);
            }

            return dict;
        }

        private static MovementInfo ReadMovementUpdateBlock(ref Packet packet, Guid guid, int index)
        {
            var lines = new List<string>();
            lines.Add("MovementUpdateBlock");

            var moveInfo = new MovementInfo();

            var flagsTypeCode = ClientVersion.AddedInVersion(ClientVersionBuild.V3_1_0_9767) ? TypeCode.UInt16 : TypeCode.Byte;
            var flags = packet.ReadEnum<UpdateFlag>("[" + index + "] Update Flags", flagsTypeCode);

            lines.Add("[" + index + "] Update Flags");

            if (flags.HasAnyFlag(UpdateFlag.Living))
            {
                moveInfo = MovementHandler.ReadMovementInfo(ref packet, guid, index);
                var moveFlags = moveInfo.Flags;

                var speedCount = ClientVersion.AddedInVersion(ClientVersionBuild.V3_0_2_9056) ? 9 : 8;
                int speedShift;
                if (ClientVersion.AddedInVersion(ClientVersionBuild.V4_1_0_13914) &&
                    ClientVersion.RemovedInVersion(ClientVersionBuild.V4_2_2_14545))
                    speedShift = 1;  // enums shifted by one
                else speedShift = 0;

                for (var i = 0; i < speedCount - speedShift; i++)
                {
                    var speedType = (SpeedType)(i + speedShift);
                    var speed = packet.ReadSingle("["+ index + "] " + speedType + " Speed");
                    lines.Add("[" + index + "] " + speedType + " Speed: " + speed);

                    switch (speedType)
                    {
                        case SpeedType.Walk:
                        {
                            moveInfo.WalkSpeed = speed / 2.5f;
                            break;
                        }
                        case SpeedType.Run:
                        {
                            moveInfo.RunSpeed = speed / 7.0f;
                            break;
                        }
                    }
                }

                // Either movement flags are incorrect for 4.2.2 or this has been removed
                if (ClientVersion.RemovedInVersion(ClientVersionBuild.V4_2_2_14545) && moveFlags.HasAnyFlag(MovementFlag.SplineEnabled)
                    || moveInfo.HasSplineData)
                {
                    // Temp solution
                    // TODO: Make Enums version friendly
                    if (ClientVersion.AddedInVersion(ClientVersionBuild.V4_2_2_14545))
                    {
                        var splineFlags422 = packet.ReadEnum<SplineFlag422>("Spline Flags", TypeCode.Int32, index);
                        lines.Add("Spline Flags: " + splineFlags422.ToString());

                        if (splineFlags422.HasAnyFlag(SplineFlag422.FinalTarget))
                        {
                            Misc.Guid targetGuid = packet.ReadGuid("Final Spline Target GUID", index);
                            lines.Add("Final Spline Target GUID: " + targetGuid.GetLow());
                        }
                        else if (splineFlags422.HasAnyFlag(SplineFlag422.FinalOrientation))
                        {
                            float orientation = packet.ReadSingle("Final Spline Orientation", index);
                            lines.Add("Final Spline Orientation: " + orientation);
                        }
                        else if (splineFlags422.HasAnyFlag(SplineFlag422.FinalPoint))
                        {
                            Vector3 finalCords = packet.ReadVector3("Final Spline Coords", index);
                            lines.Add("Final Spline Coords: " + finalCords.ToString());
                        }
                    }
                    else
                    {
                        var splineFlags = packet.ReadEnum<SplineFlag>("Spline Flags", TypeCode.Int32, index);
                        lines.Add("Spline Flags: " + splineFlags.ToString());
                        if (splineFlags.HasAnyFlag(SplineFlag.FinalTarget))
                        {
                            Guid targetGuid = packet.ReadGuid("Final Spline Target GUID", index);
                            lines.Add("Final Spline Target GUID: " + targetGuid.GetLow());
                        }
                        else if (splineFlags.HasAnyFlag(SplineFlag.FinalOrientation))
                        {
                            float orientation = packet.ReadSingle("Final Spline Orientation", index);
                            lines.Add("Final Spline Orientation: " + orientation);
                        }
                        else if (splineFlags.HasAnyFlag(SplineFlag.FinalPoint))
                        {
                            Vector3 finalCords = packet.ReadVector3("Final Spline Coords", index);
                            lines.Add("Final Spline Coords: " + finalCords.ToString());
                        }
                    }

                    lines.Add("Spline Time: " + packet.ReadInt32("Spline Time", index));
                    lines.Add("Spline Full Time: " + packet.ReadInt32("Spline Full Time", index));
                    packet.ReadInt32("Spline ID", index);

                    if (ClientVersion.AddedInVersion(ClientVersionBuild.V3_1_0_9767))
                    {
                        lines.Add("Spline Duration Multiplier: " + packet.ReadSingle("Spline Duration Multiplier", index));
                        lines.Add("Spline Duration Multiplier Next: " + packet.ReadSingle("Spline Duration Multiplier Next", index));
                        lines.Add("Spline Vertical Acceleration" + packet.ReadSingle("Spline Vertical Acceleration", index));
                        lines.Add("Spline Start Time " + packet.ReadInt32("Spline Start Time", index));
                    }

                    var splineCount = packet.ReadInt32();
                    for (var i = 0; i < splineCount; i++)
                        lines.Add("[" + i + "] Spline Waypoint: " + packet.ReadVector3("Spline Waypoint", index, i).ToString());

                    if (ClientVersion.AddedInVersion(ClientVersionBuild.V3_1_0_9767))
                        lines.Add("Spline Mode: " + packet.ReadEnum<SplineMode>("Spline Mode", TypeCode.Byte, index).ToString());

                    lines.Add("Spline Endpoint: " + packet.ReadVector3("Spline Endpoint", index).ToString());
                }
            }
            else // !UpdateFlag.Living
            {
                if (flags.HasAnyFlag(UpdateFlag.GOPosition))
                {
                    packet.ReadPackedGuid("GO Position GUID", index);

                    moveInfo.Position = packet.ReadVector3("[" + index + "] GO Position");
                    packet.ReadVector3("GO Transport Position", index);

                    moveInfo.Orientation = packet.ReadSingle("[" + index + "] GO Orientation");
                    packet.ReadSingle("GO Transport Orientation", index);
                }
                else if (flags.HasAnyFlag(UpdateFlag.StationaryObject))
                {
                    moveInfo.Position = packet.ReadVector3();
                    moveInfo.Orientation = packet.ReadSingle();
                    packet.Writer.WriteLine("[{0}] Stationary Position: {1}, O: {2}", index, moveInfo.Position, moveInfo.Orientation);
                }
            }

            if (ClientVersion.RemovedInVersion(ClientVersionBuild.V4_2_2_14545))
            {
                if (flags.HasAnyFlag(UpdateFlag.Unknown1))
                    packet.ReadInt32("Unk Int32", index);

                if (flags.HasAnyFlag(UpdateFlag.LowGuid))
                    packet.ReadInt32("Low GUID", index);
            }

            if (flags.HasAnyFlag(UpdateFlag.AttackingTarget))
                lines.Add("Target GUID: " + packet.ReadPackedGuid("Target GUID", index));

            if (flags.HasAnyFlag(UpdateFlag.Transport))
                lines.Add("Transport Movement Time (ms): " + packet.ReadInt32("Transport Movement Time (ms)", index));

            if (flags.HasAnyFlag(UpdateFlag.Vehicle))
            {
                moveInfo.VehicleId = packet.ReadUInt32("[" + index + "] Vehicle ID");
                lines.Add("Vehicle ID : " + moveInfo.VehicleId);
                lines.Add("Vehicle Orientation: " + packet.ReadSingle("Vehicle Orientation", index));
            }

            if (ClientVersion.AddedInVersion(ClientVersionBuild.V4_2_2_14545))
            {
                if (flags.HasAnyFlag(UpdateFlag.AnimKits))
                {
                    packet.ReadInt16("Unk Int16", index);
                    packet.ReadInt16("Unk Int16", index);
                    packet.ReadInt16("Unk Int16", index);
                }
            }

            if (flags.HasAnyFlag(UpdateFlag.GORotation))
                moveInfo.Rotation = packet.ReadPackedQuaternion("GO Rotation", index);

            if (ClientVersion.AddedInVersion(ClientVersionBuild.V4_2_2_14545))
            {
                if (flags.HasAnyFlag(UpdateFlag.TransportUnkArray))
                {
                    var count = packet.ReadByte("Count", index);
                    for (var i = 0; i < count; i++)
                        packet.ReadInt32("Unk Int32", index, count);
                }
            }

            // Initialize fields that are not used by GOs
            if (guid.GetObjectType() == ObjectType.GameObject)
            {
                moveInfo.VehicleId = 0;
                moveInfo.WalkSpeed = 0;
                moveInfo.RunSpeed = 0;
            }

            if (guid.HasEntry() && guid.GetObjectType() == ObjectType.Unit)
            {
                if (packet.SniffFileInfo.Stuffing.upObjPackets.ContainsKey(guid))
                    packet.SniffFileInfo.Stuffing.upObjPackets[guid].upObjPackets.Enqueue(new UpdateObjectPacket(packet.Time, packet.Number, lines));
                else
                    packet.SniffFileInfo.Stuffing.upObjPackets.TryAdd(guid, new UpdateObjectPackets(new UpdateObjectPacket(packet.Time, packet.Number, lines)));
            }

            return moveInfo;
        }

        [Parser(Opcode.SMSG_COMPRESSED_UPDATE_OBJECT)]
        public static void HandleCompressedUpdateObject(Packet packet)
        {
            using (var packet2 = packet.Inflate(packet.ReadInt32()))
            {
                HandleUpdateObject(packet2);
            }
        }

        [Parser(Opcode.SMSG_DESTROY_OBJECT)]
        public static void HandleDestroyObject(Packet packet)
        {
            packet.ReadGuid("GUID");

            if (ClientVersion.AddedInVersion(ClientVersionBuild.V3_0_2_9056))
                packet.ReadBoolean("Despawn Animation");
        }
    }
}
