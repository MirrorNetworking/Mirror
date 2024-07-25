using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace Edgegap.Editor
{
    /// <summary>Slightly shake a UI button to indicate attention.</summary>
    public class ButtonShaker
    {
        const string SHAKE_START_CLASS = "shakeStart";
        const string SHAKE_STOP_CLASS = "shakeEnd";
        private Button targetButton;

        public ButtonShaker(Button buttonToShake) =>
            targetButton = buttonToShake;

        /// <summary>Shake the button x times for x msDelayBetweenShakes each.
        /// 1 shake = 1 bigger -> followed by 1 smaller.</summary>
        /// <param name="msDelayBetweenShakes"></param>
        /// <param name="iterations"># of shakes</param>
        public async Task ApplyShakeAsync(int msDelayBetweenShakes = 40, int iterations = 2)
        {
            for (int i = 0; i < iterations; i++)
                await shakeOnce(msDelayBetweenShakes);
        }

        private async Task shakeOnce(int msDelayBetweenShakes)
        {
            targetButton.AddToClassList(SHAKE_START_CLASS);
            await Task.Delay(msDelayBetweenShakes); // duration of the first transition
            targetButton.RemoveFromClassList(SHAKE_START_CLASS);
            
            targetButton.AddToClassList(SHAKE_STOP_CLASS);
            await Task.Delay(msDelayBetweenShakes); // duration of the second transition
            targetButton.RemoveFromClassList(SHAKE_STOP_CLASS);
        }
    }
}
