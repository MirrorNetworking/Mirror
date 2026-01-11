using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;

namespace Mirror.Examples.AutoLANClientController
{
    [AddComponentMenu("")]
    public class NetworkSceneScript : NetworkBehaviour
    {

        public Button clientButton;
        public Text textResult;
        public GameObject panelClient, panelServer;

        void Start()
        {
            clientButton.onClick.AddListener(ClientButton);

            panelServer.SetActive(false);
            panelClient.SetActive(false);

            if (isServer)
            {
                panelServer.SetActive(true);

            }
            if (isClient)
            {
                panelClient.SetActive(true);
            }
        }

        void ClientButton()
        {
            if (isClient)
            {
                CmdSendText("Text: " + Random.Range(0, 999));
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdSendText(string _value)
        {
            textResult.text = _value;
        }
    }
}