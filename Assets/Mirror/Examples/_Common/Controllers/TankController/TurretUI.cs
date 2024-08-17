using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class TurretUI : ContollerUIBase
    {
        [Serializable]
        public struct MoveTexts
        {
            public Text keyTextPitchUp;
            public Text keyTextPitchDown;
            public Text keyTextTurnLeft;
            public Text keyTextTurnRight;
        }

        [SerializeField] MoveTexts moveTexts;

        public void Refresh(TankTurretBase.MoveKeys moveKeys, TankTurretBase.OptionsKeys optionsKeys)
        {
            // Movement Keys
            moveTexts.keyTextPitchUp.text = GetKeyText(moveKeys.PitchUp);
            moveTexts.keyTextPitchDown.text = GetKeyText(moveKeys.PitchDown);
            moveTexts.keyTextTurnLeft.text = GetKeyText(moveKeys.TurnLeft);
            moveTexts.keyTextTurnRight.text = GetKeyText(moveKeys.TurnRight);
        }
    }
}
