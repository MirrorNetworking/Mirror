using UnityEngine;

namespace Mirror.Examples.SyncDir // ".SyncDirection" would overshadow the enum
{
    public class Player : NetworkBehaviour
    {
        public TextMesh textMesh;
        [SyncVar] public int health;
        public Color localColor = Color.white;

        public override void OnStartLocalPlayer()
        {
            textMesh.color = localColor;
        }

        void Update()
        {
            // show health for everyone
            textMesh.text = health.ToString();

            // space bar increases health for local player.
            // note that trusting the client is a bad idea, especially with health.
            // SyncDirection is usually used for movement.
            //
            // when using custom OnSerialize, the custom OnDeserialize can still
            // safely validate client data (check position, velocity etc.).
            // this is why it's named SyncDirection, and not ClientAuthority.
            // because the server can still validate the client's data first.
            //
            // try to change SyncDirection to ServerToClient in the editor.
            // then restart the game, clients won't be allowed to change their
            // own health anymore.
            if (isLocalPlayer)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                    ++health;
            }
        }

        // show instructions
        void OnGUI()
        {
            if (!isLocalPlayer) return;

            int width = 250;
            int height = 20;
            GUI.color = localColor;
            GUI.Label(
                new Rect(Screen.width / 2 - width / 2, Screen.height / 2 - height / 2, width, height),
                "Press Space to increase your own health!");
        }
    }
}
