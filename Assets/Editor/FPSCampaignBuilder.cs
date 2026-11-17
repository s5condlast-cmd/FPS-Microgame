using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using NavSurface = Unity.AI.Navigation.NavMeshSurface;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Builds the campaign: Level 1 (Outskirts), Level 2 (Canyon Compound), Level 3 (The Foundry)
/// and the final boss (Core Arena), plus the Rail Rifle weapon and the Overseer boss prefab.
/// Every level uses a generated Terrain, block-out geometry with the Microgame materials,
/// the existing enemies / pickups / objectives prefabs and the ModAssets props.
/// Menu: Tools > FPS Mods > Build Campaign (Levels 1-3 + Boss). Safe to run again (rebuilds everything).
/// </summary>
public static class FPSCampaignBuilder
{
    // ------------------------------------------------------------------ paths
    const string k_SceneFolder = "Assets/FPS/Scenes/Campaign";
    const string k_GenFolder = k_SceneFolder + "/Generated";
    const string k_ArtFolder = "Assets/FPS/Art/Campaign";

    public const string Level1 = "Level_1";
    public const string Level2 = "Level_2";
    public const string Level3 = "Level_3";
    public const string LevelBoss = "Level_Boss";

    const string P = "Assets/FPS/Prefabs/";
    const string k_GameManager = P + "GameManager.prefab";
    const string k_Player = P + "Player.prefab";
    const string k_Sun = P + "VFX/Directional Light.prefab";
    const string k_HoverBot = P + "Enemies/Enemy_HoverBot.prefab";
    const string k_Turret = P + "Enemies/Enemy_Turret.prefab";
    const string k_Boss = P + "Enemies/Enemy_Boss_Overseer.prefab";
    const string k_ObjKill = P + "ObjectiveKillEnemies.prefab";
    const string k_ObjReach = P + "ObjectiveReachPoint.prefab";
    const string k_PickHealth = P + "Pickups/Pickup_Health.prefab";
    const string k_PickShotgun = P + "Pickups/Pickup_Shotgun.prefab";
    const string k_PickLauncher = P + "Pickups/Pickup_Launcher.prefab";
    const string k_PickJetpack = P + "Pickups/Pickup_Jetpack.prefab";
    const string k_PickBlaster = P + "Pickups/Pickup_Blaster.prefab";
    const string k_PickRail = P + "Pickups/Pickup_RailRifle.prefab";
    const string k_WBlaster = P + "Weapons/Weapon_Blaster.prefab";
    const string k_WKnife = P + "Weapons/Weapon_Knife.prefab";
    const string k_WShotgun = P + "Weapons/Weapon_Shotgun.prefab";
    const string k_WLauncher = P + "Weapons/Weapon_Launcher.prefab";
    const string k_WRail = P + "Weapons/Weapon_RailRifle.prefab";
    const string k_ProjBlaster = P + "Projectiles/Projectile_Blaster.prefab";
    const string k_ProjRail = P + "Projectiles/Projectile_Rail.prefab";
    const string k_ProjHover = P + "Projectiles/Projectile_Hoverbot.prefab";
    const string k_ProjBoss = P + "Projectiles/Projectile_BossBolt.prefab";
    const string k_VfxLava = P + "VFX/VFX_Lava.prefab";
    const string k_VfxDust = P + "VFX/VFX_Dust.prefab";
    const string k_VfxAlert = P + "VFX/VFX_Alert.prefab";

    const string M = "Assets/ModAssets/Models/";
    const string k_FirTree = M + "NatureProps/FirTree.fbx";
    const string k_FallenTree = M + "NatureProps/FallenTree.fbx";
    const string k_Bush = M + "NatureProps/FlowerBush.fbx";
    const string k_Cactus = M + "NatureProps/Cactus.fbx";
    const string k_Rock = M + "SpaceProps/Asteroid.fbx";
    const string k_Crystal = M + "SpaceProps/Crystal.fbx";
    const string k_Dish = M + "SpaceProps/SatelliteDish.fbx";
    const string k_Pillar = M + "AncientProps/Pillar.fbx";
    const string k_Obelisk = M + "AncientProps/Obelisk.fbx";
    const string k_Archway = M + "AncientProps/Archway.fbx";
    const string k_RifleMesh = M + "Weapons/Mesh_Weapon_1.fbx";

    const string LM = "Assets/FPS/Art/Materials/Level/";
    const string CM = "Assets/ModAssets/Materials/Custom Materials/";

    // ------------------------------------------------------------------ shared state
    static Material s_Floor, s_FloorDark, s_Wall, s_WallDark, s_Ramp, s_Metal, s_Copper, s_Black, s_NeonBlue, s_NeonGlow, s_Lava, s_TerrainMat;
    static List<WeaponController> s_KnownWeapons;

    class Level
    {
        public Scene Scene;
        public Transform Geo, Props, Enemies, Paths, Pickups, Objectives, Fx;
        public Terrain Terrain;
        public Random Rng;
    }

    // ================================================================== ENTRY
    [MenuItem("Tools/FPS Mods/Build Campaign (Levels 1-3 + Boss)")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        try
        {
            EnsureFolder(k_SceneFolder);
            EnsureFolder(k_GenFolder);
            EnsureFolder(k_ArtFolder);

            EditorUtility.DisplayProgressBar("Campaign", "Weapons & boss", 0.05f);
            LoadMaterials();
            var rail = CreateRailRifle();
            CreateRailRiflePickup(rail);
            CreateBossPrefab();

            s_KnownWeapons = new[] { k_WBlaster, k_WKnife, k_WShotgun, k_WLauncher, k_WRail }
                .Select(p => AssetDatabase.LoadAssetAtPath<WeaponController>(p)).Where(w => w != null).ToList();

            EditorUtility.DisplayProgressBar("Campaign", "Level 1 - Outskirts", 0.2f);
            BuildLevel1();
            EditorUtility.DisplayProgressBar("Campaign", "Level 2 - Canyon Compound", 0.4f);
            BuildLevel2();
            EditorUtility.DisplayProgressBar("Campaign", "Level 3 - The Foundry", 0.6f);
            BuildLevel3();
            EditorUtility.DisplayProgressBar("Campaign", "Final Boss - Core Arena", 0.8f);
            BuildBossLevel();

            EditorUtility.DisplayProgressBar("Campaign", "Menus & build settings", 0.95f);
            UpdateBuildSettings();
            PatchMenuScene("Assets/FPS/Scenes/IntroMenu.unity", false);
            PatchMenuScene("Assets/FPS/Scenes/WinScene.unity", false);
            PatchMenuScene("Assets/FPS/Scenes/LoseScene.unity", true);

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath(Level1));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog("FPS Campaign",
            "Campaign built:\n\n" +
            "  Level 1 - Outskirts\n  Level 2 - Canyon Compound (Rail Rifle)\n  Level 3 - The Foundry\n  Final Boss - Core Arena\n\n" +
            "Scenes are in " + k_SceneFolder + " and were added to the build settings.\n" +
            "Start from IntroMenu (Play now loads Level_1).", "OK");
    }

    static string ScenePath(string name) => k_SceneFolder + "/" + name + ".unity";

    // ================================================================== LEVEL 1 : OUTSKIRTS (easy)
    // Open valley. Teaches movement, cover and hoverbots. Shotgun in a side ruin. Extraction after clearing.
    static void BuildLevel1()
    {
        var L = BeginLevel(Level1, 101);
        var layers = new[]
        {
            Layer("Grass", new Color(0.20f, 0.36f, 0.13f), new Color(0.33f, 0.50f, 0.19f), 8),
            Layer("Dirt", new Color(0.35f, 0.26f, 0.17f), new Color(0.49f, 0.38f, 0.25f), 6),
            Layer("Rock", new Color(0.34f, 0.34f, 0.35f), new Color(0.56f, 0.55f, 0.52f), 10),
        };
        float hx = 30, hz = 62;
        CreateTerrain(L, "L1", (x, z) => RectDist(x, z, 0, 0, hx, hz), 20f, 26f, layers);
        Boundary(L, hx, hz, 0, 0);

        // start pad + exit pad
        Box(L.Geo, "StartPad", new Vector3(0, 0.05f, -55), new Vector3(10, 0.3f, 10), s_FloorDark);
        Box(L.Geo, "ExtractPad", new Vector3(0, 0.05f, 55), new Vector3(10, 0.3f, 10), s_FloorDark);

        // west ruin (shotgun reward)
        Wall(L.Geo, new Vector3(-21, 0, -29), new Vector3(-12, 0, -29), 3f, 0.6f, s_Wall);
        Wall(L.Geo, new Vector3(-21, 0, -29), new Vector3(-21, 0, -20), 3f, 0.6f, s_Wall);
        Wall(L.Geo, new Vector3(-21, 0, -20), new Vector3(-17, 0, -20), 2f, 0.6f, s_Wall);

        // crates near the start
        Crate(L.Geo, new Vector3(6, 0, -38), 1.6f);
        Crate(L.Geo, new Vector3(7.8f, 0, -37), 1.6f);
        Crate(L.Geo, new Vector3(6.9f, 1.6f, -37.5f), 1.4f);
        Crate(L.Geo, new Vector3(-6, 0, -32), 2f);

        // middle ruins - low walls for crouch cover
        Wall(L.Geo, new Vector3(-13, 0, -6), new Vector3(-4, 0, -6), 1.3f, 0.6f, s_WallDark);
        Wall(L.Geo, new Vector3(4, 0, -2), new Vector3(13, 0, -2), 1.3f, 0.6f, s_WallDark);
        Wall(L.Geo, new Vector3(-2, 0, 5), new Vector3(3, 0, 5), 3f, 0.6f, s_Wall);
        Crate(L.Geo, new Vector3(-9, 0, 2), 1.8f);
        Crate(L.Geo, new Vector3(10, 0, 9), 1.8f);

        // outpost compound (open top), gates north/south, side door east
        float ox0 = -10, ox1 = 10, oz0 = 28, oz1 = 44, oh = 4f;
        Wall(L.Geo, new Vector3(ox0, 0, oz0), new Vector3(-2, 0, oz0), oh, 0.8f, s_Wall);
        Wall(L.Geo, new Vector3(2, 0, oz0), new Vector3(ox1, 0, oz0), oh, 0.8f, s_Wall);
        Wall(L.Geo, new Vector3(ox0, 0, oz1), new Vector3(-2, 0, oz1), oh, 0.8f, s_Wall);
        Wall(L.Geo, new Vector3(2, 0, oz1), new Vector3(ox1, 0, oz1), oh, 0.8f, s_Wall);
        Wall(L.Geo, new Vector3(ox0, 0, oz0), new Vector3(ox0, 0, oz1), oh, 0.8f, s_Wall);
        Wall(L.Geo, new Vector3(ox1, 0, oz0), new Vector3(ox1, 0, 34), oh, 0.8f, s_Wall);
        Wall(L.Geo, new Vector3(ox1, 0, 38), new Vector3(ox1, 0, oz1), oh, 0.8f, s_Wall);
        Crate(L.Geo, new Vector3(-5, 0, 35), 1.6f);
        Crate(L.Geo, new Vector3(5, 0, 31.5f), 1.6f);
        Box(L.Geo, "Bench", new Vector3(0, 0.6f, 40.5f), new Vector3(4, 1.2f, 1.2f), s_Metal);
        Model(L, k_Dish, new Vector3(15, 0, 47), 5f, 200f, true);

        // nature
        foreach (var p in new[] { new Vector3(-8, 0, -18), new Vector3(18, 0, 6), new Vector3(-18, 0, 12), new Vector3(16, 0, 24), new Vector3(-24, 0, -45) })
            Model(L, k_Rock, p, 2.4f, L.Rng.Next(360), true);
        Model(L, k_FallenTree, new Vector3(16, 0, -14), 1.6f, 35f, true);
        for (int i = 0; i < 9; i++)
        {
            float z = -50 + i * 12.5f;
            Model(L, k_FirTree, new Vector3(-26 + (float)L.Rng.NextDouble() * 2, 0, z), 7f + (float)L.Rng.NextDouble() * 3, L.Rng.Next(360), true);
            Model(L, k_FirTree, new Vector3(25 + (float)L.Rng.NextDouble() * 2, 0, z + 5), 7f + (float)L.Rng.NextDouble() * 3, L.Rng.Next(360), true);
        }
        for (int i = 0; i < 6; i++)
            Model(L, k_Bush, new Vector3(-20 + i * 8, 0, -45 + (i % 2) * 6), 1f, L.Rng.Next(360), false);
        ScatterOutside(L, k_FirTree, 70, hx, hz, 8f, 13f);

        BakeNavMesh(L, "L1");

        // enemies (5 hoverbots, no turrets)
        const float drop = 0.6f;
        var a1 = Hover(L, new Vector3(-6, 0, -8), drop);
        var a2 = Hover(L, new Vector3(8, 0, -6), drop);
        Path(L, "Patrol_Mid", new[] { V(-10, 0, -10), V(10, 0, -10), V(10, 0, 9), V(-10, 0, 9) }, a1, a2);
        var b1 = Hover(L, new Vector3(16, 0, 18), drop);
        Path(L, "Patrol_Field", new[] { V(16, 0, 14), V(-16, 0, 19) }, b1);
        var c1 = Hover(L, new Vector3(-5, 0, 37), drop);
        var c2 = Hover(L, new Vector3(5, 0, 40), drop);
        Path(L, "Patrol_Outpost", new[] { V(-6, 0, 32), V(6, 0, 34), V(6, 0, 41), V(-6, 0, 41) }, c1, c2);

        // pickups
        Pick(L, k_PickShotgun, new Vector3(-16.5f, 0, -24.5f));
        Pick(L, k_PickHealth, new Vector3(12, 0, -14));
        Pick(L, k_PickHealth, new Vector3(0, 0, 37));

        // objectives
        var kill = KillObjective(L, "Clear the Outskirts", "Destroy every hostile drone in the valley");
        ExtractObjective(L, new Vector3(0, 0.2f, 55), kill);

        Finish(L, Level1, new Vector3(0, 0.3f, -55), 0f, Level2,
            "Outskirts cleared! Heading to the Canyon Compound...",
            "LEVEL 1", "Outskirts", true,
            sunEuler: new Vector3(42, -35, 0), sunColor: new Color(1f, 0.95f, 0.86f),
            fogColor: new Color(0.72f, 0.80f, 0.88f), fogStart: 60, fogEnd: 260);
    }

    // ================================================================== LEVEL 2 : CANYON COMPOUND (medium)
    // Narrow canyon. Introduces turrets on watchtowers and the new Rail Rifle (long range).
    // Compound with a two-level building (roof reachable by ramp).
    static void BuildLevel2()
    {
        var L = BeginLevel(Level2, 202);
        var layers = new[]
        {
            Layer("Sand", new Color(0.68f, 0.57f, 0.40f), new Color(0.82f, 0.71f, 0.52f), 8),
            Layer("DryDirt", new Color(0.45f, 0.32f, 0.20f), new Color(0.58f, 0.43f, 0.28f), 6),
            Layer("RedRock", new Color(0.45f, 0.27f, 0.18f), new Color(0.62f, 0.42f, 0.30f), 12),
        };
        float hx = 35, hz = 66;
        CreateTerrain(L, "L2", (x, z) => RectDist(x, z, 0, 0, hx, hz), 34f, 16f, layers);
        Boundary(L, hx, hz, 0, 0);

        Box(L.Geo, "StartPad", new Vector3(0, 0.05f, -58), new Vector3(10, 0.3f, 10), s_FloorDark);
        Box(L.Geo, "ExtractPad", new Vector3(0, 0.05f, 58), new Vector3(10, 0.3f, 10), s_FloorDark);
        Box(L.Geo, "RailPlinth", new Vector3(7, 0.5f, -50), new Vector3(2.5f, 1f, 2.5f), s_Copper);

        // trenches / sandbag lines
        Wall(L.Geo, new Vector3(-14, 0, -36), new Vector3(-6, 0, -36), 1.2f, 0.8f, s_WallDark);
        Wall(L.Geo, new Vector3(4, 0, -31), new Vector3(14, 0, -31), 1.2f, 0.8f, s_WallDark);
        Wall(L.Geo, new Vector3(-12, 0, -23), new Vector3(-2, 0, -23), 1.2f, 0.8f, s_WallDark);
        Wall(L.Geo, new Vector3(6, 0, -15), new Vector3(16, 0, -15), 1.2f, 0.8f, s_WallDark);
        Crate(L.Geo, new Vector3(0, 0, -40), 1.8f);
        Crate(L.Geo, new Vector3(-20, 0, -28), 2.2f);
        Crate(L.Geo, new Vector3(22, 0, -24), 2.2f);
        Crate(L.Geo, new Vector3(-4, 0, -9), 1.6f);
        Crate(L.Geo, new Vector3(4, 0, -6), 1.6f);

        // watchtowers (turrets on top)
        Box(L.Geo, "Tower_W", new Vector3(-20, 3, -2), new Vector3(4, 6, 4), s_WallDark);
        Box(L.Geo, "Tower_E", new Vector3(20, 3, -2), new Vector3(4, 6, 4), s_WallDark);

        // compound walls
        float wh = 5f;
        Wall(L.Geo, new Vector3(-18, 0, 10), new Vector3(-3, 0, 10), wh, 1f, s_Wall);
        Wall(L.Geo, new Vector3(3, 0, 10), new Vector3(18, 0, 10), wh, 1f, s_Wall);
        Wall(L.Geo, new Vector3(-18, 0, 44), new Vector3(-3, 0, 44), wh, 1f, s_Wall);
        Wall(L.Geo, new Vector3(3, 0, 44), new Vector3(18, 0, 44), wh, 1f, s_Wall);
        Wall(L.Geo, new Vector3(-18, 0, 10), new Vector3(-18, 0, 44), wh, 1f, s_Wall);
        Wall(L.Geo, new Vector3(18, 0, 10), new Vector3(18, 0, 44), wh, 1f, s_Wall);

        // central building with a roof platform
        Box(L.Geo, "Building", new Vector3(0, 2, 28), new Vector3(12, 4, 8), s_WallDark);
        Ramp(L.Geo, new Vector3(-14.5f, 0, 28), new Vector3(-6, 4, 28), 3f, s_Ramp);
        Wall(L.Geo, new Vector3(-3, 4, 24.6f), new Vector3(3, 4, 24.6f), 1.2f, 0.5f, s_Wall);
        Crate(L.Geo, new Vector3(4, 4, 30), 1.4f);

        // courtyard cover
        Crate(L.Geo, new Vector3(-12, 0, 16), 1.6f);
        Crate(L.Geo, new Vector3(12, 0, 17), 1.6f);
        Crate(L.Geo, new Vector3(-12, 0, 39), 1.6f);
        Crate(L.Geo, new Vector3(12, 0, 39), 1.6f);
        Crate(L.Geo, new Vector3(10, 0, 30), 3f);

        // props
        foreach (var p in new[] { V(-26, 0, -40), V(27, 0, -10), V(-28, 0, 20), V(26, 0, 50), V(-10, 0, 52) })
            Model(L, k_Rock, p, 3f, L.Rng.Next(360), true);
        for (int i = 0; i < 10; i++)
            Model(L, k_Cactus, new Vector3((i % 2 == 0 ? -1 : 1) * (27 + (float)L.Rng.NextDouble() * 4), 0, -55 + i * 11), 3f + (float)L.Rng.NextDouble() * 2, L.Rng.Next(360), true);
        ScatterOutside(L, k_Rock, 45, hx, hz, 3f, 8f);
        Model(L, k_Dish, new Vector3(15, 0, 52), 6f, 160f, true);

        BakeNavMesh(L, "L2");

        // enemies: 6 hoverbots + 2 turrets
        const float drop = 0.45f;
        var a1 = Hover(L, V(-8, 0, -26), drop);
        var a2 = Hover(L, V(10, 0, -20), drop);
        Path(L, "Patrol_Trenches", new[] { V(-12, 0, -28), V(12, 0, -27), V(12, 0, -11), V(-12, 0, -11) }, a1, a2);
        Turret(L, V(-20, 6, -2), 160f);
        Turret(L, V(20, 6, -2), 200f);
        var b1 = Hover(L, V(-10, 0, 20), drop);
        var b2 = Hover(L, V(8, 0, 37), drop);
        Path(L, "Patrol_Courtyard", new[] { V(-13, 0, 14), V(13, 0, 13), V(14, 0, 40), V(-13, 0, 41) }, b1, b2);
        var c1 = Hover(L, V(0, 4, 28), drop);
        Path(L, "Patrol_Roof", new[] { V(-4, 4, 30), V(4, 4, 27) }, c1);
        var d1 = Hover(L, V(0, 0, 50), drop);
        Path(L, "Patrol_North", new[] { V(-8, 0, 49), V(8, 0, 51) }, d1);

        // pickups
        Pick(L, k_PickRail, V(7, 1f, -50));
        Pick(L, k_PickHealth, V(-16, 0, -38));
        Pick(L, k_PickHealth, V(14, 0, 22));
        Pick(L, k_PickHealth, V(-2, 4, 30));

        var kill = KillObjective(L, "Take the Compound", "Destroy the turrets and every drone");
        ExtractObjective(L, new Vector3(0, 0.2f, 58), kill);

        Finish(L, Level2, new Vector3(0, 0.3f, -58), 0f, Level3,
            "Compound secured! Descending into the Foundry...",
            "LEVEL 2", "Canyon Compound", false,
            sunEuler: new Vector3(58, 20, 0), sunColor: new Color(1f, 0.88f, 0.70f),
            fogColor: new Color(0.84f, 0.74f, 0.58f), fogStart: 70, fogEnd: 240);
    }

    // ================================================================== LEVEL 3 : THE FOUNDRY (hard)
    // Platforms over a lava lake (damages the player). Bridges and ramps, a high central platform,
    // jetpack for shortcuts, launcher pickup, 8 hoverbots + 3 turrets.
    static void BuildLevel3()
    {
        var L = BeginLevel(Level3, 303);
        var layers = new[]
        {
            Layer("Ash", new Color(0.11f, 0.10f, 0.10f), new Color(0.24f, 0.21f, 0.19f), 8),
            Layer("Scorched", new Color(0.30f, 0.12f, 0.06f), new Color(0.46f, 0.20f, 0.10f), 6),
            Layer("Basalt", new Color(0.16f, 0.16f, 0.17f), new Color(0.32f, 0.31f, 0.30f), 12),
        };
        float hx = 34, hz = 58;
        CreateTerrain(L, "L3", (x, z) => RectDist(x, z, 0, 0, hx, hz), 36f, 14f, layers);
        Boundary(L, hx, hz, 0, 0);

        // lava lake
        Box(L.Geo, "LavaLake", new Vector3(0, 0.05f, 3.5f), new Vector3(68, 0.1f, 81), s_Lava);

        // platforms (solid blocks)
        Box(L.Geo, "P0_Start", new Vector3(0, 2, -45), new Vector3(22, 4, 16), s_FloorDark);   // z -53..-37, top 4
        Box(L.Geo, "P1_West", new Vector3(-18, 2, -14), new Vector3(14, 4, 14), s_Floor);      // x -25..-11, z -21..-7
        Box(L.Geo, "P2_East", new Vector3(18, 2, -10), new Vector3(14, 4, 14), s_Floor);       // x 11..25, z -17..-3
        Box(L.Geo, "P3_Core", new Vector3(0, 3.5f, 12), new Vector3(16, 7, 16), s_FloorDark);  // x -8..8, z 4..20, top 7
        Box(L.Geo, "P4_Exit", new Vector3(0, 2, 38), new Vector3(24, 4, 14), s_Floor);        // z 31..45, top 4

        // bridges and ramps
        Bridge(L.Geo, V(-7, 4, -37.5f), V(-15, 4, -20.5f), 3f);
        Bridge(L.Geo, V(7, 4, -37.5f), V(15, 4, -16.5f), 3f);
        Ramp(L.Geo, V(-12, 4, -7.5f), V(-7.5f, 7, 6.5f), 3.5f, s_Ramp);
        Ramp(L.Geo, V(13, 4, -3.5f), V(7.5f, 7, 6.5f), 3.5f, s_Ramp);
        Ramp(L.Geo, V(0, 7, 19.5f), V(0, 4, 31.5f), 4f, s_Ramp);
        // escape ramps out of the lava
        Ramp(L.Geo, V(0, 0.1f, -28.5f), V(0, 4, -36.8f), 3f, s_Metal);
        Ramp(L.Geo, V(-18, 0.1f, 1), V(-18, 4, -6.8f), 3f, s_Metal);
        Ramp(L.Geo, V(32.5f, 0.1f, -10), V(24.8f, 4, -10), 3f, s_Metal);
        Ramp(L.Geo, V(0, 0.1f, 22.5f), V(0, 4, 30.8f), 3f, s_Metal);
        // ramps from the dry ground around the start / exit platforms
        Ramp(L.Geo, V(-19.5f, 0, -45), V(-11.2f, 4, -45), 3f, s_Metal);
        Ramp(L.Geo, V(19.5f, 0, -45), V(11.2f, 4, -45), 3f, s_Metal);
        Ramp(L.Geo, V(6, 0, 53.5f), V(6, 4, 45.2f), 3f, s_Metal);

        // cover
        Crate(L.Geo, V(-4, 4, -42), 1.6f);
        Crate(L.Geo, V(5, 4, -44), 1.6f);
        Wall(L.Geo, V(-22, 4, -9), V(-16, 4, -9), 1.3f, 0.5f, s_WallDark);
        Crate(L.Geo, V(-15, 4, -16), 1.8f);
        Wall(L.Geo, V(14, 4, -5), V(20, 4, -5), 1.3f, 0.5f, s_WallDark);
        Crate(L.Geo, V(21, 4, -12), 1.8f);
        foreach (var p in new[] { V(-5, 7, 7), V(5, 7, 7), V(-5, 7, 17), V(5, 7, 17) })
            Box(L.Geo, "CorePillar", p + Vector3.up * 1.75f, new Vector3(1.5f, 3.5f, 1.5f), s_Metal);
        Wall(L.Geo, V(-8, 4, 35), V(-2, 4, 35), 1.3f, 0.5f, s_WallDark);
        Wall(L.Geo, V(2, 4, 38), V(8, 4, 38), 1.3f, 0.5f, s_WallDark);

        // industrial dressing
        foreach (var p in new[] { V(-28, 0, -30), V(28, 0, -32), V(-28, 0, 10), V(28, 0, 14), V(-28, 0, 36), V(28, 0, 38) })
            Box(L.Geo, "Smokestack", p + Vector3.up * 7f, new Vector3(2.5f, 14f, 2.5f), s_Metal);
        foreach (var p in new[] { V(-10, 0.1f, -26), V(10, 0.1f, 0), V(-20, 0.1f, 22), V(20, 0.1f, 24), V(-6, 0.1f, 27) })
        {
            Spawn(k_VfxLava, L.Fx, p, 0f);
            PointLight(L.Fx, p + Vector3.up * 2f, new Color(1f, 0.45f, 0.1f), 18f, 6f);
        }
        foreach (var p in new[] { V(-24, 0, 46), V(24, 0, 48), V(-30, 0, -48), V(30, 0, -50) })
            Model(L, k_Crystal, p, 4f, L.Rng.Next(360), true);
        ScatterOutside(L, k_Rock, 40, hx, hz, 3f, 9f);

        BakeNavMesh(L, "L3");

        // lava hurts (added after the bake so the trigger doesn't end up in the NavMesh)
        var hazard = new GameObject("LavaHazard");
        hazard.transform.SetParent(L.Geo, false);
        hazard.transform.position = V(0, 0.75f, 3.5f);
        var hc = hazard.AddComponent<BoxCollider>();
        hc.size = new Vector3(68, 1.5f, 81);
        hc.isTrigger = true;
        hazard.AddComponent<HazardZone>().DamagePerSecond = 10f; // a mistake, not a death sentence

        // enemies: 6 hoverbots + 1 turret (eased difficulty)
        const float drop = 0.6f;
        var a1 = Hover(L, V(-17, 4, -14), drop);
        Path(L, "Patrol_West", new[] { V(-22, 4, -18), V(-14, 4, -18), V(-14, 4, -11), V(-22, 4, -11) }, a1);
        var b1 = Hover(L, V(17, 4, -10), drop);
        Path(L, "Patrol_East", new[] { V(14, 4, -14), V(22, 4, -14), V(22, 4, -7), V(14, 4, -7) }, b1);
        var c1 = Hover(L, V(-3, 7, 10), drop);
        var c2 = Hover(L, V(3, 7, 14), drop);
        Path(L, "Patrol_Core", new[] { V(-3, 7, 9), V(3, 7, 9), V(3, 7, 15), V(-3, 7, 15) }, c1, c2);
        Turret(L, V(0, 7, 18.5f), 180f);
        var d1 = Hover(L, V(-8, 4, 36), drop);
        var d2 = Hover(L, V(8, 4, 41), drop);
        Path(L, "Patrol_Exit", new[] { V(-9, 4, 33), V(9, 4, 33), V(9, 4, 42), V(-9, 4, 42) }, d1, d2);

        // pickups
        Pick(L, k_PickJetpack, V(-8, 4, -48));
        Pick(L, k_PickLauncher, V(-22, 4, -19));
        Pick(L, k_PickHealth, V(8, 4, -48));
        Pick(L, k_PickHealth, V(-13, 4, -20));
        Pick(L, k_PickHealth, V(24, 4, -5));
        Pick(L, k_PickHealth, V(0, 7, 5.5f));
        Pick(L, k_PickHealth, V(-10, 4, 40));
        Pick(L, k_PickHealth, V(10, 4, 34));

        var kill = KillObjective(L, "Shut Down the Foundry", "Destroy every drone and turret. Don't touch the lava!");
        ExtractObjective(L, V(-6, 4.2f, 42), kill);

        Finish(L, Level3, V(0, 4.3f, -50), 0f, LevelBoss,
            "Foundry offline. The Overseer awaits...",
            "LEVEL 3", "The Foundry", false,
            sunEuler: new Vector3(28, 60, 0), sunColor: new Color(1f, 0.55f, 0.35f),
            fogColor: new Color(0.28f, 0.12f, 0.06f), fogStart: 25, fogEnd: 160);
    }

    // ================================================================== FINAL BOSS : CORE ARENA
    // Circular arena with pillars for cover. Weapon cache in the entrance corridor.
    // The Overseer: animated mech (Quaternius, CC0), 1500 HP, shields itself and summons drones at 60% and 30%.
    static void BuildBossLevel()
    {
        var L = BeginLevel(LevelBoss, 404);
        var layers = new[]
        {
            Layer("DarkStone", new Color(0.13f, 0.12f, 0.15f), new Color(0.25f, 0.23f, 0.28f), 8),
            Layer("Glowstone", new Color(0.18f, 0.10f, 0.25f), new Color(0.32f, 0.18f, 0.42f), 6),
            Layer("CliffRock", new Color(0.20f, 0.19f, 0.22f), new Color(0.38f, 0.36f, 0.40f), 12),
        };
        const float R = 34f;
        Func<float, float, float> dist = (x, z) =>
            Mathf.Min(Mathf.Max(0f, Mathf.Sqrt(x * x + z * z) - R), RectDist(x, z, 0, -44, 6.5f, 12.5f));
        CreateTerrain(L, "LB", dist, 30f, 14f, layers);

        // boundary: ring + corridor
        BoundaryRing(L, R + 0.5f, 32);
        InvisibleWall(L, V(-7, 0, -57), V(-7, 0, -33));
        InvisibleWall(L, V(7, 0, -57), V(7, 0, -33));
        InvisibleWall(L, V(-7, 0, -57), V(7, 0, -57));

        // corridor
        Box(L.Geo, "CorridorFloor", V(0, 0.05f, -45), new Vector3(10, 0.3f, 22), s_FloorDark);
        Wall(L.Geo, V(-6, 0, -55), V(-6, 0, -34), 5f, 1f, s_WallDark);
        Wall(L.Geo, V(6, 0, -55), V(6, 0, -34), 5f, 1f, s_WallDark);
        // open gate (two posts + lintel) so nothing blocks the view into the arena
        Box(L.Geo, "GatePost", V(-5, 3.5f, -34.5f), new Vector3(1.2f, 7f, 1.2f), s_Metal);
        Box(L.Geo, "GatePost", V(5, 3.5f, -34.5f), new Vector3(1.2f, 7f, 1.2f), s_Metal);
        Box(L.Geo, "GateLintel", V(0, 7.4f, -34.5f), new Vector3(11.2f, 0.8f, 1.2f), s_NeonBlue ? s_NeonBlue : s_Metal);

        // arena
        Box(L.Geo, "ArenaCore", V(0, 0.05f, 0), new Vector3(22, 0.3f, 22), s_FloorDark);
        Box(L.Geo, "ArenaRing", V(0, 0.06f, 0), new Vector3(14, 0.3f, 14), s_NeonBlue ? s_NeonBlue : s_Floor, 45f);
        for (int i = 0; i < 8; i++)
        {
            float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 18f;
            if (Model(L, k_Pillar, p, 7f, i * 45f, true) == null)
                Box(L.Geo, "Pillar", p + Vector3.up * 3.5f, new Vector3(2.2f, 7f, 2.2f), s_Wall);
            Model(L, k_Crystal, p + new Vector3(Mathf.Sin(a + 0.4f), 0, Mathf.Cos(a + 0.4f)) * 2.2f, 1.5f, L.Rng.Next(360), false);
        }
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            var c = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 11f;
            var t = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)) * 2.5f;
            Wall(L.Geo, c - t, c + t, 1.3f, 0.6f, s_WallDark);
        }
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60f + 30f) * Mathf.Deg2Rad;
            Model(L, k_Obelisk, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 30f, 9f, i * 60f, true);
        }
        ScatterOutside(L, k_Rock, 30, 0, 0, 4f, 10f, R);

        BakeNavMesh(L, "LB");

        // atmosphere lights
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            PointLight(L.Fx, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 24f + Vector3.up * 4f, new Color(0.9f, 0.2f, 0.25f), 20f, 5f);
        }
        Spawn(k_VfxDust, L.Fx, V(0, 0, 0), 0f);

        // the boss
        // dead centre of the arena, on the open floor, facing the entrance
        var boss = Spawn(k_Boss, L.Enemies, V(0, 0.2f, 0), 180f);
        if (boss == null)
            Debug.LogError("[Campaign] Boss prefab missing.");

        // weapon cache + health
        Pick(L, k_PickRail, V(-3, 0, -47));
        Pick(L, k_PickLauncher, V(3, 0, -47));
        Pick(L, k_PickShotgun, V(0, 0, -42));
        Pick(L, k_PickHealth, V(-3.5f, 0, -38));
        Pick(L, k_PickHealth, V(3.5f, 0, -38));
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60f + 30f) * Mathf.Deg2Rad;
            Pick(L, k_PickHealth, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 25f);
        }

        KillObjective(L, "Destroy the Overseer", "Bring down the Overseer. Destroy its drones to break its shield.");

        Finish(L, LevelBoss, V(0, 0.3f, -51), 0f, "WinScene",
            "The Overseer is destroyed. YOU WIN!",
            "FINAL BOSS", "The Overseer", false,
            sunEuler: new Vector3(20, -120, 0), sunColor: new Color(0.75f, 0.55f, 0.95f),
            fogColor: new Color(0.12f, 0.06f, 0.14f), fogStart: 30, fogEnd: 170);
    }

    // ================================================================== LEVEL FRAMEWORK
    static Level BeginLevel(string name, int seed)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var L = new Level { Scene = scene, Rng = new Random(seed) };
        Header("===== GENERAL =====");
        Header("===== PLAYER =====");
        L.Geo = Header("===== LEVEL =====").transform;
        L.Props = new GameObject("Props").transform;
        L.Props.SetParent(L.Geo, false);
        L.Fx = new GameObject("FX").transform;
        L.Fx.SetParent(L.Geo, false);
        L.Enemies = Header("===== ENEMIES =====").transform;
        L.Paths = Header("===== PATHS =====").transform;
        L.Pickups = Header("===== PICKUPS =====").transform;
        L.Objectives = Header("===== OBJECTIVES =====").transform;
        return L;
    }

    static GameObject Header(string n) => new GameObject(n);

    static void Finish(Level L, string sceneName, Vector3 playerPos, float playerYaw, string nextScene, string winMessage,
        string title, string subtitle, bool firstLevel,
        Vector3 sunEuler, Color sunColor, Color fogColor, float fogStart, float fogEnd)
    {
        // game manager (managers + HUD + menus)
        var gm = Spawn(k_GameManager, null, Vector3.zero, 0f);
        var flow = gm.GetComponentInChildren<GameFlowManager>(true);
        if (flow)
        {
            flow.WinSceneName = nextScene;
            flow.WinGameMessage = winMessage;
            flow.LoseSceneName = "LoseScene";
            Rec(flow);
        }

        var campaign = gm.AddComponent<CampaignLevel>();
        campaign.LevelTitle = title;
        campaign.LevelSubtitle = subtitle;
        campaign.IsFirstLevel = firstLevel;
        campaign.KnownWeapons = new List<WeaponController>(s_KnownWeapons);

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        Spawn(k_Player, null, playerPos, playerYaw);

        var sun = Spawn(k_Sun, null, Vector3.up * 20f, 0f);
        if (sun)
        {
            sun.transform.rotation = Quaternion.Euler(sunEuler);
            Rec(sun.transform);
            var light = sun.GetComponentInChildren<Light>();
            if (light)
            {
                light.color = sunColor;
                Rec(light);
                RenderSettings.sun = light;
            }
        }

        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/FPS/Art/Materials/Skybox.mat");
        if (sky) RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;

        // order roots
        var order = new[] { "===== GENERAL =====", gm.name, es.name, "===== PLAYER =====" };
        int idx = 0;
        foreach (var n in order)
        {
            var go = L.Scene.GetRootGameObjects().FirstOrDefault(g => g.name == n);
            if (go) go.transform.SetSiblingIndex(idx++);
        }

        EditorSceneManager.MarkSceneDirty(L.Scene);
        EditorSceneManager.SaveScene(L.Scene, ScenePath(sceneName));
    }

    static void BakeNavMesh(Level L, string key)
    {
        var go = new GameObject("NavMeshSurface");
        go.transform.SetSiblingIndex(1);
        var surface = go.AddComponent<NavSurface>();
        surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();

        string path = k_GenFolder + "/NavMesh_" + key + ".asset";
        AssetDatabase.DeleteAsset(path);
        if (surface.navMeshData != null)
        {
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            EditorUtility.SetDirty(surface);
        }
        else
            Debug.LogError("[Campaign] NavMesh bake failed for " + key);
    }

    // ================================================================== TERRAIN
    static float RectDist(float x, float z, float cx, float cz, float hx, float hz)
    {
        float dx = Mathf.Max(0f, Mathf.Abs(x - cx) - hx);
        float dz = Mathf.Max(0f, Mathf.Abs(z - cz) - hz);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    const float k_TerrainSize = 320f, k_TerrainHeight = 80f, k_TerrainBase = 2f;

    static void CreateTerrain(Level L, string key, Func<float, float, float> distOutside, float rimHeight, float rimWidth, TerrainLayer[] layers)
    {
        var data = new TerrainData();
        data.heightmapResolution = 257;
        data.alphamapResolution = 256;
        data.baseMapResolution = 512;
        data.size = new Vector3(k_TerrainSize, k_TerrainHeight, k_TerrainSize);

        int res = data.heightmapResolution;
        float seed = L.Rng.Next(1000);
        var heights = new float[res, res];
        for (int zi = 0; zi < res; zi++)
        for (int xi = 0; xi < res; xi++)
        {
            float wx = -k_TerrainSize / 2 + xi * k_TerrainSize / (res - 1);
            float wz = -k_TerrainSize / 2 + zi * k_TerrainSize / (res - 1);
            float d = distOutside(wx, wz);
            float t = Mathf.SmoothStep(0f, 1f, d / rimWidth);
            float big = Mathf.PerlinNoise((wx + seed) * 0.018f, (wz + seed) * 0.018f);
            float detail = Mathf.PerlinNoise((wx - seed) * 0.07f, (wz + seed * 0.5f) * 0.07f);
            float h = t * rimHeight * (0.55f + 0.9f * big) + Mathf.Clamp01(d / 10f) * detail * 4f;
            heights[zi, xi] = Mathf.Clamp01((h + k_TerrainBase) / k_TerrainHeight);
        }

        data.SetHeights(0, 0, heights);
        data.terrainLayers = layers;

        int A = data.alphamapResolution;
        var alpha = new float[A, A, layers.Length];
        for (int z = 0; z < A; z++)
        for (int x = 0; x < A; x++)
        {
            float nx = (x + 0.5f) / A, nz = (z + 0.5f) / A;
            float wx = -k_TerrainSize / 2 + nx * k_TerrainSize, wz = -k_TerrainSize / 2 + nz * k_TerrainSize;
            float steep = data.GetSteepness(nx, nz);
            float rock = Mathf.InverseLerp(20f, 34f, steep);
            float patch = Mathf.PerlinNoise(wx * 0.05f + seed, wz * 0.05f - seed);
            float second = Mathf.Clamp01((patch - 0.55f) * 4f) * (1f - rock);
            float main = Mathf.Max(0f, 1f - rock - second);
            alpha[z, x, 0] = main;
            alpha[z, x, 1] = second;
            alpha[z, x, 2] = rock;
        }

        data.SetAlphamaps(0, 0, alpha);

        string path = k_GenFolder + "/Terrain_" + key + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(data, path);

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "Terrain";
        go.transform.SetParent(L.Geo, false);
        go.transform.position = new Vector3(-k_TerrainSize / 2, -k_TerrainBase, -k_TerrainSize / 2);
        L.Terrain = go.GetComponent<Terrain>();
        if (s_TerrainMat) L.Terrain.materialTemplate = s_TerrainMat;
        L.Terrain.basemapDistance = 400f;
    }

    static float GroundY(Level L, Vector3 p) =>
        L.Terrain ? L.Terrain.SampleHeight(p) + L.Terrain.transform.position.y : 0f;

    static TerrainLayer Layer(string name, Color a, Color b, float tile)
    {
        string layerPath = k_ArtFolder + "/Layer_" + name + ".terrainlayer";
        var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existing) return existing;

        var tex = NoiseTexture("Tex_" + name, a, b, name.GetHashCode() & 0xffff);
        var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile) };
        AssetDatabase.CreateAsset(layer, layerPath);
        return layer;
    }

    static Texture2D NoiseTexture(string name, Color a, Color b, int seed)
    {
        string path = k_ArtFolder + "/" + name + ".png";
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        var rng = new Random(seed);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float n = 0f, amp = 0.55f, freq = 4f;
            for (int o = 0; o < 4; o++)
            {
                n += TileNoise(x, y, N, freq, seed + o * 31) * amp;
                amp *= 0.5f;
                freq *= 2f;
            }

            float grain = (float)rng.NextDouble() * 0.12f - 0.06f;
            Color c = Color.Lerp(a, b, Mathf.Clamp01(n)) * (1f + grain);
            c.a = 1f;
            px[y * N + x] = c;
        }

        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // seamless perlin (blend of 4 shifted samples)
    static float TileNoise(int x, int y, int n, float freq, int seed)
    {
        float s = freq / n;
        float u = (float)x / n, v = (float)y / n;
        float f(float px, float py) => Mathf.PerlinNoise(px * s + seed, py * s + seed * 0.37f);
        return f(x, y) * (1 - u) * (1 - v) + f(x - n, y) * u * (1 - v) + f(x, y - n) * (1 - u) * v + f(x - n, y - n) * u * v;
    }

    static void Boundary(Level L, float hx, float hz, float cx, float cz)
    {
        InvisibleWall(L, V(cx - hx, 0, cz - hz), V(cx + hx, 0, cz - hz));
        InvisibleWall(L, V(cx - hx, 0, cz + hz), V(cx + hx, 0, cz + hz));
        InvisibleWall(L, V(cx - hx, 0, cz - hz), V(cx - hx, 0, cz + hz));
        InvisibleWall(L, V(cx + hx, 0, cz - hz), V(cx + hx, 0, cz + hz));
    }

    static void BoundaryRing(Level L, float r, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2 / segments, a1 = (i + 1) * Mathf.PI * 2 / segments;
            var p0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * r;
            var p1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * r;
            // leave the corridor (south) open
            if (Mathf.Abs(p0.x) < 7.5f && p0.z < 0 && Mathf.Abs(p1.x) < 7.5f && p1.z < 0) continue;
            if ((p0.x < 0) != (p1.x < 0) && p0.z < 0) continue;
            InvisibleWall(L, p0, p1);
        }
    }

    static void InvisibleWall(Level L, Vector3 a, Vector3 b)
    {
        var go = Wall(L.Geo, a + Vector3.down * 2f, b + Vector3.down * 2f, 40f, 1f, null);
        go.name = "InvisibleWall";
        Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(go.GetComponent<MeshFilter>());
    }

    static void ScatterOutside(Level L, string model, int count, float hx, float hz, float minH, float maxH, float circleR = -1f)
    {
        int placed = 0, tries = 0;
        while (placed < count && tries++ < count * 20)
        {
            float x = (float)(L.Rng.NextDouble() * 2 - 1) * 110f;
            float z = (float)(L.Rng.NextDouble() * 2 - 1) * 110f;
            float d = circleR > 0 ? Mathf.Sqrt(x * x + z * z) - circleR : RectDist(x, z, 0, 0, hx, hz);
            if (d < 4f || d > 60f) continue;
            if (circleR > 0 && Mathf.Abs(x) < 9f && z < 0) continue; // keep the corridor clear
            var p = new Vector3(x, 0, z);
            p.y = GroundY(L, p) - 0.3f;
            Model(L, model, p, minH + (float)L.Rng.NextDouble() * (maxH - minH), L.Rng.Next(360), false);
            placed++;
        }
    }

    // ================================================================== GEOMETRY HELPERS
    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float yaw = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = size;
        if (mat) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    static GameObject Wall(Transform parent, Vector3 a, Vector3 b, float height, float thickness, Material mat)
    {
        Vector3 d = b - a;
        d.y = 0;
        float len = d.magnitude + thickness;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        var c = (a + b) * 0.5f + Vector3.up * height * 0.5f;
        return Box(parent, "Wall", c, new Vector3(thickness, height, len), mat, yaw);
    }

    static GameObject Crate(Transform parent, Vector3 basePos, float size)
    {
        return Box(parent, "Crate", basePos + Vector3.up * size * 0.5f, Vector3.one * size, s_Copper, (basePos.x * 37 + basePos.z * 13) % 40f);
    }

    static GameObject Ramp(Transform parent, Vector3 bottom, Vector3 top, float width, Material mat)
    {
        Vector3 d = top - bottom;
        const float thick = 0.4f;
        var go = Box(parent, "Ramp", (bottom + top) * 0.5f, new Vector3(width, thick, d.magnitude + 0.8f), mat);
        go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        go.transform.position -= go.transform.up * (thick * 0.5f);
        return go;
    }

    static GameObject Bridge(Transform parent, Vector3 a, Vector3 b, float width)
    {
        var go = Ramp(parent, a, b, width, s_Metal);
        go.name = "Bridge";
        // rails
        Vector3 d = (b - a);
        Vector3 side = Vector3.Cross(Vector3.up, d.normalized) * (width * 0.5f);
        var r1 = Wall(parent, a + side, b + side, 0.9f, 0.15f, s_Black);
        var r2 = Wall(parent, a - side, b - side, 0.9f, 0.15f, s_Black);
        r1.name = r2.name = "Rail";
        return go;
    }

    static void PointLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
    {
        var go = new GameObject("PointLight");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
    }

    static GameObject Model(Level L, string path, Vector3 basePos, float height, float yaw, bool collider)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, L.Props);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = Vector3.one;
        var b = RendererBounds(go);
        if (b.size.y < 0.0001f) return go;
        go.transform.localScale = Vector3.one * (height / b.size.y);
        b = RendererBounds(go);
        go.transform.position += basePos - new Vector3(b.center.x, b.min.y, b.center.z);

        if (collider)
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh && !mf.GetComponent<Collider>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static Bounds RendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ================================================================== GAMEPLAY HELPERS
    static GameObject Spawn(string path, Transform parent, Vector3 pos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[Campaign] Missing prefab " + path);
            return null;
        }

        var go = parent
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
            : (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        Rec(go.transform);
        return go;
    }

    static EnemyController Hover(Level L, Vector3 pos, float dropRate)
    {
        var go = Spawn(k_HoverBot, L.Enemies, pos, L.Rng.Next(360));
        var ec = go.GetComponent<EnemyController>();
        ec.DropRate = dropRate;
        Rec(ec);
        return ec;
    }

    static void Turret(Level L, Vector3 pos, float yaw) => Spawn(k_Turret, L.Enemies, pos, yaw);

    static void Path(Level L, string name, Vector3[] nodes, params EnemyController[] enemies)
    {
        var go = new GameObject(name);
        go.transform.SetParent(L.Paths, false);
        var path = go.AddComponent<PatrolPath>();
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = new GameObject("Patrol_Point (" + i + ")");
            n.transform.SetParent(go.transform, false);
            n.transform.position = nodes[i];
            path.PathNodes.Add(n.transform);
        }

        path.EnemiesToAssign.AddRange(enemies);
    }

    static void Pick(Level L, string path, Vector3 surface) => Spawn(path, L.Pickups, surface + Vector3.up * 1f, 0f);

    static Objective KillObjective(Level L, string title, string description)
    {
        var go = Spawn(k_ObjKill, L.Objectives, Vector3.zero, 0f);
        var obj = go.GetComponent<ObjectiveKillEnemies>();
        obj.Title = title;
        obj.Description = description;
        obj.MustKillAllEnemies = true;
        obj.NotificationEnemiesRemainingThreshold = 3;
        Rec(obj);
        return obj;
    }

    static void ExtractObjective(Level L, Vector3 pos, Objective required)
    {
        var go = Spawn(k_ObjReach, L.Objectives, pos, 0f);
        var obj = go.GetComponent<ObjectiveReachPoint>();
        obj.Title = "Reach the extraction point";
        obj.Description = "Clear the area first, then get to the extraction pad";
        Rec(obj);
        var gate = go.AddComponent<ExtractionGate>();
        gate.RequiredObjective = required;
        gate.UnlockMessage = "Area clear! Extraction point unlocked";
    }

    static void Rec(Object o)
    {
        if (o == null) return;
        EditorUtility.SetDirty(o);
        if (PrefabUtility.IsPartOfPrefabInstance(o))
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    // ================================================================== MATERIALS
    static void LoadMaterials()
    {
        s_Floor = AssetDatabase.LoadAssetAtPath<Material>(LM + "Floor_Grey.mat");
        s_FloorDark = AssetDatabase.LoadAssetAtPath<Material>(LM + "Floor_DarkGrey.mat");
        s_Wall = AssetDatabase.LoadAssetAtPath<Material>(LM + "Wall_Grey.mat");
        s_WallDark = AssetDatabase.LoadAssetAtPath<Material>(LM + "Wall_DarkGrey.mat");
        s_Ramp = AssetDatabase.LoadAssetAtPath<Material>(LM + "Ramp_Blue.mat");
        s_Metal = AssetDatabase.LoadAssetAtPath<Material>(CM + "Metal Silver.mat");
        s_Copper = AssetDatabase.LoadAssetAtPath<Material>(CM + "Metal Copper.mat");
        s_Black = AssetDatabase.LoadAssetAtPath<Material>(CM + "Basic Black.mat");
        s_NeonBlue = AssetDatabase.LoadAssetAtPath<Material>(CM + "Neon Blue.mat");
        s_NeonGlow = AssetDatabase.LoadAssetAtPath<Material>(CM + "Neon Glow.mat");

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        string lavaPath = k_ArtFolder + "/Lava.mat";
        s_Lava = AssetDatabase.LoadAssetAtPath<Material>(lavaPath);
        if (s_Lava == null && lit != null)
        {
            s_Lava = new Material(lit);
            s_Lava.SetColor("_BaseColor", new Color(0.9f, 0.28f, 0.05f));
            s_Lava.SetFloat("_Smoothness", 0.8f);
            s_Lava.EnableKeyword("_EMISSION");
            s_Lava.SetColor("_EmissionColor", new Color(1f, 0.32f, 0.04f) * 3f);
            s_Lava.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(s_Lava, lavaPath);
        }

        string terrainMatPath = k_ArtFolder + "/TerrainLit.mat";
        s_TerrainMat = AssetDatabase.LoadAssetAtPath<Material>(terrainMatPath);
        var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (s_TerrainMat == null && terrainShader != null)
        {
            s_TerrainMat = new Material(terrainShader);
            AssetDatabase.CreateAsset(s_TerrainMat, terrainMatPath);
        }
    }

    // ================================================================== RAIL RIFLE
    static WeaponController CreateRailRifle()
    {
        CopyIfMissing(k_ProjBlaster, k_ProjRail);
        var proj = PrefabUtility.LoadPrefabContents(k_ProjRail);
        try
        {
            var ps = proj.GetComponent<ProjectileStandard>();
            ps.Damage = 55f;
            ps.Speed = 450f;
            ps.Radius = 0.03f;
            PrefabUtility.SaveAsPrefabAsset(proj, k_ProjRail);
        }
        finally { PrefabUtility.UnloadPrefabContents(proj); }

        CopyIfMissing(k_WBlaster, k_WRail);
        var root = PrefabUtility.LoadPrefabContents(k_WRail);
        try
        {
            root.name = "Weapon_RailRifle";
            var wc = root.GetComponent<WeaponController>();
            wc.WeaponName = "Rail Rifle";
            wc.WeaponIcon = CreateRailIcon();
            wc.ShootType = WeaponShootType.Manual;
            wc.ProjectilePrefab = AssetDatabase.LoadAssetAtPath<ProjectileBase>(k_ProjRail);
            wc.DelayBetweenShots = 0.75f;
            wc.BulletsPerShot = 1;
            wc.BulletSpreadAngle = 0f;
            wc.RecoilForce = 3f;
            wc.AimZoomRatio = 0.4f;
            wc.MaxAmmo = 5;
            wc.AmmoReloadRate = 1.2f;
            wc.AmmoReloadDelay = 1.5f;
            wc.AutomaticReload = true;

            // swap the visual model for the ModAssets rifle mesh
            var rifle = AssetDatabase.LoadAssetAtPath<GameObject>(k_RifleMesh);
            Transform weaponRoot = wc.WeaponRoot ? wc.WeaponRoot.transform : root.transform;
            if (rifle && !weaponRoot.Find("RailRifleModel"))
            {
                var oldRenderers = weaponRoot.GetComponentsInChildren<MeshRenderer>(true).ToArray();
                Bounds ob = RendererBounds(weaponRoot.gameObject);
                int layer = oldRenderers.Length > 0 ? oldRenderers[0].gameObject.layer : weaponRoot.gameObject.layer;
                foreach (var r in oldRenderers) r.enabled = false;

                var model = FitModel(rifle, weaponRoot, ob, 1.3f);
                model.name = "RailRifleModel";
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var nb = RendererBounds(model);
                if (wc.WeaponMuzzle)
                    wc.WeaponMuzzle.position = new Vector3(nb.center.x, wc.WeaponMuzzle.position.y, nb.max.z);
            }

            PrefabUtility.SaveAsPrefabAsset(root, k_WRail);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        return AssetDatabase.LoadAssetAtPath<WeaponController>(k_WRail);
    }

    static void CreateRailRiflePickup(WeaponController rail)
    {
        CopyIfMissing(k_PickBlaster, k_PickRail);
        var root = PrefabUtility.LoadPrefabContents(k_PickRail);
        try
        {
            root.name = "Pickup_RailRifle";
            var wp = root.GetComponent<WeaponPickup>();
            wp.WeaponPrefab = rail;

            var rifle = AssetDatabase.LoadAssetAtPath<GameObject>(k_RifleMesh);
            var mesh = root.transform.Find("Mesh") ?? root.transform;
            if (rifle && !mesh.Find("RailRifleModel"))
            {
                var old = mesh.GetComponentsInChildren<MeshRenderer>(true).ToArray();
                Bounds ob = RendererBounds(mesh.gameObject);
                foreach (var r in old) r.enabled = false;
                var model = FitModel(rifle, mesh, ob, 1.3f);
                model.name = "RailRifleModel";
            }

            PrefabUtility.SaveAsPrefabAsset(root, k_PickRail);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // places a model under parent, aligned along +Z and scaled to lengthRatio x the length of target bounds
    static GameObject FitModel(GameObject modelAsset, Transform parent, Bounds target, float lengthRatio)
    {
        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        SceneManager.MoveGameObjectToScene(model, parent.gameObject.scene);
        model.transform.SetParent(parent, false);
        model.transform.localPosition = Vector3.zero;

        var nb = RendererBounds(model);
        if (nb.size.x > nb.size.z * 1.2f)
            model.transform.localRotation = Quaternion.Euler(0, 90, 0) * model.transform.localRotation;
        nb = RendererBounds(model);

        float targetLen = Mathf.Max(target.size.z, 0.05f) * lengthRatio;
        model.transform.localScale *= targetLen / Mathf.Max(nb.size.z, 0.0001f);
        nb = RendererBounds(model);
        model.transform.position += target.center - nb.center;
        return model;
    }

    static Sprite CreateRailIcon()
    {
        string path = k_ArtFolder + "/RailRifleIcon.png";
        const int w = 256, h = 96;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        Rect[] parts =
        {
            new Rect(20, 40, 150, 22),  // body
            new Rect(170, 46, 80, 10),  // barrel
            new Rect(70, 62, 60, 10),   // scope
            new Rect(85, 72, 8, 6), new Rect(110, 72, 8, 6),
            new Rect(40, 14, 22, 28),   // grip
            new Rect(100, 22, 18, 20),  // magazine
            new Rect(6, 36, 20, 30),    // stock
        };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            bool inside = parts.Any(r => r.Contains(p));
            px[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        }

        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ================================================================== BOSS
    const string k_MechFolder = "Assets/ModAssets/Models/BossMech";
    const string k_MechFbx = k_MechFolder + "/Mech_George.fbx";
    const string k_MechTex = k_MechFolder + "/Mech_George_Texture.png";
    const string k_MechMat = k_MechFolder + "/Mech_George.mat";
    const string k_MechController = k_MechFolder + "/Mech_Boss.controller";
    const float k_MechHeight = 6.5f;

    static readonly string[] k_LoopClips = { "Idle", "Walk", "Run", "Walk_Tall", "Run_Tall", "Walk_Holding", "Run_Holding", "Dance" };

    // Generic rig, clean clip names ("RobotArmature|Idle" -> "Idle"), looping locomotion
    static void ConfigureMechImporter()
    {
        AssetDatabase.ImportAsset(k_MechFbx);
        var imp = AssetImporter.GetAtPath(k_MechFbx) as ModelImporter;
        if (imp == null) return;

        imp.animationType = ModelImporterAnimationType.Generic;
        imp.importAnimation = true;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        var seen = new HashSet<string>();
        var clips = new List<ModelImporterClipAnimation>();
        foreach (var c in imp.defaultClipAnimations)
        {
            string n = c.name.Split('|').Last();
            if (!seen.Add(n)) continue;
            c.name = n;
            c.loopTime = k_LoopClips.Contains(n);
            clips.Add(c);
        }

        imp.clipAnimations = clips.ToArray();
        imp.SaveAndReimport();
    }

    static UnityEditor.Animations.AnimatorController CreateMechAnimator()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(k_MechFbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n) ?? clips.FirstOrDefault(c => c.name.EndsWith("|" + n));

        AssetDatabase.DeleteAsset(k_MechController);
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(k_MechController);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        foreach (var t in new[] { "Shoot", "Slash", "Hit", "Taunt" })
            ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (Clip("Idle")) tree.AddChild(Clip("Idle"), 0f);
        if (Clip("Walk")) tree.AddChild(Clip("Walk"), 2f);
        if (Clip("Run")) tree.AddChild(Clip("Run"), 5f);
        sm.defaultState = loco;

        void Action(string param, string clipName, float speed)
        {
            var clip = Clip(clipName);
            if (!clip) return;
            var st = sm.AddState(param);
            st.motion = clip;
            st.speed = speed;
            var tin = sm.AddAnyStateTransition(st);
            tin.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, param);
            tin.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Dead");
            tin.duration = 0.12f;
            tin.canTransitionToSelf = false;
            var tout = st.AddTransition(loco);
            tout.hasExitTime = true;
            tout.exitTime = 0.85f;
            tout.duration = 0.2f;
        }

        Action("Shoot", "Shoot", 1.2f);
        Action("Slash", "SwordSlash", 1f);
        Action("Hit", "HitRecieve_1", 1f);
        Action("Taunt", "Jump", 1f);

        var death = Clip("Death");
        if (death)
        {
            var st = sm.AddState("Death");
            st.motion = death;
            var t = sm.AddAnyStateTransition(st);
            t.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Dead");
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
        }

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // world-space bounds of the actual skinned vertices (+ static meshes)
    static Bounds PosedBounds(GameObject go)
    {
        bool has = false;
        var b = new Bounds(go.transform.position, Vector3.zero);
        void Add(Vector3 p)
        {
            if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
            else b.Encapsulate(p);
        }

        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.sharedMesh) continue;
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var t = smr.transform;
            foreach (var v in baked.vertices)
                Add(t.position + t.rotation * v);
            Object.DestroyImmediate(baked);
        }

        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh) continue;
            foreach (var v in mf.sharedMesh.vertices)
                Add(mf.transform.TransformPoint(v));
        }

        return has ? b : RendererBounds(go);
    }

    static Material CreateMechMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(k_MechMat);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (mat == null && lit != null)
        {
            mat = new Material(lit);
            AssetDatabase.CreateAsset(mat, k_MechMat);
        }

        if (mat != null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(k_MechTex);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0.3f);
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    static void CreateBossPrefab()
    {
        CopyIfMissing(k_ProjHover, k_ProjBoss);
        var proj = PrefabUtility.LoadPrefabContents(k_ProjBoss);
        try
        {
            var ps = proj.GetComponent<ProjectileStandard>();
            ps.Damage = 6f;
            ps.Speed = 40f;          // slow, visible bolts the player can dodge
            ps.MaxLifeTime = 4f;
            proj.transform.localScale = Vector3.one * 2.5f;
            PrefabUtility.SaveAsPrefabAsset(proj, k_ProjBoss);
        }
        finally { PrefabUtility.UnloadPrefabContents(proj); }

        ConfigureMechImporter();
        var animCtrl = CreateMechAnimator();
        var mechMat = CreateMechMaterial();
        var mechAsset = AssetDatabase.LoadAssetAtPath<GameObject>(k_MechFbx);

        CopyIfMissing(k_HoverBot, k_Boss);
        var root = PrefabUtility.LoadPrefabContents(k_Boss);
        try
        {
            root.name = "Enemy_Boss_Overseer";
            root.transform.localScale = Vector3.one;
            float h = k_MechHeight;

            // ---- replace the hoverbot visuals with the animated mech
            var oldMech = root.transform.Find("MechModel");
            if (oldMech) Object.DestroyImmediate(oldMech.gameObject);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    r.enabled = false;
            foreach (var b in root.GetComponentsInChildren<Behaviour>(true))
                if (b.GetType().Name.Contains("Projector"))
                    b.enabled = false;
            foreach (var c in root.GetComponentsInChildren<Canvas>(true))
                c.gameObject.SetActive(false); // the boss has its own screen health bar

            Animator mechAnimator = null;
            if (mechAsset)
            {
                var mech = (GameObject)PrefabUtility.InstantiatePrefab(mechAsset);
                PrefabUtility.UnpackPrefabInstance(mech, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                SceneManager.MoveGameObjectToScene(mech, root.scene);
                mech.name = "MechModel";
                mech.transform.SetParent(root.transform, false);
                mech.transform.localPosition = Vector3.zero;
                mech.transform.localRotation = Quaternion.identity;

                // size is fitted at runtime by BossController (skinned bounds are only valid once animated)
                mech.transform.localScale = Vector3.one;

                foreach (var r in mech.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true;
                    if (mechMat) r.sharedMaterials = Enumerable.Repeat(mechMat, r.sharedMaterials.Length).ToArray();
                }

                foreach (var smr in mech.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.updateWhenOffscreen = true;

                mechAnimator = mech.GetComponent<Animator>();
                if (!mechAnimator) mechAnimator = mech.AddComponent<Animator>();
                mechAnimator.runtimeAnimatorController = animCtrl;
                mechAnimator.avatar = AssetDatabase.LoadAllAssetsAtPath(k_MechFbx).OfType<Avatar>().FirstOrDefault();
                mechAnimator.applyRootMotion = false;
                mechAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else
                Debug.LogError("[Campaign] Boss mech model not found at " + k_MechFbx);

            // ---- hitbox matching the mech
            var oldHit = root.transform.Find("MechHitBox");
            if (oldHit) Object.DestroyImmediate(oldHit.gameObject);
            var hitGo = new GameObject("MechHitBox");
            hitGo.transform.SetParent(root.transform, false);
            var cap = hitGo.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, h * 0.5f, 0);
            cap.height = h;
            cap.radius = 1.4f;
            hitGo.AddComponent<Damageable>();
            hitGo.layer = root.layer;

            // ---- agent: walks on the ground
            var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            agent.baseOffset = 0f;
            agent.radius = 1.2f;
            agent.height = h;

            // ---- stats (tuned to be beatable, not punishing)
            root.GetComponent<Health>().MaxHealth = 1500f;

            var ec = root.GetComponent<EnemyController>();
            ec.DropRate = 0f;
            ec.LootPrefab = null;
            ec.SelfDestructYHeight = -50f;
            ec.DeathDuration = 3f; // let the death animation play
            if (ec.DeathVfxSpawnPoint) ec.DeathVfxSpawnPoint.position = root.transform.TransformPoint(0, h * 0.5f, 0);

            var dm = root.GetComponentInChildren<DetectionModule>(true);
            if (dm)
            {
                dm.DetectionRange = 80f;
                dm.AttackRange = 28f;
                dm.KnownTargetTimeout = 12f;
                if (dm.DetectionSourcePoint)
                    dm.DetectionSourcePoint.position = root.transform.TransformPoint(0, h * 0.75f, 0.8f);
            }

            var nav = root.GetComponentInChildren<NavigationModule>(true);
            if (nav)
            {
                nav.MoveSpeed = 3.2f;
                nav.AngularSpeed = 140f;
                nav.Acceleration = 10f;
            }

            var mobile = root.GetComponent<EnemyMobile>();
            if (mobile) mobile.AttackStopDistanceRatio = 0.5f;

            var bolt = AssetDatabase.LoadAssetAtPath<ProjectileBase>(k_ProjBoss);
            foreach (var w in root.GetComponentsInChildren<WeaponController>(true))
            {
                w.DelayBetweenShots = 1.4f;
                w.BulletsPerShot = 2;
                w.BulletSpreadAngle = 5f;
                if (bolt) w.ProjectilePrefab = bolt;
                w.transform.position = root.transform.TransformPoint(0, h * 0.62f, 1.4f);
            }

            var glowT = root.transform.Find("BossGlow");
            var glow = glowT ? glowT.gameObject : new GameObject("BossGlow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0, h * 0.75f, 1.5f);
            var l = glow.GetComponent<Light>();
            if (!l) l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.25f, 0.2f);
            l.range = 12f;
            l.intensity = 5f;
            l.shadows = LightShadows.None;

            var boss = root.GetComponent<BossController>();
            if (!boss) boss = root.AddComponent<BossController>();
            boss.BossName = "THE OVERSEER";
            boss.ModelAnimator = mechAnimator;
            boss.ModelRoot = mechAnimator ? mechAnimator.transform : null;
            boss.ModelHeight = h;
            boss.MinionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_HoverBot);
            boss.MinionDropRate = 0.8f;
            boss.MinionSpawnRadius = 9f;
            boss.ShieldMaxDuration = 20f;
            boss.SlamRange = 6f;
            boss.SlamDamage = 12f;
            boss.SlamCooldown = 4f;
            boss.SpawnVfx = AssetDatabase.LoadAssetAtPath<GameObject>(k_VfxAlert);
            boss.Phases = new List<BossController.Phase>
            {
                new BossController.Phase
                {
                    HealthThreshold = 0.6f, MinionsToSpawn = 2, FireDelayMultiplier = 0.85f, MoveSpeedMultiplier = 1f,
                    Announcement = "The Overseer raises its shield and calls for drones!"
                },
                new BossController.Phase
                {
                    HealthThreshold = 0.3f, MinionsToSpawn = 3, FireDelayMultiplier = 0.7f, MoveSpeedMultiplier = 1.15f,
                    Announcement = "The Overseer is ENRAGED!"
                },
            };

            PrefabUtility.SaveAsPrefabAsset(root, k_Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ================================================================== PROJECT SETTINGS / MENUS
    static void UpdateBuildSettings()
    {
        string[] order =
        {
            "Assets/FPS/Scenes/IntroMenu.unity",
            ScenePath(Level1), ScenePath(Level2), ScenePath(Level3), ScenePath(LevelBoss),
            "Assets/FPS/Scenes/WinScene.unity",
            "Assets/FPS/Scenes/LoseScene.unity",
            "Assets/FPS/Scenes/MainScene.unity",
        };
        var list = order.Where(p => File.Exists(p)).Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        foreach (var s in EditorBuildSettings.scenes)
            if (!list.Any(l => l.path == s.path))
                list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void PatchMenuScene(string path, bool retry)
    {
        if (!File.Exists(path)) return;
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        bool changed = false;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var b in root.GetComponentsInChildren<LoadSceneButton>(true))
        {
            if (b.SceneName != "MainScene" && b.SceneName != Level1) continue;
            b.SceneName = Level1;
            if (retry) b.LoadLastPlayedLevel = true;
            Rec(b);
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    // ================================================================== UTIL
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void CopyIfMissing(string from, string to)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(to) == null)
            AssetDatabase.CopyAsset(from, to);
    }
}
