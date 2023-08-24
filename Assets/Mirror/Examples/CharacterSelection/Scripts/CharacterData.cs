using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.CharacterSelection
{
    public class CharacterData : MonoBehaviour
    {
        public static CharacterData characterDataSingleton { get; private set; }

        public GameObject[] characterPrefabs;

        public string[] characterTitles;
        public int[] characterHealths;
        public float[] characterSpeeds;
        public int[] characterAttack;
        public string[] characterAbilities;

        public void Awake()
        {
            characterDataSingleton = this;
        }

    }

}