using System;
using Mirror;
using UnityEngine;

public class Repro : MonoBehaviour
{
    void TryUint()
    {
        using (var writer = NetworkWriterPool.GetWriter())
        {
            writer.WriteUInt(42);
            //Debug.Log("written blittable UINT");
        }

        NetworkReader reader = new NetworkReader(new byte[]{0x01, 0x02, 0x03, 0x04});
        uint value = reader.ReadUInt();
        //Debug.Log("read blittable UINT: " + value);
    }

    void TryDouble()
    {
        using (var writer = NetworkWriterPool.GetWriter())
        {
            writer.WriteDouble(42d);
            //Debug.Log("written blittable DOUBLE");
        }

        NetworkReader reader = new NetworkReader(new byte[]{0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08});
        double value = reader.ReadDouble();
        //Debug.Log("read blittable DOUBLE: " + value);
    }

    unsafe void TryUnaligned()
    {
        byte[] buffer = new byte[30];
        for (int i = 0; i < 18; ++i) {
            // 0 is aligned, 1 is aligned, etc.
            fixed (byte* ptr = &buffer[i])
            {
                double* double_ptr = (double*)ptr;
                *double_ptr = Math.PI;
            }
        }
        Debug.Log("repro unaligned: " + BitConverter.ToString(buffer));
    }

    // now try the fix
    void TryUnalignedFix()
    {
        using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
        {
            // write unaligned double at position =1
            writer.WriteByte(0xFF);
            writer.WriteDouble(Math.PI);
        }
    }

    void Start()
    {
        Debug.Log("Trying to repro..");

        //TryUint();
        //TryDouble();
        //TryUnaligned();
        TryUnalignedFix();

        Debug.LogWarning("================== END OF REPRO ==================");
    }
}
