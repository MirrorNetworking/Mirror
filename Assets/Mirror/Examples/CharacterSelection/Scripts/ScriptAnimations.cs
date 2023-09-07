using UnityEngine;

namespace Mirror.Examples.CharacterSelection
{
    // A fun little bob script for characters.
    // You could reference this and change values depending on characters state, idle, walk, run.

    public class ScriptAnimations : MonoBehaviour
    {
        public float minimum = 0.1f;
        public float maximum = 0.5f;

        private float yPos;
        public float bounceSpeed = 3;
        private float yStartPosition;

        private void Start()
        {
            yStartPosition = this.transform.localPosition.y;
        }

        void Update()
        {
            float sinValue = Mathf.Sin(Time.time * bounceSpeed);

            yPos = Mathf.Lerp(maximum, minimum, Mathf.Abs((1.0f + sinValue) / 2.0f));
            transform.localPosition = new Vector3(transform.localPosition.x, yStartPosition + yPos, transform.localPosition.z);
        }
    }

    //credits https://stackoverflow.com/questions/67322860/how-do-i-make-a-simple-idle-bobbing-motion-animation

}