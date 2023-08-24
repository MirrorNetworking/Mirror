using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.CharacterSelection
{
    
    public class CanvasReferencer : MonoBehaviour
    {
        // Make sure to attach these Buttons in the Inspector
        public Button buttonExit, buttonNextCharacter;
        public Text textTitle, textHealth, textSpeed, textAttack, textAbilities;

        public Transform podiumPosition;
        private int currentlySelectedCharacter = 3;
        private CharacterData characterData;
        private GameObject currentInstantiatedCharacter;

        private void Start()
        {
            characterData = CharacterData.characterDataSingleton;
            if (characterData == null)
            {
                Debug.Log("Add CharacterData prefab singleton into the scene.");
                return;
            }

            buttonExit.onClick.AddListener(ButtonExit);
            buttonNextCharacter.onClick.AddListener(ButtonNextCharacter);

            SetupCharacterUI();
        }

        public void ButtonExit()
        {
            Debug.Log("ButtonExit");
        }

        public void ButtonNextCharacter()
        {
           // Debug.Log("ButtonNextCharacter");

            currentlySelectedCharacter += 1;
            if (currentlySelectedCharacter >= characterData.characterPrefabs.Length)
            {
                currentlySelectedCharacter = 1;
            }
            SetupCharacterUI();
        }

        private void SetupCharacterUI()
        {
            textTitle.text = "" + characterData.characterTitles[currentlySelectedCharacter];
            textHealth.text = "Health: " + characterData.characterHealths[currentlySelectedCharacter];
            textSpeed.text = "Speed: " + characterData.characterSpeeds[currentlySelectedCharacter];
            textAttack.text = "Attack: " + characterData.characterAttack[currentlySelectedCharacter];
            textAbilities.text = "Abilities:\n" + characterData.characterAbilities[currentlySelectedCharacter];

            if (currentInstantiatedCharacter)
            {
                Destroy(currentInstantiatedCharacter);
            }
            currentInstantiatedCharacter = Instantiate(characterData.characterPrefabs[currentlySelectedCharacter]);
            currentInstantiatedCharacter.transform.position = podiumPosition.position;
            currentInstantiatedCharacter.transform.rotation = podiumPosition.rotation;
        }
    }
}