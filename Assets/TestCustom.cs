using UnityEngine;
using Mirror;

public class TestCustom : NetworkBehaviour
{
    // the syncvar
    [SyncVar] GameObject test;

    // server-side serialization
    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        writer.Write(test);
        return true;
    }

    // client-side deserialization
    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        test = reader.ReadGameObject();
    }
}
