using UnityEngine;
using Mirror;

namespace TestNT
{
    public class PlayerColor : NetworkBehaviour
    {
        // Unity clones the material when GetComponent<Renderer>().material is called
        // Cache it here and destroy it in OnDestroy to prevent a memory leak
        Material cachedMaterial;

        public Renderer rend;

        // Color32 packs to 4 bytes
        [SyncVar(hook = nameof(SetColor))]
        public Color32 color = Color.black;

        void SetColor(Color32 _, Color32 newColor)
        {
            if (cachedMaterial == null) cachedMaterial = rend.material;
            cachedMaterial.color = newColor;
        }

        public override void OnStartServer()
        {
            color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        }

        void OnDestroy()
        {
            Destroy(cachedMaterial);
        }
    }
}
