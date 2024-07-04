using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mirror.Examples.Shooter
{
    public class ShooterCharacterSelection : MonoBehaviour
    {
        // Make sure to attach these Buttons in the Inspector
        public Button buttonExit, buttonNextCharacter, buttonGo, buttonColour, buttonColourReset, buttonMenuPlay;
        public Text textTitle, textHealth, textSpeed, textAttack, textAbilities;
        public InputField inputFieldPlayerName;

        public Transform podiumPosition;
        public ShooterCharacterData characterData;
        private GameObject currentInstantiatedCharacter;
        private Player player;
        public Camera cameraObj;
        public GameObject characterDataPrefab;

        public GameObject characterUI, menuUI;

        private void Start()
        {
            characterData = Instantiate(characterDataPrefab).GetComponent<ShooterCharacterData>();

            buttonExit.onClick.AddListener(ButtonExit);
            buttonMenuPlay.onClick.AddListener(ButtonMenuPlay);
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
            // Exit selection UI back to menu, or quit game for now.
            if (characterUI.activeSelf)
            {
                characterUI.SetActive(false);
                menuUI.SetActive(true);
            }
            else
            {
                Debug.Log("Quitting Game");
                Application.Quit();
            }
        }

        public void ButtonMenuPlay()
        {
            //Debug.Log("ButtonPlay");
            menuUI.SetActive(false);
            characterUI.SetActive(true);
        }

        public void ButtonGo()
        {
            //Debug.Log("ButtonGo");
            SaveData();
            SceneManager.LoadScene("MirrorShooter");
        }

        public void ButtonNextCharacter()
        {
            //Debug.Log("ButtonNextCharacter");

            ShooterCharacterData.playerCharacter += 1;
            if (ShooterCharacterData.playerCharacter >= characterData.characterPrefabs.Length)
            {
                ShooterCharacterData.playerCharacter = 1;
            }
            SetupCharacters();
        }

        public void ButtonColour()
        {
            //Debug.Log("ButtonColour");
            ShooterCharacterData.playerColour = Random.ColorHSV(0f, 1f, 1f, 1f, 0f, 1f);
            SetupCharacterColours();
        }

        public void ButtonColourReset()
        {
            //Debug.Log("ButtonColourReset ");
            ShooterCharacterData.playerColour = new Color(0, 0, 0, 0);
            SetupCharacters();
        }

        private void SetupCharacters()
        {
            textTitle.text = "" + characterData.characterTitles[ShooterCharacterData.playerCharacter];
            textHealth.text = "Health: " + characterData.characterHealths[ShooterCharacterData.playerCharacter];
            textSpeed.text = "Speed: " + characterData.characterSpeeds[ShooterCharacterData.playerCharacter];
            textAbilities.text = "Abilities:\n" + characterData.characterDescription[ShooterCharacterData.playerCharacter];

            if (currentInstantiatedCharacter)
            {
                Destroy(currentInstantiatedCharacter);
            }
            currentInstantiatedCharacter = Instantiate(characterData.characterPrefabs[ShooterCharacterData.playerCharacter]);
            currentInstantiatedCharacter.transform.position = podiumPosition.position;
            currentInstantiatedCharacter.transform.rotation = podiumPosition.rotation;
           // currentInstantiatedCharacter.transform.SetParent(this.transform.root);
            player = currentInstantiatedCharacter.GetComponent<Player>();
            player.characterData = characterData;

            SetupCharacterColours();
            SetupPlayerName();
            SetupPlayerHealth();
        }

        public void SetupCharacterColours()
        {
            // Debug.Log("SetupCharacterColours");
            if (ShooterCharacterData.playerColour != new Color(0, 0, 0, 0))
            {
                player.playerColour = ShooterCharacterData.playerColour;
                player.HookSetColor(player.playerColour, player.playerColour);
            }
        }

        public void InputFieldChangedPlayerName()
        {
            //Debug.Log("InputFieldChangedPlayerName");
            ShooterCharacterData.playerName = inputFieldPlayerName.text;
            SetupPlayerName();
        }

        public void SetupPlayerName()
        {
            //Debug.Log("SetupPlayerName");
            if (player)
            {
                player.playerName = ShooterCharacterData.playerName;
                player.OnNameChanged("", "");
            }
        }

        public void SetupPlayerHealth()
        {
            //Debug.Log("SetupPlayerHealth");
            if (player)
            {
                player.playerHealth = characterData.characterHealths[ShooterCharacterData.playerCharacter];
                player.OnHealthChanged(0, 0);
            }
        }

        public void LoadData()
        {
            //PlayerPrefs.DeleteAll();
            ShooterCharacterData.playerName = PlayerPrefs.GetString("playerName");
            ShooterCharacterData.playerCharacter = PlayerPrefs.GetInt("playerNumber");
            Color newCol;
            if (ColorUtility.TryParseHtmlString(PlayerPrefs.GetString("playerColourHex"), out newCol))
            {
                ShooterCharacterData.playerColour = newCol;
            }
            // check if the static save data has been pre-set
            if (ShooterCharacterData.playerName != "")
            {
                if (inputFieldPlayerName)
                {
                    inputFieldPlayerName.text = ShooterCharacterData.playerName;
                }
            }
            else
            {
                ShooterCharacterData.playerName = "Player Name";
            }

            if (ShooterCharacterData.playerCharacter <= 0 || ShooterCharacterData.playerCharacter >= characterData.characterPrefabs.Length)
            {
                ShooterCharacterData.playerCharacter = UnityEngine.Random.Range(1, characterData.characterPrefabs.Length);
            }
        }

        void SaveData()
        {
            PlayerPrefs.SetString("playerName", ShooterCharacterData.playerName);
            PlayerPrefs.SetInt("playerNumber", ShooterCharacterData.playerCharacter);
            PlayerPrefs.SetString("playerColourHex", "#"+ColorUtility.ToHtmlStringRGBA(ShooterCharacterData.playerColour));
            PlayerPrefs.Save();
        }
    }
}