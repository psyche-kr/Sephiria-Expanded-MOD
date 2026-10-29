using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal sealed class JournalWeaponOutline : MonoBehaviour
    {
        private const string RecordPrefix = "HardModeClearedHighestPoint_Weapon_";
        private const int SparkleMode = 3;
        private const int SparkleStage = 60;
        private const string SparkleSuffix = "_S";

        internal static int ClearedPoints(int entityId)
        {
            return entityId >= 0 && SaveManager.Current != null ? SaveManager.Current.GetInt(RecordPrefix + entityId, 0) : 0;
        }

        internal static bool SetClearedPoints(IEnumerable<int> entityIds, int points)
        {
            if (SaveManager.Current == null) return false;

            foreach (int entityId in entityIds)
            {
                if (entityId >= 0) SaveManager.Current.SetInt(RecordPrefix + entityId, points);
            }

            SaveManager.Save(true, false);
            weaponProgressVersion++;
            return true;
        }
        private const string BaseSlotFileName = "BaseSlot.png";
        private const float JournalSearchInterval = 1f;
        private const float FrameRefreshInterval = 0.2f;

        private readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
        private readonly Dictionary<Image, Sprite> originalFrameSprites = new Dictionary<Image, Sprite>();

        private UI_JournalContent_Weapon currentJournal;
        private Sprite baseSlotSprite;
        private float journalSearchTimer;
        private float frameRefreshTimer;
        private int lastMode = -1;
        private bool previousJournalActive;
        private int lastBaseChildCount = -1;
        private int lastTier2ChildCount = -1;
        private int lastTier3ChildCount = -1;
        private int lastBaseFirstId = -1;
        private int lastTier2FirstId = -1;
        private int lastTier3FirstId = -1;
        private static int weaponProgressVersion;
        private int lastAppliedWeaponProgressVersion = -1;

        private static JournalWeaponOutline instance;

        internal static void ApplyToNewIcon(UI_WeaponIcon icon)
        {
            if (instance == null || instance.lastMode <= 0 || icon == null) return;
            if (icon.GetComponentInParent<UI_JournalContent_Weapon>(true) == null) return;

            instance.ApplyToIcon(icon);
        }

        private void Start()
        {
            instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindJournal();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreAllFrameSprites();
            foreach (Sprite sprite in sprites.Values)
                if (sprite != null) Destroy(sprite.texture);
            sprites.Clear();
            if (baseSlotSprite != null)
                Destroy(baseSlotSprite.texture);
            baseSlotSprite = null;
            originalFrameSprites.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            currentJournal = null;
            previousJournalActive = false;
            journalSearchTimer = 0f;
            frameRefreshTimer = 0f;
            originalFrameSprites.Clear();
            ResetStructureCache();
            FindJournal();
        }

        private void Update()
        {
            int mode = Mathf.Clamp(ModSettings.JournalWeaponOutlineMode, 0, SparkleMode);
            if (mode != lastMode)
            {
                lastMode = mode;
                if (mode == 0)
                    RestoreAllFrameSprites();
                else
                {
                    LoadImages(mode);
                    ResetStructureCache();
                    frameRefreshTimer = 0f;
                    journalSearchTimer = JournalSearchInterval;
                }
            }

            if (mode != 0 && (currentJournal == null || !currentJournal.gameObject.activeInHierarchy))
            {
                journalSearchTimer += Time.unscaledDeltaTime;
                if (journalSearchTimer >= JournalSearchInterval)
                {
                    journalSearchTimer = 0f;
                    FindJournal();
                }
            }

            if (mode == 0)
                return;

            if (currentJournal == null)
                return;

            if (!currentJournal.gameObject.activeInHierarchy)
            {
                previousJournalActive = false;
                return;
            }

            frameRefreshTimer -= Time.unscaledDeltaTime;
            if (!previousJournalActive || weaponProgressVersion != lastAppliedWeaponProgressVersion || HasJournalStructureChanged() || frameRefreshTimer <= 0f)
            {
                previousJournalActive = true;
                ApplyBorders(currentJournal);
                SaveCurrentStructure();
                frameRefreshTimer = FrameRefreshInterval;
            }
        }

        private void FindJournal()
        {
            UI_JournalContent_Weapon[] journals = FindObjectsByType<UI_JournalContent_Weapon>(FindObjectsSortMode.None);
            if (journals == null || journals.Length == 0)
                return;

            for (int i = 0; i < journals.Length; i++)
            {
                UI_JournalContent_Weapon journal = journals[i];
                if (journal != null)
                {
                    currentJournal = journal;
                    return;
                }
            }
        }

        [HarmonyPatch(typeof(BossSpawner), "ApplyClearProgressionLocal")]
        private static class ClearRecord
        {
            [HarmonyPostfix]
            private static void After(int gameClearType)
            {
                if (gameClearType <= 0) return;

                try
                {
                    if (!TryRecordCurrentWeapon(0)) Debug.Log("[Sephiria Expanded][WeaponOutline] 클리어 기록 실패: 하드모드 점수 또는 든 무기를 찾지 못했습니다.");
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Sephiria Expanded][WeaponOutline] 클리어 기록 오류: " + exception);
                }
            }
        }

        private static bool TryRecordCurrentWeapon(int pointsOverride)
        {
            if (DungeonManager.Instance == null || SaveManager.Current == null)
                return false;
            int points = pointsOverride > 0 ? pointsOverride : DungeonManager.Instance.CalculateCurrentHardModePoints();
            if (points <= 0)
                return false;
            PlayerAvatar player = CombatManager.Instance ? CombatManager.Instance.CurrentPlayer : null;
            if (player == null)
            {
                PlayerAvatar[] players = FindObjectsByType<PlayerAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && players[i].isLocalPlayer)
                    {
                        player = players[i];
                        break;
                    }
                }
            }
            WeaponControllerSimple controller = null;
            if (player != null)
                controller = player.GetComponent<WeaponControllerSimple>();

            WeaponSimple weapon = null;
            if (controller != null)
                weapon = controller.currentWeapon;
            if (weapon == null)
                return false;
            string key = RecordPrefix + weapon.entityId;
            int previous = SaveManager.Current.GetInt(key, 0);
            if (points > previous)
            {
                SaveManager.Current.SetInt(key, points);
                SaveManager.Save(true, false);
                weaponProgressVersion++;
                int previousStage = GetStageForPoints(previous);
                int currentStage = GetStageForPoints(points);
                if (currentStage > previousStage && ModSettings.JournalWeaponOutlineMode != 0)
                    ShowBorderUpdatedMessage(weapon);
            }
            return true;
        }

        private static int GetStageForPoints(int points)
        {
            return points >= 60 ? 60 : points >= 50 ? 50 : points >= 30 ? 30 : points >= 15 ? 15 : 0;
        }

        private static void ShowBorderUpdatedMessage(WeaponSimple weapon)
        {
            if (weapon == null)
                return;

            string weaponName;
            try
            {
                WeaponEntity entity = WeaponDatabase.FindWeaponById(weapon.entityId);
                weaponName = null;
                if (entity != null)
                    weaponName = entity.Name;
            }
            catch
            {
                weaponName = null;
            }

            if (string.IsNullOrEmpty(weaponName))
            {
                try
                {
                    weaponName = weapon.Name;
                }
                catch
                {
                    weaponName = null;
                }
            }

            if (string.IsNullOrEmpty(weaponName))
                weaponName = weapon.name;
            if (string.IsNullOrEmpty(weaponName))
                weaponName = "무기";

            UI_HUDLogViewer[] viewers = FindObjectsByType<UI_HUDLogViewer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < viewers.Length; i++)
            {
                UI_HUDLogViewer viewer = viewers[i];
                if (viewer != null && viewer.gameObject.activeInHierarchy)
                {
                    viewer.SpawnLog(weaponName + "의 테두리가 변경되었습니다.", Color.white);
                    return;
                }
            }
        }

        private void LoadImages(int mode)
        {
            string prefix = mode == 1 ? "A_" : "B_";
            int[] stages = { 15, 30, 50, 60 };
            for (int i = 0; i < stages.Length; i++)
            {
                int stage = stages[i];
                int key = mode * 100 + stage;
                string suffix = mode == SparkleMode && stage == SparkleStage ? SparkleSuffix : string.Empty;
                if (!sprites.ContainsKey(key))
                    sprites[key] = LoadSprite(prefix + stage + suffix + ".png");
            }

            if (baseSlotSprite == null)
                baseSlotSprite = LoadSprite(BaseSlotFileName);
        }

        private Sprite LoadSprite(string fileName)
        {
            string gameDirectory = Path.GetDirectoryName(Application.dataPath);
            string path = Path.Combine(gameDirectory, "AddOns", "SephiriaExpanded", "Border", fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Sephiria Expanded] Journal Weapon 테두리 파일 없음: " + fileName);
                return null;
            }
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assembly assembly = Assembly.Load("UnityEngine.ImageConversionModule");
                Type imageConversion = assembly.GetType("UnityEngine.ImageConversion");
                MethodInfo loadImage = imageConversion.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
                if (loadImage == null || !(bool)loadImage.Invoke(null, new object[] { texture, bytes }))
                {
                    Destroy(texture);
                    return null;
                }
                texture.filterMode = FilterMode.Point;
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sephiria Expanded] Journal Weapon 테두리 로드 실패: " + exception.Message);
                return null;
            }
        }

        private int ApplyBorders(UI_JournalContent_Weapon journal)
        {
            if (journal == null)
                return 0;
            int count = 0;
            count += ApplyToContainer(journal.baseWeaponContainer);
            count += ApplyToContainer(journal.tier2WeaponContainer);
            count += ApplyToContainer(journal.tier3WrapperContainer);
            return count;
        }

        private int ApplyToContainer(Transform container)
        {
            if (container == null)
                return 0;
            UI_WeaponIcon[] icons = container.GetComponentsInChildren<UI_WeaponIcon>(true);
            for (int i = 0; i < icons.Length; i++)
                ApplyToIcon(icons[i]);
            return icons.Length;
        }

        private void ApplyToIcon(UI_WeaponIcon icon)
        {
            if (icon == null || SaveManager.Current == null)
                return;
            int entityId = icon.WeaponEntity == null ? -1 : icon.WeaponEntity.id;
            int simpleId = icon.WeaponSimple == null ? -1 : icon.WeaponSimple.entityId;
            int points = 0;
            if (entityId >= 0)
                points = SaveManager.Current.GetInt(RecordPrefix + entityId, 0);
            if (simpleId >= 0 && simpleId != entityId)
                points = Mathf.Max(points, SaveManager.Current.GetInt(RecordPrefix + simpleId, 0));
            Sprite border = GetBorderSprite(points);
            SetFrameSprite(icon, border);
        }

        private Sprite GetBorderSprite(int points)
        {
            int stage = points >= 60 ? 60 : points >= 50 ? 50 : points >= 30 ? 30 : points >= 15 ? 15 : 0;
            if (stage == 0)
                return null;
            if (sprites.TryGetValue(lastMode * 100 + stage, out Sprite sprite))
                return sprite;
            return null;
        }

        private void SetFrameSprite(UI_WeaponIcon icon, Sprite border)
        {
            Image frame = FindFrameImage(icon);
            if (frame == null)
                return;

            Sprite original;
            if (!originalFrameSprites.TryGetValue(frame, out original))
            {
                original = frame.sprite;
                originalFrameSprites[frame] = original;
            }

            frame.sprite = border ?? original ?? baseSlotSprite;
        }

        private static Image FindFrameImage(UI_WeaponIcon icon)
        {
            if (icon == null)
                return null;

            Transform child = icon.transform.Find("Image");
            if (child != null)
                return child.GetComponent<Image>();

            Image[] images = icon.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject.name == "Image")
                    return images[i];
            }

            return null;
        }

        private void RestoreAllFrameSprites()
        {
            foreach (KeyValuePair<Image, Sprite> pair in originalFrameSprites)
            {
                if (pair.Key != null)
                    pair.Key.sprite = pair.Value ?? baseSlotSprite;
            }

            originalFrameSprites.Clear();
        }

        private bool HasJournalStructureChanged()
        {
            return GetChildCount(currentJournal.baseWeaponContainer) != lastBaseChildCount ||
                   GetChildCount(currentJournal.tier2WeaponContainer) != lastTier2ChildCount ||
                   GetChildCount(currentJournal.tier3WrapperContainer) != lastTier3ChildCount ||
                   GetFirstChildId(currentJournal.baseWeaponContainer) != lastBaseFirstId ||
                   GetFirstChildId(currentJournal.tier2WeaponContainer) != lastTier2FirstId ||
                   GetFirstChildId(currentJournal.tier3WrapperContainer) != lastTier3FirstId;
        }

        private void SaveCurrentStructure()
        {
            lastBaseChildCount = GetChildCount(currentJournal.baseWeaponContainer);
            lastTier2ChildCount = GetChildCount(currentJournal.tier2WeaponContainer);
            lastTier3ChildCount = GetChildCount(currentJournal.tier3WrapperContainer);
            lastBaseFirstId = GetFirstChildId(currentJournal.baseWeaponContainer);
            lastTier2FirstId = GetFirstChildId(currentJournal.tier2WeaponContainer);
            lastTier3FirstId = GetFirstChildId(currentJournal.tier3WrapperContainer);
            lastAppliedWeaponProgressVersion = weaponProgressVersion;
        }

        private static int GetChildCount(Transform container)
        {
            return container == null ? 0 : container.childCount;
        }

        private static int GetFirstChildId(Transform container)
        {
            return container == null || container.childCount == 0 ? -1 : container.GetChild(0).gameObject.GetInstanceID();
        }

        private void ResetStructureCache()
        {
            lastBaseChildCount = -1;
            lastTier2ChildCount = -1;
            lastTier3ChildCount = -1;
            lastBaseFirstId = -1;
            lastTier2FirstId = -1;
            lastTier3FirstId = -1;
            lastAppliedWeaponProgressVersion = -1;
        }
    }

    internal static class JournalWeaponOutlinePatches
    {
        [HarmonyPatch(typeof(UI_WeaponIcon), nameof(UI_WeaponIcon.SetWeapon), new[] { typeof(WeaponSimple) })]
        private static class SetWeaponSimple
        {
            [HarmonyPostfix]
            private static void After(UI_WeaponIcon __instance)
            {
                JournalWeaponOutline.ApplyToNewIcon(__instance);
            }
        }

        [HarmonyPatch(typeof(UI_WeaponIcon), nameof(UI_WeaponIcon.SetWeapon), new[] { typeof(WeaponEntity) })]
        private static class SetWeaponEntity
        {
            [HarmonyPostfix]
            private static void After(UI_WeaponIcon __instance)
            {
                JournalWeaponOutline.ApplyToNewIcon(__instance);
            }
        }
    }
}




