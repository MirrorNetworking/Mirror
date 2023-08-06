using UnityEngine;
using UnityEngine.UI;
namespace Mirror.Examples.CouchCoop
{
    public class CanvasScript : MonoBehaviour
    {
        public CouchPlayerManager couchPlayerManager; // Sets itself
        public Button buttonAddPlayer, buttonRemovePlayer; // Make sure to attach these Buttons in the Inspector

        private void Start()
        {
            buttonAddPlayer.onClick.AddListener(ButtonAddPlayer);
            buttonRemovePlayer.onClick.AddListener(ButtonRemovePlayer);
        }

        private void ButtonAddPlayer()
        {
            if (couchPlayerManager == null)
            { Debug.Log("Start game first."); return; }

            couchPlayerManager.CmdAddPlayer();
        }

        private void ButtonRemovePlayer()
        {
            if (couchPlayerManager == null)
            { Debug.Log("Start game first."); return; }

            couchPlayerManager.CmdRemovePlayer();
        }
    }
}