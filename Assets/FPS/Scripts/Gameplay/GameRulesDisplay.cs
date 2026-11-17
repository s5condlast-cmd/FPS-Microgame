using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Shows the game's rules, victory condition and loss condition as on-screen messages when the level starts.
    /// </summary>
    public class GameRulesDisplay : MonoBehaviour
    {
        [Tooltip("Delay before the first message appears")]
        public float InitialDelay = 1f;

        [Tooltip("Delay between two messages")]
        public float DelayBetweenMessages = 2.5f;

        [TextArea]
        public string[] Messages =
        {
            "RULES: Move WASD, Jump SPACE, Sprint SHIFT, Crouch C, Dash F",
            "Weapons: 1 = Blaster (shoot), 2 = Knife (melee). Left click to attack",
            "VICTORY: Eliminate every enemy robot in the level",
            "DEFEAT: Your health reaches 0 or you fall off the map",
        };

        void Start()
        {
            for (int i = 0; i < Messages.Length; i++)
            {
                DisplayMessageEvent evt = Events.DisplayMessageEvent;
                evt.Message = Messages[i];
                evt.DelayBeforeDisplay = InitialDelay + i * DelayBetweenMessages;
                EventManager.Broadcast(evt);
            }
        }
    }
}
