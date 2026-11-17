using System.Collections.Generic;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Campaign state that survives scene loads (Level 1 -> 2 -> 3 -> Boss).
    /// It remembers the last level played (for "Retry") and the weapons the
    /// player had when they finished the previous level.
    /// </summary>
    public static class CampaignProgress
    {
        public static string LastPlayedLevel = "";

        /// <summary>Names of the weapon prefabs carried over from the previous level.</summary>
        public static readonly HashSet<string> CarriedWeapons = new HashSet<string>();

        public static void Reset()
        {
            CarriedWeapons.Clear();
        }
    }
}
