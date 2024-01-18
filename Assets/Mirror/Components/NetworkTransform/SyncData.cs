using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Mirror
{
    public struct SyncDataFullMsg : NetworkMessage
    {
        public uint netId;
        public byte componentId;
        public SyncDataFull syncData;

        public SyncDataFullMsg(uint netId, byte componentId, SyncDataFull syncData)
        {
            this.netId = netId;
            this.componentId = componentId;
            this.syncData = syncData;
        }
    }

    public struct SyncDataDeltaMsg : NetworkMessage
    {
        public uint netId;
        public byte componentId;
        public SyncDataDelta syncData;

        public SyncDataDeltaMsg(uint netId, byte componentId, SyncDataDelta syncData)
        {
            this.netId = netId;
            this.componentId = componentId;
            this.syncData = syncData;
        }        
    }

    public struct SyncDataFull 
    {
        public byte fullSyncDataIndex;
        public SyncSettings syncSettings;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
 
        public SyncDataFull(byte fullSyncDataIndex, SyncSettings syncSettings, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.fullSyncDataIndex = fullSyncDataIndex;
            this.syncSettings = syncSettings;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }
    
    public struct SyncDataDelta 
    {
        public byte fullSyncDataIndex;
        public DeltaHeader deltaHeader;
        public Vector3Long position;
        public Quaternion quatRotation;
        public Vector3Long eulRotation;
        public Vector3Long scale;
 
        public SyncDataDelta(byte fullSyncDataIndex, DeltaHeader deltaHeader, Vector3Long position, Quaternion rotation,Vector3Long eulRotation, Vector3Long scale)
        {
            this.fullSyncDataIndex = fullSyncDataIndex;
            this.deltaHeader = deltaHeader;
            this.position = position;
            this.quatRotation = rotation;
            this.eulRotation = eulRotation;
            this.scale = scale;
        }
    }

    public struct QuantizedSnapshot
    {
        public Vector3Long position;
        public Quaternion rotation;
        public Vector3Long rotationEuler;
        public Vector3Long scale;

        public QuantizedSnapshot(Vector3Long position, Quaternion rotation, Vector3Long eulRotation, Vector3Long scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.rotationEuler = eulRotation;
            this.scale = scale;
        }
    }

    [Flags]
    public enum DeltaHeader : byte
    {
        None = 0,
        PosX = 1 << 0, 
        PosY = 1 << 1, 
        PosZ = 1 << 2, 
        SendQuat = 1 << 3,
        RotX = 1 << 4, 
        RotY = 1 << 5, 
        RotZ = 1 << 6, 
        Scale = 1 << 7,

        SendQuatCompressed = RotX,
    }

    [Flags]
    public enum SyncSettings : byte
    {
        None = 0,
        SyncPosX = 1 << 0,
        SyncPosY = 1 << 1,
        SyncPosZ = 1 << 2,
        SyncRot = 1 << 3,
        SyncScale = 1 << 4,
        CompressRot = 1 << 5,
        UseEulerAngles = 1 << 6,

    }

    public static class SyncDataReaderWriter
    {
        public static void WriteSyncDataFull(this NetworkWriter writer, SyncDataFull syncData)
        {
            writer.WriteByte(syncData.fullSyncDataIndex);
            writer.WriteByte((byte)syncData.syncSettings);
 
            if ((syncData.syncSettings & SyncSettings.SyncPosX) > 0) writer.WriteFloat(syncData.position.x);
            if ((syncData.syncSettings & SyncSettings.SyncPosY) > 0) writer.WriteFloat(syncData.position.y);
            if ((syncData.syncSettings & SyncSettings.SyncPosZ) > 0) writer.WriteFloat(syncData.position.z);
            
            if ((syncData.syncSettings & SyncSettings.SyncRot) > 0)
            {
                if ((syncData.syncSettings & SyncSettings.CompressRot) > 0) writer.WriteUInt(Compression.CompressQuaternion(syncData.rotation));
                else writer.WriteQuaternion(syncData.rotation);
            }
            
            if ((syncData.syncSettings & SyncSettings.SyncScale) > 0) writer.WriteVector3(syncData.scale);

        }
 
        public static SyncDataFull ReadSyncDataFull(this NetworkReader reader)
        {
            byte index = reader.ReadByte();   
            SyncSettings syncSettings = (SyncSettings)reader.ReadByte();
            
            // If we have nothing to read here, let's say because posX is unchanged, then we can write anything
            // for now, but in the NT, we will need to check changedData again, to put the right values of the axis
            // back. We don't have it here.
            
            Vector3 position = new Vector3(
                (syncSettings & SyncSettings.SyncPosX) > 0 ? reader.ReadFloat() : default,
                (syncSettings & SyncSettings.SyncPosY) > 0 ? reader.ReadFloat() : default,
                (syncSettings & SyncSettings.SyncPosZ) > 0 ? reader.ReadFloat() : default
            );
            
            Quaternion rotation = new Quaternion();
            if ((syncSettings & SyncSettings.SyncRot) > 0)
                rotation = (syncSettings & SyncSettings.CompressRot) > 0 ? Compression.DecompressQuaternion(reader.ReadUInt()) : reader.ReadQuaternion();
            else
                rotation = new Quaternion();
                
            Vector3 scale =  (syncSettings & SyncSettings.SyncScale) > 0 ? reader.ReadVector3() : default;
 
            return new SyncDataFull(index, syncSettings, position, rotation, scale);
        }

        public static void WriteSyncDataDelta(this NetworkWriter writer, SyncDataDelta syncData)
        {
            writer.WriteByte(syncData.fullSyncDataIndex);
            writer.WriteByte((byte)syncData.deltaHeader);

            if ((syncData.deltaHeader & DeltaHeader.PosX) > 0) Compression.CompressVarInt(writer, syncData.position.x);
            if ((syncData.deltaHeader & DeltaHeader.PosY) > 0) Compression.CompressVarInt(writer, syncData.position.y);
            if ((syncData.deltaHeader & DeltaHeader.PosZ) > 0) Compression.CompressVarInt(writer, syncData.position.z);

            if ((syncData.deltaHeader & DeltaHeader.SendQuat) > 0)
            {
                if ((syncData.deltaHeader & DeltaHeader.SendQuatCompressed) > 0) writer.WriteUInt(Compression.CompressQuaternion(syncData.quatRotation));
                else writer.WriteQuaternion(syncData.quatRotation);
            }
            else
            {
                if ((syncData.deltaHeader & DeltaHeader.RotX) > 0) Compression.CompressVarInt(writer, syncData.eulRotation.x); 
                if ((syncData.deltaHeader & DeltaHeader.RotY) > 0) Compression.CompressVarInt(writer, syncData.eulRotation.y); 
                if ((syncData.deltaHeader & DeltaHeader.RotZ) > 0) Compression.CompressVarInt(writer, syncData.eulRotation.z); 
            }

            if ((syncData.deltaHeader & DeltaHeader.Scale) > 0) 
            {
                Compression.CompressVarInt(writer, syncData.scale.x);
                Compression.CompressVarInt(writer, syncData.scale.y);
                Compression.CompressVarInt(writer, syncData.scale.z);
            }
        }

        public static SyncDataDelta ReadSyncDataDelta(this NetworkReader reader)
        {
            byte index = reader.ReadByte();
            DeltaHeader header = (DeltaHeader)reader.ReadByte();

            Vector3Long position = new Vector3Long(
                (header & DeltaHeader.PosX) > 0 ? Compression.DecompressVarInt(reader) : 0,
                (header & DeltaHeader.PosY) > 0 ? Compression.DecompressVarInt(reader) : 0,
                (header & DeltaHeader.PosZ) > 0 ? Compression.DecompressVarInt(reader) : 0
            );

            Quaternion quatRotation = new Quaternion();
            Vector3Long eulRotation = new Vector3Long();

            if ((header & DeltaHeader.SendQuat) > 0)
            {
                if ((header & DeltaHeader.SendQuatCompressed) > 0) quatRotation = Compression.DecompressQuaternion(reader.ReadUInt());
                else quatRotation = reader.ReadQuaternion();
            }
            else
            {
                eulRotation = new Vector3Long(
                    (header & DeltaHeader.RotX) > 0 ? Compression.DecompressVarInt(reader) : 0,
                    (header & DeltaHeader.RotY) > 0 ? Compression.DecompressVarInt(reader) : 0,
                    (header & DeltaHeader.RotZ) > 0 ? Compression.DecompressVarInt(reader) : 0
                );
            }

            Vector3Long scale = new Vector3Long();
            if ((header & DeltaHeader.Scale) > 0)
            {
                scale = new Vector3Long(
                    Compression.DecompressVarInt(reader),
                    Compression.DecompressVarInt(reader),
                    Compression.DecompressVarInt(reader)
                );                
            }

            return new SyncDataDelta(index, header, position, quatRotation, eulRotation, scale);
        }

        public static void WriteSyncDataFullMsg(this NetworkWriter writer, SyncDataFullMsg msg)
        {
            Compression.CompressVarUInt(writer, msg.netId);
            writer.WriteByte(msg.componentId);
            writer.Write<SyncDataFull>(msg.syncData);
        }

        public static SyncDataFullMsg ReadSyncDataFullMsg(this NetworkReader reader)
        {
            uint netId = (uint)Compression.DecompressVarUInt(reader);
            byte componentId = reader.ReadByte();

            SyncDataFull syncData = reader.Read<SyncDataFull>();

            return new SyncDataFullMsg(netId, componentId, syncData);
        }

        public static void WriteSyncDataDeltaMsg(this NetworkWriter writer, SyncDataDeltaMsg msg)
        {
            Compression.CompressVarUInt(writer, msg.netId);
            writer.WriteByte(msg.componentId);
            writer.Write<SyncDataDelta>(msg.syncData);
        }

        public static SyncDataDeltaMsg ReadSyncDataDeltaMsg(this NetworkReader reader)
        {
            uint netId = (uint)Compression.DecompressVarUInt(reader);
            byte componentId = reader.ReadByte();

            SyncDataDelta syncData = reader.Read<SyncDataDelta>();

            return new SyncDataDeltaMsg(netId, componentId, syncData);
        }
    }
}
