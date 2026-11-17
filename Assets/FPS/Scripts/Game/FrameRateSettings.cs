using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Caps the frame rate to the monitor's refresh rate (60 by default).
    /// An uncapped frame rate makes laptops with integrated graphics heat up and throttle,
    /// which shows up as stutter. A steady capped rate feels much smoother.
    /// Runs automatically when the game starts, no scene setup needed.
    /// </summary>
    public static class FrameRateSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            double hz = Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = hz >= 30 ? Mathf.RoundToInt((float)hz) : 60;
        }
    }
}
