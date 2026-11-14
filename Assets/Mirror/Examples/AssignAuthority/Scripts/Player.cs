using UnityEngine;

namespace Mirror.Examples.AssignAuthority
{
    [AddComponentMenu("")]
    public class Player : NetworkBehaviour
    {
        [SyncVar]
        public Color Color;

        public override void OnStartServer()
        {
            Color = Random.ColorHSV(0, 1, 1, 1, 1, 1);
        }
    }
}
