using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Per-level campaign glue:
    ///  - shows the level title when the level starts
    ///  - gives back the weapons collected in previous levels
    ///  - saves the player's weapons when the level is won
    /// </summary>
    public class CampaignLevel : MonoBehaviour
    {
        [Tooltip("Title shown when the level starts")]
        public string LevelTitle = "Level 1";

        [Tooltip("Sub title shown under the level title")]
        public string LevelSubtitle = "";

        [Tooltip("First level of the campaign: clears the carried weapons")]
        public bool IsFirstLevel;

        [Tooltip("Every weapon prefab that can be carried between levels")]
        public List<WeaponController> KnownWeapons = new List<WeaponController>();

        PlayerWeaponsManager m_WeaponsManager;

        void Awake()
        {
            CampaignProgress.LastPlayedLevel = SceneManager.GetActiveScene().name;
            if (IsFirstLevel)
                CampaignProgress.Reset();

            EventManager.AddListener<AllObjectivesCompletedEvent>(OnLevelWon);
        }

        IEnumerator Start()
        {
            m_WeaponsManager = FindAnyObjectByType<PlayerWeaponsManager>();

            DisplayMessageEvent title = Events.DisplayMessageEvent;
            title.Message = string.IsNullOrEmpty(LevelSubtitle) ? LevelTitle : LevelTitle + " - " + LevelSubtitle;
            title.DelayBeforeDisplay = 0.5f;
            EventManager.Broadcast(title);

            // wait for PlayerWeaponsManager.Start to equip the starting weapons
            yield return null;

            if (m_WeaponsManager == null)
                yield break;

            foreach (var weapon in KnownWeapons)
            {
                if (weapon != null && CampaignProgress.CarriedWeapons.Contains(weapon.name))
                    m_WeaponsManager.AddWeapon(weapon);
            }
        }

        void OnLevelWon(AllObjectivesCompletedEvent evt)
        {
            if (m_WeaponsManager == null)
                return;

            for (int i = 0; i < 9; i++)
            {
                var w = m_WeaponsManager.GetWeaponAtSlotIndex(i);
                if (w != null && w.SourcePrefab != null)
                    CampaignProgress.CarriedWeapons.Add(w.SourcePrefab.name);
            }
        }

        void OnDestroy()
        {
            EventManager.RemoveListener<AllObjectivesCompletedEvent>(OnLevelWon);
        }
    }
}
