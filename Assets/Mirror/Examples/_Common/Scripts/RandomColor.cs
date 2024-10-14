using UnityEngine;

namespace Mirror.Examples.Common
{
    [AddComponentMenu("")]
    public class RandomColor : NetworkBehaviour
    {
        // Unity clones the material when GetComponent<Renderer>().material is called
        // Cache it here and destroy it in OnDestroy to prevent a memory leak
        Material cachedMaterial;

        // Color32 packs to 4 bytes
        [SyncVar(hook = nameof(SetColor))]
        public Color32 color = Color.black;

        void SetColor(Color32 _, Color32 newColor)
        {
            if (cachedMaterial == null) cachedMaterial = GetComponentInChildren<Renderer>().material;
            cachedMaterial.color = newColor;
        }

        public override void OnStartServer()
        {
            // Only set the color once. Players / objects may be unspawned and
            // respawned and we don't want to keep changing their colors.
            if (color == Color.black)
                color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        }

        void OnDestroy()
        {
            Destroy(cachedMaterial);
        }
    }
}
