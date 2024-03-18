using UnityEngine;

namespace Mirror.Examples.CharacterSelection
{
    // we will use static variables to pass data between scenes
    // this could also be done using other methods
    public class StaticVariables : MonoBehaviour
    {
        public static string playerName = "";
        public static int characterNumber = 0;
        public static Color characterColour;
    }
}