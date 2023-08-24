using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.CharacterSelection
{
    public class ScriptAnimations : MonoBehaviour
    {
        //public float rotSpeed = 3;
        public float minimum = 0.1f;
        public float maximum = 0.5f;

        private float yPos;
        public float bounceSpeed = 3;
        private float yStartPosition;

        private void Start()
        {
            yStartPosition = this.transform.position.y;
        }

        // Update is called once per frame
        void Update()
        {
            float sinValue = Mathf.Sin(Time.time * bounceSpeed);

            yPos = Mathf.Lerp(maximum, minimum, Mathf.Abs((1.0f + sinValue) / 2.0f));
            transform.position = new Vector3(transform.position.x, yStartPosition + yPos, transform.position.z);

            //Rotate
            //transform.Rotate(Vector3.up, Time.deltaTime * rotSpeed);

        }
    }

    //credits https://stackoverflow.com/questions/67322860/how-do-i-make-a-simple-idle-bobbing-motion-animation

}