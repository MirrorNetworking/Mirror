using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.MultipleMatch
{
    public class CellGUI : MonoBehaviour
    {
        [SerializeField] MatchController matchController;
        [SerializeField] internal CellValue cellValue;

        [Header("GUI References")]
        [SerializeField] Image image;
        [SerializeField] Button button;

        [Header("Diagnostics - Do Not Modify")]
        [SerializeField] internal NetworkIdentity playerIdentity;


        public void Awake()
        {
            matchController.MatchCells.Add(cellValue, this);
        }

        public void MakePlay()
        {
            if (matchController.currentPlayer.isLocalPlayer)
                matchController.CmdMakePlay(cellValue);
        }

        public void SetPlayer(NetworkIdentity playerIdentity)
        {
            if (playerIdentity != null)
            {
                this.playerIdentity = playerIdentity;
                image.color = this.playerIdentity.isLocalPlayer ? Color.blue : Color.red;
                button.interactable = false;
            }
            else
            {
                this.playerIdentity = null;
                image.color = Color.white;
                button.interactable = true;
            }
        }
    }
}