using UnityEngine;
using Mirror;

namespace SceneChangeTest
{
    public class SceneChangeTest : NetworkBehaviour
    {
        public readonly SyncList<int> ints = new SyncList<int>();

        public override void OnStartServer()
        {
            ints.Add(0);
        }

        public override void OnStartClient()
        {
            ints.Callback += OnInventoryUpdated;
        }

        void Start()
        {
            Debug.Log("Player Starting", gameObject);
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (isLocalPlayer && Input.GetKeyDown(KeyCode.Space))
                CmdChangeScene();
            if (isLocalPlayer && Input.GetKeyDown(KeyCode.K))
                CmdAddInt();
        }

        [Command]
        void CmdAddInt()
        {
            ints.Add(10);
        }

        [Command]
        void CmdChangeScene()
        {
            if (NetworkManager.networkSceneName.Contains("Online1"))
                NetworkManager.singleton.ServerChangeScene("Online2");
            else if (NetworkManager.networkSceneName.Contains("Online2"))
                NetworkManager.singleton.ServerChangeScene("Online1");
            else
                Debug.LogWarning(NetworkManager.networkSceneName);
        }

        void OnInventoryUpdated(SyncList<int>.Operation op, int index, int oldItem, int newItem)
        {
            Debug.Log($"OnInventoryUpdated {op} {index} {oldItem} {newItem}");

            switch (op)
            {
                case SyncList<int>.Operation.OP_ADD:
                    // index is where it was added into the list
                    // newItem is the new item
                    break;
                case SyncList<int>.Operation.OP_INSERT:
                    // index is where it was inserted into the list
                    // newItem is the new item
                    break;
                case SyncList<int>.Operation.OP_REMOVEAT:
                    // index is where it was removed from the list
                    // oldItem is the item that was removed
                    break;
                case SyncList<int>.Operation.OP_SET:
                    // index is of the item that was changed
                    // oldItem is the previous value for the item at the index
                    // newItem is the new value for the item at the index
                    break;
                case SyncList<int>.Operation.OP_CLEAR:
                    // list got cleared
                    break;
            }
        }
    }
}