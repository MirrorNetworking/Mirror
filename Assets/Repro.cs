using Mirror;
using UnityEngine;

public class Repro : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Trying to repro..");
        using (var writer = NetworkWriterPool.GetWriter())
        {
            writer.WriteUInt(42);
            Debug.Log("written blittable");
        }

        NetworkReader reader = new NetworkReader(new byte[]{0x01, 0x02, 0x03, 0x04});
        uint value = reader.ReadUInt();
        Debug.Log("read blittable: " + value);
    }
}
