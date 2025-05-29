using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.AssignAuthority
{
    [AddComponentMenu("")]
    public class UI : NetworkBehaviour
    {
        public List<ObserverBehaviour> Objects;
        public List<Button> Buttons;

        void Awake()
        {
            for (int i = 0; i < Buttons.Count; i++)
            {
                Button button = Buttons[i];
                int index = i;
                button.onClick.AddListener(() =>
                {
                    CmdTakeAuthority(index, null /* will be filled by mirror */);
                });
            }
        }


        [Command(requiresAuthority = false)]
        public void CmdTakeAuthority(int index, NetworkConnectionToClient sender)
        {
            if (index < 0 || index >= Objects.Count)
                return;
            ObserverBehaviour obj = Objects[index];
            if (obj.netIdentity.connectionToClient != sender)
            {
                if (obj.netIdentity.connectionToClient != null)
                    obj.netIdentity.RemoveClientAuthority();
                obj.netIdentity.AssignClientAuthority(sender);
                obj.Color = sender.identity.GetComponent<Player>().Color;
            }
            else
            {
                obj.netIdentity.RemoveClientAuthority();
                obj.Color = Color.gray;
            }
        }

    }
}
