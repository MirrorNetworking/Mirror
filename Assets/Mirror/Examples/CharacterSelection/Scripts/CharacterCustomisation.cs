using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.CharacterSelection
{
    public class CharacterCustomisation : NetworkBehaviour
    {
        public TextMesh textMeshName;
        [SyncVar(hook = nameof(HookSetName))]
        public string playerName = "";

        void HookSetName(string _old, string _new)
        {
            AssignName();
        }

        [SyncVar]
        public int characterNumber = 0;
        
        [SyncVar(hook = nameof(HookSetColor))]
        public Color32 characterColour;
        private Material cachedMaterial;
        public MeshRenderer[] characterRenderers;

        void HookSetColor(Color32 _old, Color32 _new)
        {
            Debug.Log("HookSetColor");
            AssignColours();
        }

        //public override void OnStartAuthority()
        //{
        //}

        //[Command]
        //void CmdSetupCharacter(string _playerName, int _characterNumber, Color32 _characterColour)
        //{
        //    playerName = _playerName;
        //    characterNumber = _characterNumber;
        //    characterColour = _characterColour;
        //}

        public void AssignColours()
        {
            foreach (MeshRenderer meshRenderer in characterRenderers)
            {
                cachedMaterial = meshRenderer.material;
                cachedMaterial.color = characterColour;
            }
        }

        void OnDestroy()
        {
            if (cachedMaterial) { Destroy(cachedMaterial); }
        }

        public void AssignName()
        {
            textMeshName.text = playerName;
        }
    }
}