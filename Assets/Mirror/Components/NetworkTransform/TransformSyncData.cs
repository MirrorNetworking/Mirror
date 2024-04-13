using UnityEngine;
using System;
using Mirror;

namespace Mirror
{
    [Serializable]
    public struct SyncData
    {
        public Changed changedDataByte;
        public Vector3 position;
        public Quaternion quatRotation;
        public Vector3 vecRotation;
        public Vector3 scale;
 
        public SyncData(Changed _dataChangedByte, Vector3 _position, Quaternion _rotation, Vector3 _scale)
        {
            this.changedDataByte = _dataChangedByte;
            this.position = _position;
            this.quatRotation = _rotation;
            this.vecRotation = quatRotation.eulerAngles;
            this.scale = _scale;
        }

        public SyncData(Changed _dataChangedByte, TransformSnapshot _snapshot)
        {
            this.changedDataByte = _dataChangedByte;
            this.position = _snapshot.position;
            this.quatRotation = _snapshot.rotation;
            this.vecRotation = quatRotation.eulerAngles;
            this.scale = _snapshot.scale;
        }

        public SyncData(Changed _dataChangedByte, Vector3 _position, Vector3 _vecRotation, Vector3 _scale)
        {
            this.changedDataByte = _dataChangedByte;
            this.position = _position;
            this.vecRotation = _vecRotation;
            this.quatRotation = Quaternion.Euler(vecRotation);
            this.scale = _scale;            
        }
    } 
    
    [Flags]
    public enum Changed : byte
    {
        None = 0,
        PosX = 1 << 0, 
        PosY = 1 << 1, 
        PosZ = 1 << 2, 
        CompressRot = 1 << 3,
        RotX = 1 << 4, 
        RotY = 1 << 5, 
        RotZ = 1 << 6, 
        Scale = 1 << 7,

        Pos = PosX | PosY | PosZ,
        Rot = RotX | RotY | RotZ
    }

 
    public static class SyncDataReaderWriter
    {
        public static void WriteSyncData(this NetworkWriter writer, SyncData syncData)
        {
            writer.WriteByte((byte)syncData.changedDataByte);
 
            // Write position
            if ((syncData.changedDataByte & Changed.PosX) > 0)
            {
                writer.WriteFloat(syncData.position.x);
            }
            
            if ((syncData.changedDataByte & Changed.PosY) > 0)
            {
                writer.WriteFloat(syncData.position.y);
            }

            if ((syncData.changedDataByte & Changed.PosZ) > 0)
            {
                writer.WriteFloat(syncData.position.z);
            }
 
            // Write rotation
            if ((syncData.changedDataByte & Changed.CompressRot) > 0)
            {
                if((syncData.changedDataByte & Changed.Rot) > 0)
                {
                    writer.WriteUInt(Compression.CompressQuaternion(syncData.quatRotation));
                }
            }
            else
            {
                if ((syncData.changedDataByte & Changed.RotX) > 0)
                {
                    writer.WriteFloat(syncData.quatRotation.eulerAngles.x);
                }                

                if ((syncData.changedDataByte & Changed.RotY) > 0)
                {
                    writer.WriteFloat(syncData.quatRotation.eulerAngles.y);
                }  

                if ((syncData.changedDataByte & Changed.RotZ) > 0)
                {
                    writer.WriteFloat(syncData.quatRotation.eulerAngles.z);
                }  
            }

            // Write scale
            if ((syncData.changedDataByte & Changed.Scale) > 0)
            {
                writer.WriteVector3(syncData.scale);
            }
        }
 
        public static SyncData ReadSyncData(this NetworkReader reader)
        {   
            Changed changedData = (Changed)reader.ReadByte();
            
            // If we have nothing to read here, let's say because posX is unchanged, then we can write anything
            // for now, but in the NT, we will need to check changedData again, to put the right values of the axis
            // back. We don't have it here.

            Vector3 position = 
                new Vector3(
                    (changedData & Changed.PosX) > 0 ? reader.ReadFloat() : 0,
                    (changedData & Changed.PosY) > 0 ? reader.ReadFloat() : 0,
                    (changedData & Changed.PosZ) > 0 ? reader.ReadFloat() : 0
                );

            Vector3 vecRotation = new Vector3();
            Quaternion quatRotation = new Quaternion();

            if ((changedData & Changed.CompressRot) > 0)
            {
                quatRotation = (changedData & Changed.RotX) > 0 ? Compression.DecompressQuaternion(reader.ReadUInt()) : new Quaternion();
            }
            else
            {
                vecRotation =
                    new Vector3(
                        (changedData & Changed.RotX) > 0 ? reader.ReadFloat() : 0,
                        (changedData & Changed.RotY) > 0 ? reader.ReadFloat() : 0,
                        (changedData & Changed.RotZ) > 0 ? reader.ReadFloat() : 0
                    );
            }
                
            Vector3 scale = (changedData & Changed.Scale) == Changed.Scale ? reader.ReadVector3() : new Vector3();

            SyncData _syncData = (changedData & Changed.CompressRot) > 0 ? new SyncData(changedData, position, quatRotation, scale) : new SyncData(changedData, position, vecRotation, scale);

            return _syncData;
        }
    }
}
