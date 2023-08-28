using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.CharacterSelection
{
    public class CharacterCustomisation : NetworkBehaviour
    {
        public MeshRenderer[] characterRenderers;

        // Color32 packs to 4 bytes
        [SyncVar(hook = nameof(SetColor))]
        public Color32 characterColour;

        // Unity clones the material when GetComponent<Renderer>().material is called
        // Cache it here and destroy it in OnDestroy to prevent a memory leak
        Material cachedMaterial;

        void SetColor(Color32 _, Color32 newColor)
        {
            if (cachedMaterial == null) cachedMaterial = GetComponentInChildren<Renderer>().material;
            cachedMaterial.color = newColor;
        }

        void OnDestroy()
        {
            Destroy(cachedMaterial);
        }

        public void AssignColours()
        {
            foreach (MeshRenderer meshRenderer in characterRenderers)
            {
                cachedMaterial = meshRenderer.material;
                cachedMaterial.color = characterColour;
            }
        }

        //public override void OnStartServer()
        //{
        //    base.OnStartServer();

        //    // This script is on players that are respawned repeatedly
        //    // so once the color has been set, don't change it.
        //    if (color == Color.black)
        //        color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        //}
    }
}