using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click setup for the gameplay mods:
///  - builds a Knife weapon prefab (procedural model) and gives it to the player as secondary weapon (slot 2)
///  - adds the Dash ability to the player
///  - adds 2 extra HoverBots to the first level (MainScene)
///  - adds the on-screen rules / victory / defeat messages to the first level
/// Menu: Tools > FPS Mods > Setup Knife, Dash and Extra Bots
/// Safe to run more than once.
/// </summary>
public static class FPSModSetup
{
    const string k_ArtFolder = "Assets/FPS/Art/Knife";
    const string k_BladeMeshPath = k_ArtFolder + "/KnifeBlade.asset";
    const string k_IconPath = k_ArtFolder + "/KnifeIcon.png";
    const string k_KnifePrefabPath = "Assets/FPS/Prefabs/Weapons/Weapon_Knife.prefab";
    const string k_BlasterPrefabPath = "Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab";
    const string k_PlayerPrefabPath = "Assets/FPS/Prefabs/Player.prefab";
    const string k_HoverBotPrefabPath = "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab";
    const string k_MainScenePath = "Assets/FPS/Scenes/MainScene.unity";
    const string k_SilverMatPath = "Assets/ModAssets/Materials/Custom Materials/Metal Silver.mat";
    const string k_BlackMatPath = "Assets/ModAssets/Materials/Custom Materials/Basic Black.mat";
    const string k_HitSfxPath = "Assets/FPS/Audio/SFX/Player/Damage_Tick.mp3";
    const string k_DashSfxPath = "Assets/FPS/Audio/SFX/Player/Jump.wav";
    const string k_HitVfxPath = "Assets/FPS/Prefabs/VFX/VFX_HitSparks.prefab";
    const string k_ExtraBotPrefix = "Enemy_HoverBot_Extra";

    [MenuItem("Tools/FPS Mods/Setup Knife, Dash and Extra Bots")]
    public static void RunAll()
    {
        WeaponController knife = CreateKnifePrefab();
        SetupPlayerPrefab(knife);
        bool sceneOk = SetupMainScene();

        AssetDatabase.SaveAssets();
        string msg = "Knife prefab created and added as secondary weapon (key 2).\nDash added to player (key F).";
        msg += sceneOk
            ? "\n2 extra HoverBots + rules messages added to MainScene (scene saved)."
            : "\nMainScene was NOT modified (cancelled or not found).";
        EditorUtility.DisplayDialog("FPS Mods", msg, "OK");
    }

    // ------------------------------------------------------------------ KNIFE

    static WeaponController CreateKnifePrefab()
    {
        if (!AssetDatabase.IsValidFolder(k_ArtFolder))
            AssetDatabase.CreateFolder("Assets/FPS/Art", "Knife");

        Mesh bladeMesh = CreateOrUpdateBladeMesh();
        Sprite icon = CreateIcon();

        var silver = AssetDatabase.LoadAssetAtPath<Material>(k_SilverMatPath);
        var black = AssetDatabase.LoadAssetAtPath<Material>(k_BlackMatPath);
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var blaster = AssetDatabase.LoadAssetAtPath<GameObject>(k_BlasterPrefabPath).GetComponent<WeaponController>();

        // Build the hierarchy in a preview scene so the open scene isn't touched
        Scene preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Weapon_Knife");
        SceneManager.MoveGameObjectToScene(root, preview);

        var weaponRoot = new GameObject("WeaponRoot");
        weaponRoot.transform.SetParent(root.transform, false);

        var model = new GameObject("KnifeModel");
        model.transform.SetParent(weaponRoot.transform, false);
        model.transform.localPosition = new Vector3(-0.02f, 0.02f, 0.05f);
        model.transform.localRotation = Quaternion.Euler(-12f, -14f, -20f);
        model.transform.localScale = Vector3.one * 1.6f;

        AddPart(model.transform, "Blade", bladeMesh, silver, Vector3.zero, Vector3.one);
        AddPart(model.transform, "Guard", cube, silver, new Vector3(0f, 0f, -0.004f), new Vector3(0.022f, 0.06f, 0.012f));
        AddPart(model.transform, "Handle", cube, black, new Vector3(0f, -0.002f, -0.065f), new Vector3(0.02f, 0.028f, 0.11f));
        AddPart(model.transform, "Pommel", cube, silver, new Vector3(0f, -0.002f, -0.125f), new Vector3(0.024f, 0.032f, 0.014f));

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(weaponRoot.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0f, 0.4f);

        // WeaponController (RequireComponent adds the AudioSource)
        var wc = root.AddComponent<WeaponController>();
        var audio = root.GetComponent<AudioSource>();
        EditorUtility.CopySerialized(blaster.GetComponent<AudioSource>(), audio);

        wc.WeaponName = "Knife";
        wc.WeaponIcon = icon;
        wc.CrosshairDataDefault = blaster.CrosshairDataDefault;
        wc.CrosshairDataTargetInSight = blaster.CrosshairDataTargetInSight;
        wc.WeaponRoot = weaponRoot;
        wc.WeaponMuzzle = muzzle.transform;
        wc.ShootType = WeaponShootType.Manual;
        wc.ProjectilePrefab = null;
        wc.DelayBetweenShots = 0.45f;
        wc.BulletSpreadAngle = 0f;
        wc.BulletsPerShot = 1;
        wc.RecoilForce = 0f;
        wc.AimZoomRatio = 1f;
        wc.AimOffset = Vector3.zero;
        wc.AutomaticReload = true;
        wc.MaxAmmo = 1;
        wc.AmmoReloadDelay = 0f;
        wc.AmmoReloadRate = 100f;
        wc.ChangeWeaponSfx = blaster.ChangeWeaponSfx;

        var melee = root.AddComponent<MeleeWeapon>();
        melee.SwingPivot = model.transform;
        melee.HitSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(k_HitSfxPath);
        melee.ImpactVfx = AssetDatabase.LoadAssetAtPath<GameObject>(k_HitVfxPath);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, k_KnifePrefabPath);
        Object.DestroyImmediate(root);
        EditorSceneManager.ClosePreviewScene(preview);

        return saved.GetComponent<WeaponController>();
    }

    static void AddPart(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Blade pointing +Z, spine on top (+Y), sharp edge on the bottom (-Y)
    static Mesh CreateOrUpdateBladeMesh()
    {
        float[] z = { 0.0f, 0.08f, 0.14f, 0.18f, 0.21f, 0.235f };
        float[] spine = { 0.018f, 0.018f, 0.017f, 0.013f, 0.007f, 0.0f };
        float[] edge = { -0.018f, -0.018f, -0.015f, -0.009f, -0.004f, 0.0f };
        float[] half = { 0.0045f, 0.0042f, 0.0036f, 0.003f, 0.002f, 0.0f };

        var verts = new List<Vector3>();
        var tris = new List<int>();

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f)
                n = Vector3.Cross(c - a, d - a);
            Vector3 faceCenter = (a + b + c + d) * 0.25f;
            if (Vector3.Dot(n, faceCenter - center) < 0f)
            {
                (b, d) = (d, b);
            }

            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        for (int s = 0; s < z.Length - 1; s++)
        {
            Vector3 sl0 = new Vector3(-half[s], spine[s], z[s]);
            Vector3 sr0 = new Vector3(half[s], spine[s], z[s]);
            Vector3 e0 = new Vector3(0f, edge[s], z[s]);
            Vector3 sl1 = new Vector3(-half[s + 1], spine[s + 1], z[s + 1]);
            Vector3 sr1 = new Vector3(half[s + 1], spine[s + 1], z[s + 1]);
            Vector3 e1 = new Vector3(0f, edge[s + 1], z[s + 1]);
            Vector3 center = new Vector3(0f, (spine[s] + edge[s]) * 0.5f, (z[s] + z[s + 1]) * 0.5f);

            Quad(sl0, e0, e1, sl1, center); // left face
            Quad(e0, sr0, sr1, e1, center); // right face
            Quad(sr0, sl0, sl1, sr1, center); // spine
        }

        // base cap
        Vector3 bl = new Vector3(-half[0], spine[0], 0f), br = new Vector3(half[0], spine[0], 0f), be = new Vector3(0f, edge[0], 0f);
        Quad(bl, br, be, be, new Vector3(0f, 0f, 0.05f));

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(k_BladeMeshPath);
        bool isNew = mesh == null;
        if (isNew)
            mesh = new Mesh { name = "KnifeBlade" };
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (isNew)
            AssetDatabase.CreateAsset(mesh, k_BladeMeshPath);
        else
            EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // Simple knife silhouette icon for the weapon HUD
    static Sprite CreateIcon()
    {
        const int w = 256, h = 96;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        Vector2[] blade = { new(100, 34), new(250, 40), new(222, 62), new(100, 64) };
        Vector2[] guard = { new(88, 22), new(100, 22), new(100, 76), new(88, 76) };
        Vector2[] handle = { new(14, 36), new(88, 38), new(88, 60), new(14, 62) };

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            bool inside = InPoly(p, blade) || InPoly(p, guard) || InPoly(p, handle);
            px[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        }

        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(k_IconPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(k_IconPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(k_IconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(k_IconPath);
    }

    static bool InPoly(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }

        return inside;
    }

    // ------------------------------------------------------------------ PLAYER

    static void SetupPlayerPrefab(WeaponController knife)
    {
        GameObject player = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
        try
        {
            var wm = player.GetComponent<PlayerWeaponsManager>();
            wm.StartingWeapons.RemoveAll(w => w == null);
            if (!wm.StartingWeapons.Contains(knife))
                wm.StartingWeapons.Insert(Mathf.Min(1, wm.StartingWeapons.Count), knife); // slot 2 = secondary

            var dash = player.GetComponent<PlayerDash>();
            if (dash == null)
                dash = player.AddComponent<PlayerDash>();
            if (dash.DashSfx == null)
                dash.DashSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(k_DashSfxPath);

            PrefabUtility.SaveAsPrefabAsset(player, k_PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    // ------------------------------------------------------------------ SCENE

    static bool SetupMainScene()
    {
        Scene scene = SceneManager.GetSceneByPath(k_MainScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            scene = EditorSceneManager.OpenScene(k_MainScenePath, OpenSceneMode.Single);
        }

        if (!scene.IsValid())
            return false;

        var roots = scene.GetRootGameObjects();

        // --- rules / victory / defeat messages
        var flow = roots.SelectMany(r => r.GetComponentsInChildren<GameFlowManager>(true)).FirstOrDefault();
        if (flow != null && flow.GetComponent<GameRulesDisplay>() == null)
            Undo.AddComponent<GameRulesDisplay>(flow.gameObject);

        // --- extra hoverbots
        var existingNames = new HashSet<string>(roots.Select(r => r.name));
        var enemies = roots.SelectMany(r => r.GetComponentsInChildren<EnemyController>(true)).ToList();
        var mobiles = roots.SelectMany(r => r.GetComponentsInChildren<EnemyMobile>(true)).ToList();
        var paths = roots.SelectMany(r => r.GetComponentsInChildren<PatrolPath>(true)).ToList();
        var player = roots.SelectMany(r => r.GetComponentsInChildren<PlayerCharacterController>(true)).FirstOrDefault();
        var hoverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_HoverBotPrefabPath);

        // patrol path used by the original hoverbot (new bots will share it)
        PatrolPath path = paths.FirstOrDefault(p => mobiles.Any(m => p.EnemiesToAssign.Contains(m.GetComponent<EnemyController>())))
                          ?? paths.FirstOrDefault();

        var anchors = new List<Vector3>();
        if (path != null)
            anchors.AddRange(path.PathNodes.Where(n => n != null).Select(n => n.position));
        anchors.AddRange(mobiles.Select(m => m.transform.position));
        if (anchors.Count == 0)
            anchors.AddRange(enemies.Select(e => e.transform.position));

        var taken = enemies.Select(e => e.transform.position).ToList();
        int toSpawn = 2 - Enumerable.Range(1, 2).Count(i => existingNames.Contains(k_ExtraBotPrefix + i));

        for (int i = 1; i <= 2; i++)
        {
            string botName = k_ExtraBotPrefix + i;
            if (existingNames.Contains(botName))
                continue;

            Vector3 pos = FindSpawnPoint(anchors, taken, player ? player.transform.position : (Vector3?)null, i);
            var bot = (GameObject)PrefabUtility.InstantiatePrefab(hoverPrefab, scene);
            Undo.RegisterCreatedObjectUndo(bot, "Add extra hoverbot");
            bot.name = botName;
            bot.transform.position = pos;
            if (player)
            {
                Vector3 look = Vector3.ProjectOnPlane(player.transform.position - pos, Vector3.up);
                if (look.sqrMagnitude > 0.01f)
                    bot.transform.rotation = Quaternion.LookRotation(look);
            }

            taken.Add(pos);

            if (path != null)
            {
                Undo.RecordObject(path, "Assign hoverbot to patrol path");
                path.EnemiesToAssign.Add(bot.GetComponent<EnemyController>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(path);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[FPSModSetup] MainScene updated ({toSpawn} hoverbot(s) added).");
        return true;
    }

    static Vector3 FindSpawnPoint(List<Vector3> anchors, List<Vector3> taken, Vector3? playerPos, int seed)
    {
        float[] radii = { 4f, 6f, 3f, 8f };
        // try anchors in a different order for each bot so they spread out
        var orderedAnchors = anchors.Skip(seed % Mathf.Max(1, anchors.Count)).Concat(anchors).ToList();

        foreach (float r in radii)
        foreach (var anchor in orderedAnchors)
            for (int a = 0; a < 8; a++)
            {
                float ang = (a * 45f + seed * 20f) * Mathf.Deg2Rad;
                Vector3 c = anchor + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;
                if (Mathf.Abs(hit.position.y - anchor.y) > 2f)
                    continue; // stay on the same floor as the anchor
                if (taken.Any(t => Vector3.Distance(t, hit.position) < 3.5f))
                    continue;
                if (playerPos.HasValue && Vector3.Distance(playerPos.Value, hit.position) < 12f)
                    continue;
                return hit.position;
            }

        Debug.LogWarning("[FPSModSetup] No NavMesh found near the existing hoverbot, using a fallback offset. " +
                         "Check the new bot's position in MainScene.");
        Vector3 basePos = anchors.Count > 0 ? anchors[0] : Vector3.zero;
        return basePos + new Vector3(seed == 1 ? 3f : -3f, 0f, 2f);
    }
}
