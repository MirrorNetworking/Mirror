using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Mirror.Examples.CharacterSelection.NetworkManagerCharacterSelection;

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
        private CharacterSelection characterSelection;
        public SceneReferencer sceneReferencer;
        public Camera cameraObj;

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
            //Debug.Log("ButtonExit");
            if (sceneReferencer)
            {
                sceneReferencer.CloseCharacterSelection();
            }
        }

        public void ButtonGo()
        {
            //Debug.Log("ButtonGo");

            // presumes we're already in-game
            if (sceneReferencer && NetworkClient.active)
            {

                // You could check if prefab (character number) has not changed, and if so just update the sync vars and hooks of current prefab, this would call a command from your player.
                // this is not fully setup for this example, but provides a minor template to follow if needed
                //NetworkClient.localPlayer.GetComponent<CharacterSelection>().CmdSetupCharacter(StaticVariables.playerName, StaticVariables.characterColour);

                CreateCharacterMessage _characterMessage = new CreateCharacterMessage
                {
                    playerName = StaticVariables.playerName,
                    characterNumber = StaticVariables.characterNumber,
                    characterColour = StaticVariables.characterColour
                };

                ReplaceCharacterMessage replaceCharacterMessage = new ReplaceCharacterMessage
                {
                    createCharacterMessage = _characterMessage
                };
                NetworkManagerCharacterSelection.singleton.ReplaceCharacter(replaceCharacterMessage);
                sceneReferencer.CloseCharacterSelection();
            }
            else
            {
                // not in-game
                SceneManager.LoadScene("MirrorCharacterSelection");
            }
        }

        public void ButtonNextCharacter()
        {
            //Debug.Log("ButtonNextCharacter");

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
            //Debug.Log("ButtonColour");
            StaticVariables.characterColour = Random.ColorHSV(0f, 1f, 1f, 1f, 0f, 1f);
            SetupCharacterColours();
        }

        public void ButtonColourReset()
        {
            //Debug.Log("ButtonColourReset ");
            StaticVariables.characterColour = new Color(0, 0, 0, 0);
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
            characterSelection = currentInstantiatedCharacter.GetComponent<CharacterSelection>();
            currentInstantiatedCharacter.transform.SetParent(this.transform.root);

            SetupCharacterColours();
            SetupPlayerName();

            if (cameraObj)
            {
                characterSelection.floatingInfo.forward = cameraObj.transform.forward;
            }
        }

        public void SetupCharacterColours()
        {
           // Debug.Log("SetupCharacterColours");
            if (StaticVariables.characterColour != new Color(0, 0, 0, 0))
            {
                characterSelection.characterColour = StaticVariables.characterColour;
                characterSelection.AssignColours();
            }
        }

        public void InputFieldChangedPlayerName()
        {
            //Debug.Log("InputFieldChangedPlayerName");
            StaticVariables.playerName = inputFieldPlayerName.text;
            SetupPlayerName();
        }

        public void SetupPlayerName()
        {
            //Debug.Log("SetupPlayerName");
            if (characterSelection)
            {
                characterSelection.playerName = StaticVariables.playerName;
                characterSelection.AssignName();
            }
        }

        public void LoadData()
        {
            // check if the static save data has been pre-set
            if (StaticVariables.playerName != "")
            {
                if (inputFieldPlayerName)
                {
                    inputFieldPlayerName.text = StaticVariables.playerName;
                }
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