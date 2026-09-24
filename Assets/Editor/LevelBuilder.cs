#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using EcosDeLaCripta.Audio;
using EcosDeLaCripta.Combat;
using EcosDeLaCripta.Core;
using EcosDeLaCripta.Enemy;
using EcosDeLaCripta.Environment;
using EcosDeLaCripta.Items;
using EcosDeLaCripta.Player;
using EcosDeLaCripta.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EcosDeLaCripta.EditorTools
{
    /// <summary>
    /// Genera de forma procedural las dos escenas de "Ecos de la Cripta": el Menú Principal
    /// (con pantalla de carga) y el Nivel 1 (dungeon en cuadrícula construido con el pack
    /// gratuito Kenney "Mini Dungeon", IA de steering seek/wander, power-ups y HUD).
    /// Menú: Ecos de la Cripta > Construir Todo / Construir Nivel 1 / Construir Menú.
    /// </summary>
    public static class LevelBuilder
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string Level1ScenePath = "Assets/Scenes/Level1.unity";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string KenneyModelsFolder = "Assets/Art/Kenney/MiniDungeon/Models";
        private const string KenneyTexturePath = "Assets/Art/Kenney/MiniDungeon/Textures/colormap.png";
        private const string AudioFolder = "Assets/Art/Kenney/RPGAudio";
        private const string CustomAudioFolder = "Assets/Audio";

        private const float CellSize = 1f;
        private const int RoomSize = 11;
        private const int DoorWidth = 3;
        private const int CorridorLength = 5;

        private static readonly Color Orange = new Color(0.85f, 0.47f, 0.16f);
        private static readonly Color WineRed = new Color(0.55f, 0.18f, 0.22f);
        private static readonly Color CharcoalLight = new Color(0.155f, 0.13f, 0.115f);
        private static readonly Color PlayerBlue = new Color(0.24f, 0.6f, 0.86f);
        private static readonly Color GreenGem = new Color(0.35f, 0.85f, 0.45f);
        private static readonly Color GoldGem = new Color(1f, 0.85f, 0.3f);
        private static readonly Color CyanGem = new Color(0.3f, 0.8f, 0.95f);
        private static readonly Color FogColor = new Color(0.025f, 0.02f, 0.025f);

        [MenuItem("Ecos de la Cripta/Construir Todo (Menú + Nivel 1)")]
        public static void BuildAll()
        {
            BuildMainMenu();
            BuildLevel1();
        }

        public static void BuildAllAndScreenshot()
        {
            ProjectSetup.SetFullHD();
            BuildAll();
            SceneScreenshot.CaptureMainMenu();
            SceneScreenshot.CaptureLevel1();
        }

        public static void BuildLevel1AndScreenshot()
        {
            ProjectSetup.SetFullHD();
            BuildLevel1();
            SceneScreenshot.CaptureLevel1();
        }

        [MenuItem("Ecos de la Cripta/Construir Solo Nivel 1")]
        public static void BuildLevel1Menu() => BuildLevel1();

        [MenuItem("Ecos de la Cripta/Construir Solo Menú Principal")]
        public static void BuildMainMenuMenu() => BuildMainMenu();

        // ==================== NIVEL 1 ====================

        public static void BuildLevel1()
        {
            EnsureFolders();
            EnsureTag("Wall");
            EnsureCustomAudioImportSettings();
            KenneyTextureSetup.FixColormapImport();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            SetupFog();

            Material kenneyMat = GetKenneyMaterial();
            Material bossMat = GetKenneyBossMaterial();
            Material torchFlameMat = CreateMaterial("Mat_TorchFlame", new Color(0.95f, 0.4f, 0.08f), 0.2f, new Color(0.95f, 0.4f, 0.08f) * 2.2f);
            Material torchPoleMat = CreateMaterial("Mat_TorchPole", new Color(0.05f, 0.04f, 0.03f), 0.3f);
            Material projectileMat = CreateMaterial("Mat_Projectile", Color.white, 0.7f, PlayerBlue * 3f);
            Material enemyBoltMat = CreateMaterial("Mat_EnemyBolt", new Color(0.25f, 0.01f, 0.02f), 0.55f, new Color(2.2f, 0.04f, 0.015f));
            Material runeMat = CreateMaterial("Mat_Rune", new Color(0.22f, 0.015f, 0.025f), 0.25f, new Color(0.75f, 0.03f, 0.015f));
            Material cyanRuneMat = CreateMaterial("Mat_RuneCyan", new Color(0.015f, 0.08f, 0.11f), 0.3f, new Color(0.02f, 0.48f, 0.72f));
            Material greenRuneMat = CreateMaterial("Mat_RuneGreen", new Color(0.01f, 0.12f, 0.055f), 0.25f, new Color(0.02f, 0.65f, 0.22f));
            Material violetRuneMat = CreateMaterial("Mat_RuneViolet", new Color(0.08f, 0.015f, 0.13f), 0.3f, new Color(0.48f, 0.05f, 0.85f));

            AudioClip shootClip = LoadCustomAudio("Disparo");
            AudioClip hurtClip = LoadCustomAudio("Dano");
            AudioClip ambientClip = LoadCustomAudio("Ambiente");
            AudioClip bossMusicClip = LoadCustomAudio("BossFinal");
            AudioClip hitClip = LoadAudio("knifeSlice2");
            AudioClip attackClip = LoadAudio("chop");
            AudioClip pickupClip = LoadAudio("handleCoins");
            AudioClip[] footstepClips =
            {
                LoadAudio("footstep00"), LoadAudio("footstep01"), LoadAudio("footstep02"),
                LoadAudio("footstep03"), LoadAudio("footstep04"), LoadAudio("footstep05"),
            };

            GameObject root = new GameObject("Level1");
            var roomsParent = NewChild(root.transform, "Rooms");
            var propsParent = NewChild(root.transform, "Props");

            // Cinco cámaras con encuentros distintos y descansos breves entre combates.
            Vector2Int room0 = new Vector2Int(0, 0);
            Vector2Int corridor0 = new Vector2Int(RoomSize, (RoomSize - DoorWidth) / 2);
            Vector2Int room1 = new Vector2Int(RoomSize + CorridorLength, 0);
            Vector2Int corridor1 = new Vector2Int(room1.x + RoomSize, (RoomSize - DoorWidth) / 2);
            Vector2Int room2 = new Vector2Int(corridor1.x + CorridorLength, 0);
            Vector2Int corridor2 = new Vector2Int(room2.x + RoomSize, (RoomSize - DoorWidth) / 2);
            Vector2Int room3 = new Vector2Int(corridor2.x + CorridorLength, 0);
            Vector2Int corridor3 = new Vector2Int(room3.x + RoomSize, (RoomSize - DoorWidth) / 2);
            Vector2Int room4 = new Vector2Int(corridor3.x + CorridorLength, 0);

            BuildRoomGrid(roomsParent, room0, doorEast: true, doorWest: false);
            BuildCorridorGrid(roomsParent, corridor0);
            BuildRoomGrid(roomsParent, room1, doorEast: true, doorWest: true);
            BuildCorridorGrid(roomsParent, corridor1);
            BuildRoomGrid(roomsParent, room2, doorEast: true, doorWest: true);
            BuildCorridorGrid(roomsParent, corridor2);
            BuildRoomGrid(roomsParent, room3, doorEast: true, doorWest: true);
            BuildCorridorGrid(roomsParent, corridor3);
            BuildRoomGrid(roomsParent, room4, doorEast: false, doorWest: true);

            float totalWidth = room4.x + RoomSize;
            GameObject groundGo = new GameObject("GroundCollider");
            groundGo.transform.SetParent(root.transform);
            BoxCollider ground = groundGo.AddComponent<BoxCollider>();
            ground.size = new Vector3(totalWidth + 4f, 0.2f, RoomSize + 4f);
            ground.center = new Vector3(totalWidth / 2f, -0.1f, RoomSize / 2f);

            // Antorchas cerca de las esquinas de cada sala.
            PlaceTorch(propsParent, CellToWorld(room0, 1, 1), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room0, RoomSize - 2, RoomSize - 2), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room1, 1, RoomSize - 2), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room1, RoomSize - 2, 1), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room2, 1, 1), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room2, RoomSize - 2, RoomSize - 2), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room3, 1, RoomSize - 2), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room3, RoomSize - 2, 1), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room4, 1, 1), torchPoleMat, torchFlameMat);
            PlaceTorch(propsParent, CellToWorld(room4, RoomSize - 2, RoomSize - 2), torchPoleMat, torchFlameMat);

            // Altares luminosos que dan identidad visual y orientan el recorrido.
            BuildRune(propsParent, CellToWorld(room0, 5, 5), cyanRuneMat, new Color(0.1f, 0.75f, 1f), 1.25f);
            BuildRune(propsParent, CellToWorld(room1, 5, 5), runeMat, new Color(1f, 0.12f, 0.03f), 1.55f);
            BuildRune(propsParent, CellToWorld(room2, 5, 5), greenRuneMat, new Color(0.08f, 1f, 0.35f), 1.45f);
            BuildRune(propsParent, CellToWorld(room3, 5, 5), violetRuneMat, new Color(0.62f, 0.12f, 1f), 1.7f);
            BuildRune(propsParent, CellToWorld(room4, 5, 6), runeMat, new Color(1f, 0.05f, 0.02f), 2.2f);

            // Decoración: barriles, mesa y silla, cofre del tesoro junto al jefe.
            SpawnKenney("barrel", propsParent, CellToWorld(room0, 2, 2));
            SpawnKenney("barrel", propsParent, CellToWorld(room0, 2, 3));
            SpawnKenney("table", propsParent, CellToWorld(room1, 5, 8));
            SpawnKenney("chair", propsParent, CellToWorld(room1, 5, 7));
            SpawnKenney("banner", propsParent, CellToWorld(room1, 5, 10) + Vector3.up * 0.01f);
            SpawnKenney("chest", propsParent, CellToWorld(room2, 8, 2));
            SpawnKenney("rocks", propsParent, CellToWorld(room2, 2, 8));
            SpawnKenney("stones", propsParent, CellToWorld(room0, 8, 2));
            SpawnKenney("dirt", propsParent, CellToWorld(room0, 3, 8));
            SpawnKenney("stones", propsParent, CellToWorld(room1, 2, 2));
            SpawnKenney("dirt", propsParent, CellToWorld(room1, 8, 8));
            SpawnKenney("shield-round", propsParent, CellToWorld(room2, 8, 8));
            SpawnKenney("wood-structure", propsParent, CellToWorld(room2, 3, 3));
            SpawnKenney("wood-structure", propsParent, CellToWorld(room2, 7, 7));
            SpawnKenney("barrel", propsParent, CellToWorld(room3, 2, 8));
            SpawnKenney("barrel", propsParent, CellToWorld(room3, 8, 2));
            SpawnKenney("banner", propsParent, CellToWorld(room3, 5, 10));
            SpawnKenney("chest", propsParent, CellToWorld(room3, 5, 2));
            SpawnKenney("shield-rectangle", propsParent, CellToWorld(room4, 2, 8));
            SpawnKenney("shield-round", propsParent, CellToWorld(room4, 8, 8));
            SpawnKenney("weapon-sword", propsParent, CellToWorld(room4, 5, 9));

            // Sellos de progresión: cada sala debe limpiarse antes de continuar.
            BuildKillGate(root.transform, new Vector3(room0.x + RoomSize - 1, 0f, 5f), 2, runeMat);
            BuildKillGate(root.transform, new Vector3(room1.x + RoomSize - 1, 0f, 5f), 5, runeMat);
            BuildKillGate(root.transform, new Vector3(room2.x + RoomSize - 1, 0f, 5f), 8, greenRuneMat);
            BuildKillGate(root.transform, new Vector3(room3.x + RoomSize - 1, 0f, 5f), 11, violetRuneMat);

            // Trampas telegrafiadas: obligan a moverse y usar el impulso.
            BuildSpikeTrap(propsParent, CellToWorld(room1, 4, 4));
            BuildSpikeTrap(propsParent, CellToWorld(room1, 6, 6));
            BuildSpikeTrap(propsParent, CellToWorld(room2, 3, 5));
            BuildSpikeTrap(propsParent, CellToWorld(room2, 7, 5));
            BuildSpikeTrap(propsParent, CellToWorld(room3, 4, 7));
            BuildSpikeTrap(propsParent, CellToWorld(room3, 6, 3));
            BuildSpikeTrap(propsParent, CellToWorld(room4, 3, 5));
            BuildSpikeTrap(propsParent, CellToWorld(room4, 7, 5));

            // ---------- Jugador ----------
            GameObject player = BuildPlayer(root.transform, CellToWorld(room0, 5, 2), kenneyMat);

            // ---------- Cámara ----------
            BuildCamera(root.transform, player.transform);

            // ---------- Proyectil ----------
            Projectile projectilePrefab = BuildProjectilePrefab(projectileMat, hitClip);
            EnemyBolt enemyBoltPrefab = BuildEnemyBoltPrefab(enemyBoltMat);
            var muzzle = NewChild(player.transform, "Muzzle");
            muzzle.localPosition = new Vector3(0, 0.6f, 0.35f);
            PlayerController pc = player.GetComponent<PlayerController>();
            pc.Configure(projectilePrefab, muzzle);
            pc.ConfigureAudio(shootClip, footstepClips);

            // ---------- Enemigos ----------
            AnimatorController enemyAnimCtrl = KenneyAnimatorSetup.BuildEnemyController();
            EnemyAI scoutA = BuildEnemy(root.transform, CellToWorld(room0, 3, 7), kenneyMat, enemyAnimCtrl, scale: 0.88f, maxHealth: 2);
            scoutA.ConfigureVariant(3.1f, 7f, 1, 100, 6.5f);
            scoutA.ConfigureAudio(attackClip);
            EnemyAI scoutB = BuildEnemy(root.transform, CellToWorld(room0, 7, 7), kenneyMat, enemyAnimCtrl, scale: 0.88f, maxHealth: 2);
            scoutB.ConfigureVariant(3.1f, 7f, 1, 100, 6.5f);
            scoutB.ConfigureAudio(attackClip);

            EnemyAI roomGuardA = BuildEnemy(root.transform, CellToWorld(room1, 3, 7), kenneyMat, enemyAnimCtrl, maxHealth: 3);
            roomGuardA.ConfigureVariant(2.5f, 5.5f, 1, 125, 7f);
            roomGuardA.ConfigureAudio(attackClip);
            EnemyAI roomGuardB = BuildEnemy(root.transform, CellToWorld(room1, 7, 3), kenneyMat, enemyAnimCtrl, maxHealth: 3);
            roomGuardB.ConfigureVariant(2.5f, 5.5f, 1, 125, 7f);
            roomGuardB.ConfigureAudio(attackClip);
            EnemyAI acolyteA = BuildEnemy(root.transform, CellToWorld(room1, 8, 8), bossMat, enemyAnimCtrl, scale: 0.92f, maxHealth: 3);
            acolyteA.ConfigureVariant(2.15f, 5f, 1, 175, 8f);
            acolyteA.ConfigureRanged(enemyBoltPrefab);
            acolyteA.ConfigureAudio(attackClip);

            EnemyAI cryptScoutA = BuildEnemy(root.transform, CellToWorld(room2, 3, 7), kenneyMat, enemyAnimCtrl, scale: 0.9f, maxHealth: 3);
            cryptScoutA.ConfigureVariant(3f, 6.8f, 1, 125, 7.2f);
            cryptScoutA.ConfigureAudio(attackClip);
            EnemyAI cryptScoutB = BuildEnemy(root.transform, CellToWorld(room2, 7, 3), kenneyMat, enemyAnimCtrl, scale: 0.9f, maxHealth: 3);
            cryptScoutB.ConfigureVariant(3f, 6.8f, 1, 125, 7.2f);
            cryptScoutB.ConfigureAudio(attackClip);
            EnemyAI acolyteB = BuildEnemy(root.transform, CellToWorld(room2, 8, 8), bossMat, enemyAnimCtrl, scale: 0.95f, maxHealth: 3);
            acolyteB.ConfigureVariant(2.1f, 5f, 1, 175, 8f);
            acolyteB.ConfigureRanged(enemyBoltPrefab, 7.5f, 4.6f, 1.5f);
            acolyteB.ConfigureAudio(attackClip);

            EnemyAI brute = BuildEnemy(root.transform, CellToWorld(room3, 5, 7), bossMat, enemyAnimCtrl, scale: 1.35f, maxHealth: 8);
            brute.ConfigureVariant(1.95f, 5.5f, 2, 275, 8f, 1.65f);
            brute.ConfigureAudio(attackClip);
            EnemyAI bruteGuard = BuildEnemy(root.transform, CellToWorld(room3, 3, 4), kenneyMat, enemyAnimCtrl, maxHealth: 4);
            bruteGuard.ConfigureVariant(2.65f, 6f, 1, 150, 7.5f);
            bruteGuard.ConfigureAudio(attackClip);
            EnemyAI acolyteC = BuildEnemy(root.transform, CellToWorld(room3, 8, 5), bossMat, enemyAnimCtrl, scale: 0.95f, maxHealth: 4);
            acolyteC.ConfigureVariant(2.15f, 5f, 1, 200, 8f);
            acolyteC.ConfigureRanged(enemyBoltPrefab, 7.5f, 4.8f, 1.35f);
            acolyteC.ConfigureAudio(attackClip);

            EnemyAI finalGuardA = BuildEnemy(root.transform, CellToWorld(room4, 3, 7), kenneyMat, enemyAnimCtrl, scale: 1.05f, maxHealth: 5);
            finalGuardA.ConfigureVariant(2.8f, 6.5f, 1, 175, 8f);
            finalGuardA.ConfigureArenaBounds(room4.x + 2f, room4.x + 8.7f, 1.25f, 8.75f);
            finalGuardA.ConfigureAudio(attackClip);
            EnemyAI finalGuardB = BuildEnemy(root.transform, CellToWorld(room4, 8, 4), bossMat, enemyAnimCtrl, scale: 1f, maxHealth: 4);
            finalGuardB.ConfigureVariant(2.25f, 5.2f, 1, 200, 8.5f);
            finalGuardB.ConfigureRanged(enemyBoltPrefab, 8f, 5f, 1.25f);
            finalGuardB.ConfigureArenaBounds(room4.x + 2f, room4.x + 8.7f, 1.25f, 8.75f);
            finalGuardB.ConfigureAudio(attackClip);
            EnemyAI boss = BuildEnemy(root.transform, CellToWorld(room4, 6, 7), bossMat, enemyAnimCtrl, scale: 1.7f, maxHealth: 20);
            boss.ConfigureAsBoss(speed: 2.15f, force: 7f, damage: 2, score: 800, detection: 9f, lose: 11f);
            boss.ConfigureBossSpecial(enemyBoltPrefab);
            boss.ConfigureArenaBounds(room4.x + 2f, room4.x + 8.4f, 1.5f, 8.5f);
            boss.ConfigureAudio(attackClip);

            GameObject audioDirectorGo = new GameObject("DungeonAudioDirector");
            audioDirectorGo.transform.SetParent(root.transform, false);
            audioDirectorGo.AddComponent<DungeonAudioDirector>().Configure(player.transform, boss, room4.x + 1.5f, ambientClip, bossMusicClip);

            // ---------- Power-ups ----------
            BuildPowerUp(propsParent, CellToWorld(room0, 8, 8), PowerUpType.Damage, pickupClip);
            BuildPowerUp(propsParent, CellToWorld(room1, 5, 2), PowerUpType.Speed, pickupClip);
            BuildPowerUp(propsParent, CellToWorld(room1, 8, 2), PowerUpType.Health, pickupClip);
            BuildPowerUp(propsParent, CellToWorld(room2, 5, 8), PowerUpType.Health, pickupClip);
            BuildPowerUp(propsParent, CellToWorld(room2, 5, 2), PowerUpType.Damage, pickupClip);
            BuildPowerUp(propsParent, CellToWorld(room3, 2, 2), PowerUpType.Speed, pickupClip);
            BuildPowerUp(propsParent, CellToWorld(room4, 2, 2), PowerUpType.Health, pickupClip);

            BuildPostProcessing(root.transform);

            GameObject gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(root.transform);
            GameManager gm = gmGo.AddComponent<GameManager>();
            gm.SetTotalEnemies(14);

            BuildLevelUI(root.transform, player.GetComponent<Health>(), hurtClip);

            EditorSceneManager.SaveScene(scene, Level1ScenePath);
            SetBuildScenesOrder(MainMenuScenePath, Level1ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[LevelBuilder] Nivel 1 generado y guardado en " + Level1ScenePath);
        }

        private static Vector3 CellToWorld(Vector2Int roomOrigin, int localX, int localZ)
        {
            return new Vector3((roomOrigin.x + localX) * CellSize, 0f, (roomOrigin.y + localZ) * CellSize);
        }

        private static void BuildRoomGrid(Transform parent, Vector2Int origin, bool doorEast, bool doorWest)
        {
            Transform room = NewChild(parent, $"Room_{origin.x}");
            int doorStart = (RoomSize - DoorWidth) / 2;
            int doorEnd = doorStart + DoorWidth - 1;

            for (int cx = 0; cx < RoomSize; cx++)
            {
                for (int cz = 0; cz < RoomSize; cz++)
                {
                    Vector3 worldPos = CellToWorld(origin, cx, cz);
                    PlaceFloorTile(room, worldPos);

                    bool isPerimeter = cx == 0 || cx == RoomSize - 1 || cz == 0 || cz == RoomSize - 1;
                    if (!isPerimeter) continue;

                    bool isDoorRow = cz >= doorStart && cz <= doorEnd;
                    if (doorEast && cx == RoomSize - 1 && isDoorRow) continue;
                    if (doorWest && cx == 0 && isDoorRow) continue;

                    bool isCorner = (cx == 0 || cx == RoomSize - 1) && (cz == 0 || cz == RoomSize - 1);
                    if (isCorner) PlaceColumn(room, worldPos);
                    else PlaceWall(room, worldPos);
                }
            }
        }

        private static void BuildCorridorGrid(Transform parent, Vector2Int origin)
        {
            Transform corridor = NewChild(parent, $"Corridor_{origin.x}");
            for (int cx = 0; cx < CorridorLength; cx++)
            {
                for (int cz = 0; cz < DoorWidth; cz++)
                {
                    Vector3 worldPos = CellToWorld(origin, cx, cz);
                    PlaceFloorTile(corridor, worldPos);

                    bool isSide = cz == 0 || cz == DoorWidth - 1;
                    if (isSide) PlaceWall(corridor, worldPos);
                }
            }
        }

        private static void PlaceFloorTile(Transform parent, Vector3 worldPos) => SpawnKenney("floor", parent, worldPos);

        private static void PlaceWall(Transform parent, Vector3 worldPos)
        {
            GameObject go = SpawnKenney("wall", parent, worldPos);
            go.transform.localScale = new Vector3(1f, 3f, 1f);
            AddBoxCollider(go, new Vector3(1f, 1.1f, 1f), new Vector3(0, 0.55f, 0), false, "Wall");
        }

        private static void PlaceColumn(Transform parent, Vector3 worldPos)
        {
            GameObject go = SpawnKenney("column", parent, worldPos);
            go.transform.localScale = new Vector3(1f, 3f, 1f);
            AddBoxCollider(go, new Vector3(0.5f, 1.1f, 0.5f), new Vector3(0, 0.55f, 0), false, "Wall");
        }

        private static void PlaceTorch(Transform parent, Vector3 worldPos, Material poleMat, Material flameMat)
        {
            Transform torch = NewChild(parent, "Torch");
            torch.position = worldPos;
            BoxCollider torchCollider = torch.gameObject.AddComponent<BoxCollider>();
            torchCollider.center = new Vector3(0f, 0.9f, 0f);
            torchCollider.size = new Vector3(0.28f, 1.8f, 0.28f);
            torch.gameObject.tag = "Wall";

            CreatePrimitiveChild(torch, PrimitiveType.Cylinder, "Pole", new Vector3(0, 0.9f, 0), new Vector3(0.08f, 0.9f, 0.08f), poleMat, removeCollider: true);
            CreatePrimitiveChild(torch, PrimitiveType.Sphere, "Flame", new Vector3(0, 1.82f, 0), new Vector3(0.16f, 0.24f, 0.16f), flameMat, removeCollider: true);

            Transform lightT = NewChild(torch, "Light");
            lightT.localPosition = new Vector3(0, 1.82f, 0);
            Light light = lightT.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            light.range = 4.25f;
            light.intensity = 3.15f;
            lightT.gameObject.AddComponent<TorchFlicker>();

            AddFlameParticles(torch, new Vector3(0, 1.85f, 0), new Color(1f, 0.55f, 0.15f));
        }

        private static void AddFlameParticles(Transform parent, Vector3 localPos, Color color)
        {
            Transform fx = NewChild(parent, "FlameFX");
            fx.localPosition = localPos;

            ParticleSystem ps = fx.gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.5f;
            main.startSpeed = 0.6f;
            main.startSize = 0.22f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 16;
            main.gravityModifier = -0.02f;

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.06f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(new Color(0.35f, 0.08f, 0.04f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var renderer = fx.GetComponent<ParticleSystemRenderer>();
            Material fxMat = new Material(GetUnlitShader());
            ApplyUnlitColor(fxMat, color);
            renderer.material = fxMat;
        }

        // ==================== JUGADOR / ENEMIGOS / CÁMARA / PROYECTIL ====================

        private static GameObject BuildPlayer(Transform parent, Vector3 worldPos, Material kenneyMat)
        {
            GameObject go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.tag = "Player";

            GameObject model = SpawnKenneyChild("character-human", go.transform, Vector3.zero, kenneyMat);
            model.name = "Model";
            Animator playerAnimator = model.GetComponent<Animator>();
            if (playerAnimator == null) playerAnimator = model.AddComponent<Animator>();
            playerAnimator.runtimeAnimatorController = KenneyAnimatorSetup.BuildPlayerController();
            playerAnimator.applyRootMotion = false;

            CharacterController cc = go.AddComponent<CharacterController>();
            cc.height = 1f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.5f, 0);
            cc.skinWidth = 0.02f;
            cc.minMoveDistance = 0f;

            go.AddComponent<Health>();
            go.AddComponent<PlayerController>();
            go.AddComponent<DamageFeedback>();

            TrailRenderer dashTrail = go.AddComponent<TrailRenderer>();
            dashTrail.time = 0.12f;
            dashTrail.startWidth = 0.42f;
            dashTrail.endWidth = 0f;
            dashTrail.minVertexDistance = 0.08f;
            Material dashMat = new Material(GetUnlitShader());
            ApplyUnlitColor(dashMat, new Color(0.1f, 0.75f, 1f, 0.65f));
            dashTrail.material = dashMat;

            return go;
        }

        private static void BuildCamera(Transform parent, Transform target)
        {
            GameObject go = new GameObject("Main Camera");
            go.transform.SetParent(parent, false);
            go.tag = "MainCamera";

            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.75f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camData.renderPostProcessing = true;

            go.AddComponent<AudioListener>();

            CameraFollow follow = go.AddComponent<CameraFollow>();
            follow.SetTarget(target);
            follow.SetOffset(new Vector3(0, 10f, -7.2f));
            go.transform.rotation = Quaternion.Euler(54.25f, 0f, 0f);
        }

        private static Projectile BuildProjectilePrefab(Material mat, AudioClip hitClip)
        {
            string path = "Assets/Prefabs/Projectile.prefab";

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.localScale = Vector3.one * 0.18f;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            Object.DestroyImmediate(go.GetComponent<Collider>());
            SphereCollider col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            go.AddComponent<Projectile>().ConfigureAudio(hitClip);

            Transform trailT = NewChild(go.transform, "Trail");
            TrailRenderer trail = trailT.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = 0.1f;
            trail.endWidth = 0f;
            Material trailMat = new Material(GetUnlitShader());
            ApplyUnlitColor(trailMat, PlayerBlue);
            trail.material = trailMat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return prefab.GetComponent<Projectile>();
        }

        private static EnemyBolt BuildEnemyBoltPrefab(Material mat)
        {
            const string path = "Assets/Prefabs/EnemyBolt.prefab";
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "EnemyBolt";
            go.transform.localScale = Vector3.one * 0.22f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            SphereCollider col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            go.AddComponent<EnemyBolt>();

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.28f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0f;
            Material trailMat = new Material(GetUnlitShader());
            ApplyUnlitColor(trailMat, new Color(1f, 0.08f, 0.025f));
            trail.material = trailMat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<EnemyBolt>();
        }

        private static EnemyAI BuildEnemy(Transform parent, Vector3 worldPos, Material mat, AnimatorController animatorController, float scale = 1f, int maxHealth = 3)
        {
            GameObject go = new GameObject(scale > 1.2f ? "Boss" : "Enemy");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * scale;

            GameObject model = SpawnKenneyChild("character-orc", go.transform, Vector3.zero, mat);
            model.name = "Model";
            Animator enemyAnimator = model.GetComponent<Animator>();
            if (enemyAnimator == null) enemyAnimator = model.AddComponent<Animator>();
            enemyAnimator.runtimeAnimatorController = animatorController;
            enemyAnimator.applyRootMotion = false;

            CharacterController cc = go.AddComponent<CharacterController>();
            cc.height = 1f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.5f, 0);
            cc.skinWidth = 0.02f;
            cc.minMoveDistance = 0f;

            go.AddComponent<Health>().SetMax(maxHealth);
            EnemyAI ai = go.AddComponent<EnemyAI>();
            go.AddComponent<DamageFeedback>();
            go.AddComponent<EnemyHealthBar>();

            return ai;
        }

        private static void BuildRune(Transform parent, Vector3 worldPos, Material mat, Color lightColor, float radius)
        {
            Transform rune = NewChild(parent, "RunicCircle");
            rune.position = worldPos + Vector3.up * 0.025f;
            CreatePrimitiveChild(rune, PrimitiveType.Cylinder, "OuterSigil", Vector3.zero,
                new Vector3(radius, 0.012f, radius), mat, removeCollider: true);
            CreatePrimitiveChild(rune, PrimitiveType.Cube, "GlyphA", new Vector3(0f, 0.04f, 0f),
                new Vector3(radius * 1.4f, 0.018f, 0.09f), mat, removeCollider: true).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            CreatePrimitiveChild(rune, PrimitiveType.Cube, "GlyphB", new Vector3(0f, 0.04f, 0f),
                new Vector3(radius * 1.4f, 0.018f, 0.09f), mat, removeCollider: true).transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            Transform lightT = NewChild(rune, "RuneGlow");
            lightT.localPosition = Vector3.up * 0.5f;
            Light light = lightT.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColor;
            light.range = radius * 3f;
            light.intensity = 1.25f;
            lightT.gameObject.AddComponent<TorchFlicker>();
        }

        private static void BuildKillGate(Transform parent, Vector3 worldPos, int requiredKills, Material sealMat)
        {
            GameObject gate = new GameObject($"KillGate_{requiredKills}");
            gate.transform.SetParent(parent, false);
            gate.transform.position = worldPos;
            GameObject model = SpawnKenneyChild("gate", gate.transform, Vector3.zero, GetKenneyMaterial());
            model.name = "GateModel";
            CreatePrimitiveChild(gate.transform, PrimitiveType.Cube, "SealBandLow", new Vector3(0f, 0.68f, 0f),
                new Vector3(0.12f, 0.09f, 2.4f), sealMat, removeCollider: true);
            CreatePrimitiveChild(gate.transform, PrimitiveType.Cube, "SealBandHigh", new Vector3(0f, 1.28f, 0f),
                new Vector3(0.12f, 0.09f, 2.4f), sealMat, removeCollider: true);
            BoxCollider blocker = gate.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, 2f, 0f);
            blocker.size = new Vector3(0.45f, 4f, 3.2f);
            gate.tag = "Wall";
            gate.AddComponent<KillGate>().Configure(requiredKills);
        }

        private static void BuildSpikeTrap(Transform parent, Vector3 worldPos)
        {
            GameObject trap = new GameObject("SpikeTrap");
            trap.transform.SetParent(parent, false);
            trap.transform.position = worldPos;
            GameObject model = SpawnKenneyChild("trap", trap.transform, new Vector3(0f, -0.08f, 0f), GetKenneyMaterial());
            model.name = "TrapVisual";
            BoxCollider trigger = trap.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 0.15f, 0f);
            trigger.size = new Vector3(0.8f, 0.45f, 0.8f);
            trigger.isTrigger = true;
            trap.AddComponent<SpikeTrap>();
        }

        private static void BuildPowerUp(Transform parent, Vector3 worldPos, PowerUpType type, AudioClip pickupClip)
        {
            string modelName;
            Color glowColor;
            switch (type)
            {
                case PowerUpType.Damage:
                    modelName = "weapon-sword";
                    glowColor = GoldGem;
                    break;
                case PowerUpType.Speed:
                    modelName = "key";
                    glowColor = CyanGem;
                    break;
                default:
                    modelName = "potion";
                    glowColor = GreenGem;
                    break;
            }

            GameObject go = new GameObject($"PowerUp_{type}");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            GameObject model = SpawnKenneyChild(modelName, go.transform, new Vector3(0, 0.15f, 0), GetKenneyMaterial());
            model.name = "Model";

            SphereCollider col = go.AddComponent<SphereCollider>();
            col.center = new Vector3(0, 0.3f, 0);
            col.radius = 0.5f;
            col.isTrigger = true;

            Material glowMat = CreateMaterial($"Mat_PowerUp_{type}", glowColor * 0.28f, 0.35f, glowColor * 2.2f);
            CreatePrimitiveChild(go.transform, PrimitiveType.Cylinder, "GlowPedestal", new Vector3(0f, 0.025f, 0f),
                new Vector3(0.62f, 0.025f, 0.62f), glowMat, removeCollider: true);
            Transform orbit = NewChild(go.transform, "Orbit");
            orbit.localPosition = new Vector3(0f, 0.42f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                CreatePrimitiveChild(orbit, PrimitiveType.Sphere, "Wisp", new Vector3(Mathf.Cos(angle) * 0.52f, 0f, Mathf.Sin(angle) * 0.52f),
                    Vector3.one * 0.1f, glowMat, removeCollider: true);
            }

            Transform lightT = NewChild(go.transform, "Glow");
            lightT.localPosition = new Vector3(0, 0.3f, 0);
            Light glow = lightT.gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.range = 3.35f;
            glow.intensity = 2.7f;

            Transform labelT = NewChild(go.transform, "PowerUpLabel");
            labelT.localPosition = new Vector3(0f, 1.15f, 0f);
            TextMesh label = labelT.gameObject.AddComponent<TextMesh>();
            label.text = type == PowerUpType.Health ? "VIDA +2" : type == PowerUpType.Damage ? "DAÑO x2" : "VELOCIDAD";
            label.fontSize = 56;
            label.characterSize = 0.042f;
            label.fontStyle = FontStyle.Bold;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = glowColor;

            Transform fxT = NewChild(go.transform, "PickupParticles");
            fxT.localPosition = new Vector3(0f, 0.2f, 0f);
            ParticleSystem ps = fxT.gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0.32f;
            main.startSize = 0.065f;
            main.startColor = glowColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 16;
            var emission = ps.emission;
            emission.rateOverTime = 6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.52f;
            fxT.GetComponent<ParticleSystemRenderer>().material = glowMat;

            PowerUp powerUp = go.AddComponent<PowerUp>();
            powerUp.Configure(type);
            powerUp.ConfigureAudio(pickupClip);
        }

        // ==================== ASSETS KENNEY (MINI DUNGEON, CC0) ====================

        private static AudioClip LoadAudio(string clipName)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{clipName}.ogg");
        }

        private static AudioClip LoadCustomAudio(string clipName)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CustomAudioFolder}/{clipName}.mp3");
            if (clip == null) Debug.LogError($"[LevelBuilder] No se pudo cargar el audio personalizado {clipName}.mp3");
            return clip;
        }

        private static GameObject LoadKenney(string modelName)
        {
            string path = $"{KenneyModelsFolder}/{modelName}.fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[LevelBuilder] No se encontró el modelo Kenney '{modelName}' en {path}");
            }
            return prefab;
        }

        private static GameObject SpawnKenney(string modelName, Transform parent, Vector3 worldPos)
        {
            GameObject prefab = LoadKenney(modelName);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent);
            go.name = modelName;
            go.transform.position = worldPos;
            ApplyKenneyMaterial(go, GetKenneyMaterial());
            AddAutomaticPropCollider(modelName, go);
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        private static void AddAutomaticPropCollider(string modelName, GameObject go)
        {
            bool solid = modelName == "barrel" || modelName == "table" || modelName == "chair" ||
                         modelName == "chest" || modelName == "rocks" || modelName == "stones" ||
                         modelName == "wood-structure" || modelName == "pot" ||
                         modelName == "shield-round" || modelName == "shield-rectangle" ||
                         modelName == "weapon-sword";
            if (!solid || go.GetComponent<Collider>() != null) return;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.center = go.transform.InverseTransformPoint(bounds.center);
            Vector3 size = go.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Max(0.22f, Mathf.Abs(size.x)), Mathf.Max(0.3f, Mathf.Abs(size.y)), Mathf.Max(0.22f, Mathf.Abs(size.z)));
            go.tag = "Wall";
        }

        private static GameObject SpawnKenneyChild(string modelName, Transform parent, Vector3 localPos, Material mat)
        {
            GameObject prefab = LoadKenney(modelName);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            ApplyKenneyMaterial(go, mat);
            return go;
        }

        private static void ApplyKenneyMaterial(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                if (mats.Length == 0) mats = new[] { mat };
                r.sharedMaterials = mats;
            }
        }

        private static void AddBoxCollider(GameObject go, Vector3 size, Vector3 center, bool trigger, string tag)
        {
            if (go == null) return;
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.center = center;
            col.isTrigger = trigger;
            if (!string.IsNullOrEmpty(tag)) go.tag = tag;
        }

        private static Material GetKenneyMaterial()
        {
            string path = $"{MaterialsFolder}/Mat_KenneyDungeon.mat";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(KenneyTexturePath);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GetKenneyBossMaterial()
        {
            string path = $"{MaterialsFolder}/Mat_KenneyDungeon_Boss.mat";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(KenneyTexturePath);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.5f));
            mat.SetFloat("_Smoothness", 0.2f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", new Color(0.35f, 0.02f, 0.02f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ==================== POST-PROCESADO / LUZ / NIEBLA ====================

        private static void SetupLighting()
        {
            GameObject sunGo = new GameObject("Directional Light (Luna)");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.75f, 0.78f, 0.95f);
            sun.intensity = 0.42f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sunGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.075f, 0.11f);
        }

        private static void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.014f;
        }

        private static void BuildPostProcessing(Transform root)
        {
            string path = "Assets/Art/" + root.name + "_PostFX.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.45f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.42f);
            vignette.color.Override(new Color(0.02f, 0.01f, 0.01f));

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(-0.18f);
            colorAdjustments.contrast.Override(23f);
            colorAdjustments.saturation.Override(-4f);
            colorAdjustments.colorFilter.Override(new Color(0.92f, 0.95f, 1f));

            EditorUtility.SetDirty(profile);

            GameObject volGo = new GameObject("GlobalVolume");
            volGo.transform.SetParent(root, false);
            Volume vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.weight = 1f;
            vol.sharedProfile = profile;
        }

        // ==================== MENÚ PRINCIPAL ====================

        public static void BuildMainMenu()
        {
            EnsureFolders();
            EnsureCustomAudioImportSettings();
            KenneyTextureSetup.FixColormapImport();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            SetupFog();

            Material kenneyMat = GetKenneyMaterial();
            Material bossMat = GetKenneyBossMaterial();
            Material torchFlameMat = CreateMaterial("Mat_TorchFlame", new Color(0.95f, 0.4f, 0.08f), 0.2f, new Color(0.95f, 0.4f, 0.08f) * 2.2f);
            Material torchPoleMat = CreateMaterial("Mat_TorchPole", new Color(0.05f, 0.04f, 0.03f), 0.3f);
            Material menuRuneMat = CreateMaterial("Mat_MenuRune", new Color(0.16f, 0.01f, 0.02f), 0.25f, new Color(0.8f, 0.03f, 0.01f));

            GameObject root = new GameObject("MainMenu");
            GameObject audioDirectorGo = new GameObject("DungeonAudioDirector");
            audioDirectorGo.transform.SetParent(root.transform, false);
            audioDirectorGo.AddComponent<DungeonAudioDirector>().Configure(null, null, 65.5f,
                LoadCustomAudio("Ambiente"), LoadCustomAudio("BossFinal"));
            Transform backdrop = NewChild(root.transform, "Backdrop");

            for (int x = -5; x <= 5; x++)
            {
                for (int z = -2; z <= 5; z++)
                {
                    SpawnKenney("floor", backdrop, new Vector3(x, 0, z));
                }
                GameObject wall = SpawnKenney("wall", backdrop, new Vector3(x, 0, 5));
                AddBoxCollider(wall, new Vector3(1f, 1.1f, 1f), new Vector3(0, 0.55f, 0), false, null);
                SpawnKenney("wall", backdrop, new Vector3(x, 1f, 5));
            }

            SpawnKenney("gate", backdrop, new Vector3(2.4f, 0, 4.85f));
            SpawnKenney("column", backdrop, new Vector3(-4.2f, 0, 2.8f));
            SpawnKenney("column", backdrop, new Vector3(4.2f, 0, 2.8f));
            SpawnKenney("column", backdrop, new Vector3(-2.4f, 0, 4.6f));
            SpawnKenney("column", backdrop, new Vector3(2.4f, 0, 4.6f));
            SpawnKenney("stairs", backdrop, new Vector3(2.4f, 0, -1f));
            SpawnKenney("banner", backdrop, new Vector3(-3.4f, 0, 4.9f));
            SpawnKenney("banner", backdrop, new Vector3(3.4f, 0, 4.9f));
            SpawnKenney("chest", backdrop, new Vector3(4.1f, 0, 1.1f));
            SpawnKenney("rocks", backdrop, new Vector3(-3.6f, 0, 0.4f));
            SpawnKenney("weapon-sword", backdrop, new Vector3(1.65f, 0, 3.9f));
            SpawnKenney("shield-round", backdrop, new Vector3(3.15f, 0, 3.9f));

            BuildRune(backdrop, new Vector3(2.4f, 0, 2f), menuRuneMat, new Color(1f, 0.08f, 0.02f), 1.65f);
            PlaceTorch(backdrop, new Vector3(-2.4f, 0, 3.8f), torchPoleMat, torchFlameMat);
            PlaceTorch(backdrop, new Vector3(2.4f, 0, 3.8f), torchPoleMat, torchFlameMat);
            PlaceTorch(backdrop, new Vector3(4.3f, 0, 0f), torchPoleMat, torchFlameMat);

            GameObject hero = SpawnKenneyChild("character-human", backdrop, new Vector3(2.4f, 0, -0.2f), kenneyMat);
            hero.name = "HeroSilhouette";
            hero.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            GameObject guardian = SpawnKenneyChild("character-orc", backdrop, new Vector3(2.4f, 0, 2.8f), bossMat);
            guardian.name = "CryptGuardianSilhouette";
            guardian.transform.localScale = Vector3.one * 1.65f;
            guardian.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(root.transform, false);
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 37f;
            cam.nearClipPlane = 0.1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            var menuCamData = cam.GetUniversalAdditionalCameraData();
            menuCamData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            menuCamData.renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0.8f, 4.5f, -7.7f);
            camGo.transform.LookAt(new Vector3(1.2f, 1.05f, 2.1f));

            BuildPostProcessing(root.transform);
            BuildMainMenuUI(root.transform);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            SetBuildScenesOrder(MainMenuScenePath, Level1ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[LevelBuilder] Menú principal generado y guardado en " + MainMenuScenePath);
        }

        private static void BuildMainMenuUI(Transform root)
        {
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform canvasT = canvasGo.transform;

            GameObject esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(root, false);
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            // ---------- Panel de menú ----------
            GameObject menuPanel = CreateUIImage(canvasT, "MenuPanel", new Color(0, 0, 0, 0.16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            GameObject titlePlate = CreateUIImage(menuPanel.transform, "TitlePlate", new Color(0.018f, 0.012f, 0.018f, 0.88f),
                new Vector2(0f, 0f), new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero);
            Text title = CreateText(titlePlate.transform, "Title", "ECOS DE LA\nCRIPTA", 68, Orange,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 175), new Vector2(720, 180), TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            CreateText(titlePlate.transform, "Tagline", "DESCIENDE  •  SOBREVIVE  •  ROMPE EL ECO", 18, new Color(0.78f, 0.67f, 0.58f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 55), new Vector2(720, 34), TextAnchor.MiddleCenter);
            CreateText(titlePlate.transform, "Subtitle", "UNA AVENTURA DE TRIAS GAMES", 15, new Color(0.52f, 0.52f, 0.55f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 15), new Vector2(700, 30), TextAnchor.MiddleCenter);
            Button playBtn = CreateButton(titlePlate.transform, "PlayButton", "INICIAR DESCENSO", new Vector2(0, -72), new Vector2(360, 72), Orange, Color.black);
            Button instructionsBtn = CreateButton(titlePlate.transform, "InstructionsButton", "INSTRUCCIONES", new Vector2(0, -164), new Vector2(360, 64), WineRed, Color.white);

            // ---------- Manual del explorador ----------
            GameObject instructionsPanel = CreateUIImage(canvasT, "InstructionsPanel", new Color(0.008f, 0.006f, 0.01f, 0.86f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            GameObject manualFrame = CreateUIImage(instructionsPanel.transform, "ManualFrame", new Color(0.42f, 0.20f, 0.09f, 0.98f),
                new Vector2(0.09f, 0.07f), new Vector2(0.91f, 0.93f), Vector2.zero, Vector2.zero);
            GameObject manualInner = CreateUIImage(manualFrame.transform, "ManualInner", new Color(0.025f, 0.018f, 0.026f, 0.98f),
                Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));

            Text manualTitle = CreateText(manualInner.transform, "ManualTitle", "MANUAL DEL EXPLORADOR", 44, Orange,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(1100f, 60f), TextAnchor.MiddleCenter);
            manualTitle.fontStyle = FontStyle.Bold;
            CreateText(manualInner.transform, "ManualSubtitle", "Todo lo que necesitas antes de entrar en la oscuridad", 18, new Color(0.72f, 0.66f, 0.62f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1000f, 32f), TextAnchor.MiddleCenter);

            CreateInstructionCard(manualInner.transform, "MovementCard", "01", "MOVIMIENTO", "W A S D  +  ESPACIO", "Muévete, salta y esquiva las trampas.", new Vector2(-365f, 105f), CyanGem);
            CreateInstructionCard(manualInner.transform, "CombatCard", "02", "COMBATE", "MOUSE  +  CLIC", "Apunta y dispara contra los guardianes.", new Vector2(365f, 105f), Orange);
            CreateInstructionCard(manualInner.transform, "DashCard", "03", "IMPULSO", "SHIFT", "Evade ataques y cruza zonas de peligro.", new Vector2(-365f, -155f), new Color(0.72f, 0.4f, 0.9f));
            CreateInstructionCard(manualInner.transform, "PerspectiveCard", "04", "PERSPECTIVA", "T", "Alterna entre vista cenital y primera persona.", new Vector2(365f, -155f), GoldGem);

            Button instructionsBackBtn = CreateButton(manualInner.transform, "BackButton", "VOLVER AL MENÚ", new Vector2(0f, -355f), new Vector2(320f, 60f), Orange, Color.black);
            instructionsPanel.SetActive(false);

            // ---------- Panel de carga ----------
            GameObject loadingPanel = CreateUIImage(canvasT, "LoadingPanel", new Color(0.03f, 0.025f, 0.03f, 0.96f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(loadingPanel.transform, "LoadingTitle", "ECOS DE LA CRIPTA", 40, Orange,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(900, 60), TextAnchor.MiddleCenter);

            GameObject barGo = new GameObject("LoadingBar", typeof(RectTransform));
            barGo.transform.SetParent(loadingPanel.transform, false);
            RectTransform barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 0.5f);
            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.anchoredPosition = new Vector2(0, -10);
            barRt.sizeDelta = new Vector2(560, 26);
            Slider loadingBar = barGo.AddComponent<Slider>();
            loadingBar.interactable = false;
            loadingBar.transition = Selectable.Transition.None;
            CreateUIImage(barGo.transform, "Background", new Color(0, 0, 0, 0.6f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(barGo.transform, false);
            RectTransform fart = fillArea.GetComponent<RectTransform>();
            fart.anchorMin = Vector2.zero;
            fart.anchorMax = Vector2.one;
            fart.offsetMin = new Vector2(4, 4);
            fart.offsetMax = new Vector2(-4, -4);
            GameObject fillGo = CreateUIImage(fillArea.transform, "Fill", Orange, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            loadingBar.fillRect = fillGo.GetComponent<RectTransform>();
            loadingBar.targetGraphic = fillGo.GetComponent<Image>();
            loadingBar.direction = Slider.Direction.LeftToRight;
            loadingBar.minValue = 0;
            loadingBar.maxValue = 1;
            loadingBar.value = 0;

            Text loadingText = CreateText(loadingPanel.transform, "LoadingText", "Cargando Fase 1... 0%", 20, new Color(0.85f, 0.85f, 0.85f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(700, 30), TextAnchor.MiddleCenter);
            loadingPanel.SetActive(false);

            GameObject menuCtrlGo = new GameObject("MainMenuController");
            menuCtrlGo.transform.SetParent(root, false);
            MainMenuController controller = menuCtrlGo.AddComponent<MainMenuController>();
            controller.Bind(menuPanel, loadingPanel, loadingBar, loadingText, playBtn,
                instructionsPanel, instructionsBtn, instructionsBackBtn, "Level1");
        }

        private static void CreateInstructionCard(Transform parent, string name, string number, string title, string key,
            string description, Vector2 anchoredPosition, Color accent)
        {
            GameObject shadow = CreateUIImage(parent, name + "Shadow", new Color(0f, 0f, 0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition + new Vector2(-338f, -113f), anchoredPosition + new Vector2(342f, 117f));
            GameObject card = CreateUIImage(parent, name, new Color(0.075f, 0.058f, 0.07f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition + new Vector2(-340f, -110f), anchoredPosition + new Vector2(340f, 110f));
            shadow.transform.SetAsFirstSibling();
            CreateUIImage(card.transform, "Accent", accent, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(8f, 0f));
            Text numberText = CreateText(card.transform, "Number", number, 44, new Color(accent.r, accent.g, accent.b, 0.28f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -18f), new Vector2(90f, 60f), TextAnchor.UpperRight);
            numberText.fontStyle = FontStyle.Bold;
            Text heading = CreateText(card.transform, "Heading", title, 20, accent,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(-56f, 34f), TextAnchor.MiddleCenter);
            heading.fontStyle = FontStyle.Bold;
            Text keyText = CreateText(card.transform, "Key", key, 28, Color.white,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), new Vector2(-70f, 44f), TextAnchor.MiddleCenter);
            keyText.fontStyle = FontStyle.Bold;
            CreateText(card.transform, "Description", description, 17, new Color(0.76f, 0.73f, 0.75f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(-54f, 32f), TextAnchor.MiddleCenter);
        }

        // ==================== HUD DEL NIVEL 1 ====================

        private static void BuildLevelUI(Transform root, Health playerHealth, AudioClip hurtClip)
        {
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform canvasT = canvasGo.transform;

            GameObject esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(root, false);
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            GameObject hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(canvasT, false);
            Transform hud = hudGo.transform;
            StretchFull(hudGo.GetComponent<RectTransform>());

            GameObject damageOverlayGo = CreateUIImage(hud, "PlayerDamageOverlay", new Color(0.72f, 0.015f, 0.01f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            damageOverlayGo.GetComponent<Image>().raycastTarget = false;
            damageOverlayGo.AddComponent<PlayerDamageOverlay>().Configure(playerHealth, hurtClip);
            damageOverlayGo.transform.SetAsFirstSibling();

            Slider slider = BuildHealthSlider(hud);
            Text healthText = CreateText(hud, "HealthText", "Vida: 5/5", 20, Color.white,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -66), new Vector2(280, 30), TextAnchor.UpperLeft);
            Text scoreText = CreateText(hud, "ScoreText", "Puntaje: 0", 26, Orange,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(280, 36), TextAnchor.UpperRight);
            GameObject objectivePlate = CreateUIImage(hud, "ObjectivePlate", new Color(0.025f, 0.018f, 0.025f, 0.82f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-300f, -78f), new Vector2(300f, -18f));
            Text objectiveText = CreateText(objectivePlate.transform, "ObjectiveText", "PURGA LA ANTESALA  •  0/2", 22, new Color(1f, 0.66f, 0.2f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            objectiveText.fontStyle = FontStyle.Bold;

            Text pickupAnnouncement = CreateText(hud, "PickupAnnouncement", "✦  POWER-UP  ✦", 34, new Color(1f, 0.78f, 0.24f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 155), new Vector2(760, 56), TextAnchor.MiddleCenter);
            pickupAnnouncement.fontStyle = FontStyle.Bold;
            Outline pickupOutline = pickupAnnouncement.gameObject.AddComponent<Outline>();
            pickupOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            pickupOutline.effectDistance = new Vector2(3f, -3f);
            pickupAnnouncement.gameObject.SetActive(false);

            GameObject crosshair = BuildFirstPersonCrosshair(hud);
            Text perspectiveToast = CreateText(hud, "PerspectiveToast", "PRIMERA PERSONA", 18, new Color(0.86f, 0.78f, 0.68f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(520f, 34f), TextAnchor.MiddleCenter);
            perspectiveToast.fontStyle = FontStyle.Bold;
            Outline toastOutline = perspectiveToast.gameObject.AddComponent<Outline>();
            toastOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            toastOutline.effectDistance = new Vector2(2f, -2f);
            perspectiveToast.gameObject.AddComponent<CanvasGroup>();
            perspectiveToast.gameObject.SetActive(false);

            CameraFollow cameraRig = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cameraRig != null) cameraRig.ConfigureFirstPersonUI(crosshair, perspectiveToast);

            GameObject wonPanel = CreateUIImage(canvasT, "WonPanel", new Color(0.04f, 0.06f, 0.04f, 0.92f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(wonPanel.transform, "Title", "¡FASE 1 COMPLETADA!", 52, new Color(0.4f, 0.85f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1000, 80), TextAnchor.MiddleCenter);
            Button wonRetry = CreateButton(wonPanel.transform, "RetryButton", "JUGAR DE NUEVO", new Vector2(-160, -50), new Vector2(280, 64), Orange, Color.black);
            Button wonMenu = CreateButton(wonPanel.transform, "MenuButton", "MENÚ", new Vector2(160, -50), new Vector2(280, 64), CharcoalLight, Color.white);
            wonPanel.SetActive(false);

            GameObject lostPanel = CreateUIImage(canvasT, "LostPanel", new Color(0.08f, 0.02f, 0.02f, 0.92f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(lostPanel.transform, "Title", "GAME OVER", 58, new Color(0.85f, 0.25f, 0.25f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1000, 80), TextAnchor.MiddleCenter);
            Button lostRetry = CreateButton(lostPanel.transform, "RetryButton", "REINTENTAR", new Vector2(-160, -50), new Vector2(280, 64), WineRed, Color.white);
            Button lostMenu = CreateButton(lostPanel.transform, "MenuButton", "MENÚ", new Vector2(160, -50), new Vector2(280, 64), CharcoalLight, Color.white);
            lostPanel.SetActive(false);

            GameObject hudCtrlGo = new GameObject("HUDController");
            hudCtrlGo.transform.SetParent(root, false);
            HUDController hudController = hudCtrlGo.AddComponent<HUDController>();
            hudController.Bind(slider, healthText, scoreText, objectiveText, pickupAnnouncement,
                wonPanel, lostPanel, wonRetry, lostRetry, wonMenu, lostMenu);
        }

        private static GameObject BuildFirstPersonCrosshair(Transform hud)
        {
            GameObject root = new GameObject("FirstPersonCrosshair", typeof(RectTransform));
            root.transform.SetParent(hud, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(92f, 92f);
            root.AddComponent<FirstPersonReticle>();

            Color shadow = new Color(0f, 0f, 0f, 0.82f);
            Color bright = new Color(0.98f, 0.63f, 0.22f, 0.98f);
            CreateCrosshairBar(root.transform, "TopShadow", new Vector2(1f, 18f), new Vector2(0f, 25f), shadow);
            CreateCrosshairBar(root.transform, "BottomShadow", new Vector2(1f, 18f), new Vector2(0f, -25f), shadow);
            CreateCrosshairBar(root.transform, "LeftShadow", new Vector2(18f, 1f), new Vector2(-25f, 0f), shadow);
            CreateCrosshairBar(root.transform, "RightShadow", new Vector2(18f, 1f), new Vector2(25f, 0f), shadow);
            CreateCrosshairBar(root.transform, "Top", new Vector2(3f, 16f), new Vector2(0f, 24f), bright);
            CreateCrosshairBar(root.transform, "Bottom", new Vector2(3f, 16f), new Vector2(0f, -24f), bright);
            CreateCrosshairBar(root.transform, "Left", new Vector2(16f, 3f), new Vector2(-24f, 0f), bright);
            CreateCrosshairBar(root.transform, "Right", new Vector2(16f, 3f), new Vector2(24f, 0f), bright);
            CreateCrosshairBar(root.transform, "CenterShadow", new Vector2(8f, 8f), Vector2.zero, shadow);
            CreateCrosshairBar(root.transform, "Center", new Vector2(4f, 4f), Vector2.zero, Color.white);
            root.SetActive(false);
            return root;
        }

        private static void CreateCrosshairBar(Transform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            GameObject bar = CreateUIImage(parent, name, color,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position - size * 0.5f, position + size * 0.5f);
            bar.GetComponent<Image>().raycastTarget = false;
        }

        private static Slider BuildHealthSlider(Transform hud)
        {
            GameObject sliderGo = new GameObject("HealthSlider", typeof(RectTransform));
            sliderGo.transform.SetParent(hud, false);
            RectTransform srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 1);
            srt.anchorMax = new Vector2(0, 1);
            srt.pivot = new Vector2(0, 1);
            srt.anchoredPosition = new Vector2(30, -30);
            srt.sizeDelta = new Vector2(280, 28);

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            CreateUIImage(sliderGo.transform, "Background", new Color(0, 0, 0, 0.6f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform fart = fillArea.GetComponent<RectTransform>();
            fart.anchorMin = Vector2.zero;
            fart.anchorMax = Vector2.one;
            fart.offsetMin = new Vector2(4, 4);
            fart.offsetMax = new Vector2(-4, -4);

            GameObject fillGo = CreateUIImage(fillArea.transform, "Fill", new Color(0.75f, 0.18f, 0.2f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.targetGraphic = fillGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 5;
            slider.wholeNumbers = true;
            slider.value = 5;

            return slider;
        }

        // ==================== UTILIDADES DE BAJO NIVEL ====================

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject CreatePrimitiveChild(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat, bool removeCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (removeCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            return go;
        }

        private static Material CreateMaterial(string name, Color baseColor, float smoothness, Color? emission = null)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emission.Value);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Shader GetUnlitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        private static void ApplyUnlitColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        private static GameObject CreateUIImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Text txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = GetDefaultFont();
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 sizeDelta, Color bg, Color fg)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            go.AddComponent<Image>().color = bg;
            Button btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bg * 1.15f;
            colors.pressedColor = bg * 0.8f;
            btn.colors = colors;

            Text txt = CreateText(go.transform, "Label", label, 24, fg, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return btn;
        }

        private static Font GetDefaultFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets/Art");
            CreateFolderIfMissing("Assets/Art/Materials");
            CreateFolderIfMissing("Assets/Audio");
            CreateFolderIfMissing("Assets/Prefabs");
            CreateFolderIfMissing("Assets/Scenes");
        }

        private static void EnsureCustomAudioImportSettings()
        {
            ConfigureAudioImporter($"{CustomAudioFolder}/Ambiente.mp3", stream: true);
            ConfigureAudioImporter($"{CustomAudioFolder}/BossFinal.mp3", stream: true);
            ConfigureAudioImporter($"{CustomAudioFolder}/Disparo.mp3", stream: false);
            ConfigureAudioImporter($"{CustomAudioFolder}/Dano.mp3", stream: false);
        }

        private static void ConfigureAudioImporter(string path, bool stream)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType targetLoadType = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            AudioCompressionFormat targetFormat = stream ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            bool dirty = settings.loadType != targetLoadType || settings.compressionFormat != targetFormat ||
                         importer.loadInBackground != stream || settings.preloadAudioData == stream;
            if (!dirty) return;
            settings.loadType = targetLoadType;
            settings.compressionFormat = targetFormat;
            settings.quality = stream ? 0.72f : 1f;
            settings.preloadAudioData = !stream;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = stream;
            importer.SaveAndReimport();
        }

        private static void CreateFolderIfMissing(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void EnsureTag(string tag)
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets.Length == 0) return;
            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }

        private static void SetBuildScenesOrder(params string[] orderedPaths)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var p in orderedPaths) scenes.Add(new EditorBuildSettingsScene(p, true));
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (System.Array.IndexOf(orderedPaths, existing.path) < 0) scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
