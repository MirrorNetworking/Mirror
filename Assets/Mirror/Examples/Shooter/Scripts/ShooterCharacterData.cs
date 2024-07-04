using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class ShooterCharacterData : MonoBehaviour
    {
        // A reference and storage data script for most things character and customisation related.

        public static string playerName = "";
        public static int playerCharacter = 0;
        public static Color playerColour;

        public GameObject[] characterPrefabs;
        public string[] characterTitles;
        public int[] characterHealths;
        public float[] characterSpeeds;
        public string[] characterDescription;

        public GameObject[] weaponPrefabs;

        //public void Awake()
        //{
        //print("playerName: " + playerName);
        //print("characterNumber: " + characterNumber);
        //print("characterColour: " + characterColour);
        //}

    }

}