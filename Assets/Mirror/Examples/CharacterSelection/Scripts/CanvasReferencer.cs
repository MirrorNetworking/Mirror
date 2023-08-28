using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mirror.Examples.CharacterSelection
{
    
    public class CanvasReferencer : MonoBehaviour
    {
        // Make sure to attach these Buttons in the Inspector
        public Button buttonExit, buttonNextCharacter, buttonGo, buttonColour, buttonColourReset;
        public Text textTitle, textHealth, textSpeed, textAttack, textAbilities;
        public InputField inputFieldPlayerName;

        public Transform podiumPosition;
        private int currentlySelectedCharacter = 1;
        private CharacterData characterData;
        private GameObject currentInstantiatedCharacter;
        private CharacterCustomisation characterCustomisation;

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
            buttonGo.onClick.AddListener(ButtonGo);
            buttonColour.onClick.AddListener(ButtonColour);
            buttonColourReset.onClick.AddListener(ButtonColourReset);
            //Adds a listener to the main input field and invokes a method when the value changes.
            inputFieldPlayerName.onValueChanged.AddListener(delegate { InputFieldChangedPlayerName(); });

            LoadData();
            SetupCharacters();
        }

        public void ButtonExit()
        {
            Debug.Log("ButtonExit");
        }

        public void ButtonGo()
        {
            Debug.Log("ButtonGo");
            SceneManager.LoadScene("SceneMap");
        }

        public void ButtonNextCharacter()
        {
            Debug.Log("ButtonNextCharacter");

            currentlySelectedCharacter += 1;
            if (currentlySelectedCharacter >= characterData.characterPrefabs.Length)
            {
                currentlySelectedCharacter = 1;
            }
            SetupCharacters();

            StaticVariables.characterNumber = currentlySelectedCharacter;
        }

        public void ButtonColour()
        {
            Debug.Log("ButtonColour");
            StaticVariables.characterColourActive = true;
            StaticVariables.characterColour = Random.ColorHSV(0f, 1f, 1f, 1f, 0f, 1f);
            SetupCharacterColours();
        }

        public void ButtonColourReset()
        {
            Debug.Log("ButtonColourReset ");
            StaticVariables.characterColourActive = false;
            SetupCharacters();
        }

        private void SetupCharacters()
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
            characterCustomisation = currentInstantiatedCharacter.GetComponent<CharacterCustomisation>();

            SetupCharacterColours();
            SetupPlayerName();
        }

        public void SetupCharacterColours()
        {
            Debug.Log("SetupCharacterColours");
            if (StaticVariables.characterColourActive == true)
            {
                characterCustomisation.characterColour = StaticVariables.characterColour;
                characterCustomisation.AssignColours();
            }
        }

        public void InputFieldChangedPlayerName()
        {
            Debug.Log("InputFieldChangedPlayerName");
            StaticVariables.playerName = inputFieldPlayerName.text;
            SetupPlayerName();
        }

        public void SetupPlayerName()
        {
            Debug.Log("SetupPlayerName");
            characterCustomisation.playerName = StaticVariables.playerName;
            characterCustomisation.AssignName();
        }

        public void LoadData()
        {
            // check if the static save data has been pre-set
            if (StaticVariables.playerName != "")
            {
                inputFieldPlayerName.text = StaticVariables.playerName;
            }
            else
            {
                StaticVariables.playerName = "Player Name";
            }

            // check that prefab is set, or exists for saved character number data
            if (StaticVariables.characterNumber > 0 && StaticVariables.characterNumber < characterData.characterPrefabs.Length)
            {
                currentlySelectedCharacter = StaticVariables.characterNumber;
            }
            else
            {
                StaticVariables.characterNumber = currentlySelectedCharacter;
            }
        }
    }
}