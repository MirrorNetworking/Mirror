using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class PlayerControllerRBUI : ControllerUIBase
    {
        [Serializable]
        public struct MoveTexts
        {
            public Text keyTextTurnLeft;
            public Text keyTextForward;
            public Text keyTextTurnRight;
            public Text keyTextStrafeLeft;
            public Text keyTextBack;
            public Text keyTextStrafeRight;
            public Text keyTextJump;
        }

        [Serializable]
        public struct OptionsTexts
        {
            public Text keyTextMouseSteer;
            public Text keyTextAutoRun;
            public Text keyTextToggleUI;
        }

        [SerializeField] MoveTexts moveTexts;
        [SerializeField] OptionsTexts optionsTexts;

        public void Refresh(PlayerControllerRBBase.MoveKeys moveKeys, PlayerControllerRBBase.OptionsKeys optionsKeys)
        {
            // Movement Keys
            moveTexts.keyTextTurnLeft.text = GetKeyText(moveKeys.TurnLeft);
            moveTexts.keyTextForward.text = GetKeyText(moveKeys.Forward);
            moveTexts.keyTextTurnRight.text = GetKeyText(moveKeys.TurnRight);
            moveTexts.keyTextStrafeLeft.text = GetKeyText(moveKeys.StrafeLeft);
            moveTexts.keyTextBack.text = GetKeyText(moveKeys.Back);
            moveTexts.keyTextStrafeRight.text = GetKeyText(moveKeys.StrafeRight);
            moveTexts.keyTextJump.text = GetKeyText(moveKeys.Jump);

            // Options Keys
            optionsTexts.keyTextMouseSteer.text = GetKeyText(optionsKeys.MouseSteer);
            optionsTexts.keyTextAutoRun.text = GetKeyText(optionsKeys.AutoRun);
            optionsTexts.keyTextToggleUI.text = GetKeyText(optionsKeys.ToggleUI);
        }
    }
}
