using UnityEditor;
using UnityEngine;

namespace Mirror.Transports.Encryption
{
    [CustomEditor(typeof(EncryptionTransport), true)]
    public class EncryptionTransportInspector : UnityEditor.Editor
    {
        SerializedProperty innerProperty;
        SerializedProperty clientValidatesServerPubKeyProperty;
        SerializedProperty clientTrustedPubKeySignaturesProperty;
        SerializedProperty serverKeypairPathProperty;
        SerializedProperty serverLoadKeyPairFromFileProperty;

        // Assuming proper SerializedProperty definitions for properties
        // Add more SerializedProperty fields related to different modes as needed

        void OnEnable()
        {
            innerProperty = serializedObject.FindProperty("Inner");
            clientValidatesServerPubKeyProperty = serializedObject.FindProperty("ClientValidateServerPubKey");
            clientTrustedPubKeySignaturesProperty = serializedObject.FindProperty("ClientTrustedPubKeySignatures");
            serverKeypairPathProperty = serializedObject.FindProperty("ServerKeypairPath");
            serverLoadKeyPairFromFileProperty = serializedObject.FindProperty("ServerLoadKeyPairFromFile");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw default inspector for the parent class
            DrawDefaultInspector();
            EditorGUILayout.LabelField("Encryption Settings", EditorStyles.boldLabel);
            if (innerProperty != null)
            {
                EditorGUILayout.LabelField("Common", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(innerProperty);
                EditorGUILayout.Separator();
            }
            // Client Section
            EditorGUILayout.LabelField("Client", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Validating the servers public key is essential for complete man-in-the-middle (MITM) safety, but might not be feasible for all modes of hosting.", MessageType.Info);
            EditorGUILayout.PropertyField(clientValidatesServerPubKeyProperty, new GUIContent("Validate Server Public Key"));

            EncryptionTransport.ValidationMode validationMode = (EncryptionTransport.ValidationMode)clientValidatesServerPubKeyProperty.enumValueIndex;

            switch (validationMode)
            {
                case EncryptionTransport.ValidationMode.List:
                    EditorGUILayout.PropertyField(clientTrustedPubKeySignaturesProperty);
                    break;
                case EncryptionTransport.ValidationMode.Callback:
                    EditorGUILayout.HelpBox("Please set the EncryptionTransport.onClientValidateServerPubKey at runtime.", MessageType.Info);
                    break;
            }

            EditorGUILayout.Separator();
            // Server Section
            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serverLoadKeyPairFromFileProperty, new GUIContent("Load Keypair From File"));
            if (serverLoadKeyPairFromFileProperty.boolValue)
            {
                EditorGUILayout.PropertyField(serverKeypairPathProperty, new GUIContent("Keypair File Path"));
            }
            if(GUILayout.Button("Generate Key Pair"))
            {
                EncryptionCredentials keyPair = EncryptionCredentials.Generate();
                string path = EditorUtility.SaveFilePanel("Select where to save the keypair", "", "server-keys.json", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    keyPair.SaveToFile(path);
                    EditorUtility.DisplayDialog("KeyPair Saved", $"Successfully saved the keypair.\nThe fingerprint is {keyPair.PublicKeyFingerprint}, you can also retrieve it from the saved json file at any point.", "Ok");
                    if (validationMode == EncryptionTransport.ValidationMode.List)
                    {
                        if (EditorUtility.DisplayDialog("Add key to trusted list?", "Do you also want to add the generated key to the trusted list?", "Yes", "No"))
                        {
                            clientTrustedPubKeySignaturesProperty.arraySize++;
                            clientTrustedPubKeySignaturesProperty.GetArrayElementAtIndex(clientTrustedPubKeySignaturesProperty.arraySize - 1).stringValue = keyPair.PublicKeyFingerprint;
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        [CustomEditor(typeof(ThreadedEncryptionKcpTransport), true)]
        class EncryptionThreadedTransportInspector : EncryptionTransportInspector {}
    }
}
