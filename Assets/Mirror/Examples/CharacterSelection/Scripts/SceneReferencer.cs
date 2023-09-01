using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mirror;

namespace Mirror.Examples.CharacterSelection
{
    
    public class SceneReferencer : NetworkBehaviour
    {
        // Make sure to attach these Buttons in the Inspector
        public Button buttonCharacterSelection;

        private CharacterData characterData;
        public GameObject characterSelectionObject;
        public GameObject sceneObjects;
        public GameObject cameraObject;

        private void Start()
        {
            characterData = CharacterData.characterDataSingleton;
            if (characterData == null)
            {
                Debug.Log("Add CharacterData prefab singleton into the scene.");
                return;
            }

            buttonCharacterSelection.onClick.AddListener(ButtonCharacterSelection);

        }

        public void ButtonCharacterSelection()
        {
            Debug.Log("ButtonCharacterSelection");
            cameraObject.SetActive(false);
            sceneObjects.SetActive(false);
            characterSelectionObject.SetActive(true);
            this.GetComponent<Canvas>().enabled = false;
        }

        public void CloseCharacterSelection()
        {
            Debug.Log("CloseCharacterSelection");
            cameraObject.SetActive(true);
            characterSelectionObject.SetActive(false);
            sceneObjects.SetActive(true);
            this.GetComponent<Canvas>().enabled = true;
        }
    }
}