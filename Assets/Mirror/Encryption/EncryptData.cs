using UnityEngine;

namespace Mirror.Encryption
{
    public class EncryptData : MonoBehaviour
    {
        ServerEncrypter serverEncrypter;
        ClientEncrypter clientEncrypter;

        string text;

        private void OnGUI()
        {
            using (new GUILayout.AreaScope(new Rect(250, 0, 200, 600)))
            {
                if (GUILayout.Button("init"))
                {
                    NetworkManager.singleton.StartHost();

                    serverEncrypter = ServerEncrypter.Instance;
                    clientEncrypter = ClientEncrypter.Instance;

                    serverEncrypter.OnEncryptedMessage += ServerEncrypter_OnEncryptedMessage;
                }

                if (GUILayout.Button("Get public key"))
                {
                    clientEncrypter.RequestPublicKey();
                }

                text = GUILayout.TextField(text);
                if (GUILayout.Button("Send Encrypt"))
                {
                    NetworkWriter writer = new NetworkWriter();
                    writer.WriteString(text);
                    clientEncrypter.EncryptSend(writer.ToArray());
                }
                if (GUILayout.Button("Send"))
                {
                    NetworkWriter writer = new NetworkWriter();
                    writer.WriteString(text);
                    clientEncrypter.Send(writer.ToArray());
                }
            }
        }

        private void ServerEncrypter_OnEncryptedMessage(byte[] bytes)
        {
            NetworkReader reader = new NetworkReader(bytes);

            string myText = reader.ReadString();
            Debug.Log($"decrypted text: {myText}");
        }
    }
}
