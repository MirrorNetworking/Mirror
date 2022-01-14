using Mirror;
using UnityEngine;

public class Repro : MonoBehaviour
{
    void TryUint()
    {
        using (var writer = NetworkWriterPool.GetWriter())
        {
            writer.WriteUInt(42);
            Debug.Log("written blittable UINT");
        }

        NetworkReader reader = new NetworkReader(new byte[]{0x01, 0x02, 0x03, 0x04});
        uint value = reader.ReadUInt();
        Debug.Log("read blittable UINT: " + value);
    }

    void TryDouble()
    {
        using (var writer = NetworkWriterPool.GetWriter())
        {
            writer.WriteDouble(42d);
            Debug.Log("written blittable DOUBLE");
        }

        NetworkReader reader = new NetworkReader(new byte[]{0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08});
        double value = reader.ReadDouble();
        Debug.Log("read blittable DOUBLE: " + value);
    }

    void Start()
    {
        Debug.Log("Trying to repro..");

        TryUint();
        TryDouble();

        Debug.LogWarning("================== END OF REPRO ==================");
    }
}
