using Mirror;
using UnityEngine;

namespace GeneratedReaderWriter.MessageWriterShouldUseMessageBaseFunctions
{
    public class MyMessage : IMessageBase
    {
        public int someField;
        public MeshRenderer renderer;

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteInt32(someField);
            writer.WriteNetworkIdentity(renderer.GetComponent<NetworkIdentity>());
        }

        public void Deserialize(NetworkReader reader)
        {
            someField = reader.ReadInt32();
            NetworkIdentity netID = reader.ReadNetworkIdentity();
            renderer = netID.GetComponent<MeshRenderer>();
        }
    }

    public class MessageWriterShouldUseMessageBaseFunctions : NetworkBehaviour
    {
        // use an rpc here to force weaver to create writer for MyMessage
        // writer should be Serialize/Deserialize so invalid type MeshRenderer shouldn't give error
        [ClientRpc]
        public void RpcSendMessage(MyMessage myMessage)
        {
            // do something with message
        }
    }
}
