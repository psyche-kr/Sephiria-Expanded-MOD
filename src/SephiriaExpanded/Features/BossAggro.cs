using System;
using System.Collections.Generic;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class BossAggro
    {
        private const string MarkName = "SephiriaExpanded_BossAggro";
        private static readonly HashSet<uint> targets = new HashSet<uint>();
        private static readonly HashSet<uint> scannedTargets = new HashSet<uint>();
        private static readonly Dictionary<uint, AggroMark> marks = new Dictionary<uint, AggroMark>();
        private static readonly List<uint> staleTargets = new List<uint>();
        private static float nextScanTime;

        internal static void Tick()
        {
            if (!ModSettings.BossAggroEnabled)
            {
                ClearMarks();
                return;
            }

            if (Time.unscaledTime >= nextScanTime)
            {
                nextScanTime = Time.unscaledTime + 0.05f;
                ScanTargets();
            }

            Paint();
        }

        private static void ScanTargets()
        {
            scannedTargets.Clear();

            foreach (UnitAI_NewBasic ai in UnitAI_NewBasic.AllInstances)
            {
                if (ai == null)
                    continue;

                UnitAvatar boss = ai.Avatar;
                if (boss == null || boss.IsDead ||
                    (boss.monsterType != EMonsterType.Boss && boss.monsterType != EMonsterType.Miniboss))
                    continue;

                PlayerAvatar target = ai.CurrentTarget as PlayerAvatar;
                if (target == null || target.IsDead || target.netId == 0)
                    continue;

                if (ai.GetRelation(target) > ERelationBehaviour.Hostile)
                    continue;

                scannedTargets.Add(target.netId);
            }

            targets.Clear();
            foreach (uint id in scannedTargets)
                targets.Add(id);
        }

        private static void Paint()
        {
            staleTargets.Clear();
            foreach (KeyValuePair<uint, AggroMark> pair in marks)
            {
                if (!targets.Contains(pair.Key) || pair.Value == null)
                    staleTargets.Add(pair.Key);
            }

            foreach (uint id in staleTargets)
                RemoveMark(id);

            foreach (uint id in targets)
            {
                if (marks.TryGetValue(id, out AggroMark mark) && mark != null)
                    continue;

                AddMark(id);
            }
        }

        private static void ClearMarks()
        {
            if (marks.Count == 0)
                return;

            staleTargets.Clear();
            foreach (uint id in marks.Keys)
                staleTargets.Add(id);

            foreach (uint id in staleTargets)
                RemoveMark(id);
        }

        private static void AddMark(uint id)
        {
            UnitAvatar avatar = FindAvatar(id);
            TopdownActorRenderingMetadata actor = null;
            if (avatar != null)
                actor = avatar.TopdownActor;
            CombatManager manager = CombatManager.Instance;
            if (actor == null || actor.body == null || manager == null || manager.superArmorFxPrefab == null)
                return;

            Transform existing = actor.body.Find(MarkName);
            if (existing != null)
                UnityEngine.Object.Destroy(existing.gameObject);

            GameObject instance = UnityEngine.Object.Instantiate(manager.superArmorFxPrefab, actor.body);
            instance.name = MarkName;
            instance.transform.localPosition = Vector3.zero;

            AggroMark mark = instance.AddComponent<AggroMark>();
            mark.Bind(actor);
            marks[id] = mark;
        }

        private static void RemoveMark(uint id)
        {
            if (marks.TryGetValue(id, out AggroMark mark) && mark != null)
                UnityEngine.Object.Destroy(mark.gameObject);

            marks.Remove(id);
        }

        private static UnitAvatar FindAvatar(uint id)
        {
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = null;
                if (spawner != null)
                    avatar = spawner.PlayerAvatar;
                if (avatar != null && avatar.netId == id)
                    return avatar;
            }

            return null;
        }

        private sealed class AggroMark : MonoBehaviour
        {
            private static readonly int OutlineColor1 = Shader.PropertyToID("_OutlineColor1");
            private static readonly int OutlineColor2 = Shader.PropertyToID("_OutlineColor2");
            private static readonly int OutlineThickness = Shader.PropertyToID("_OutlineThickness");
            private static readonly Color AggroColor = new Color32(253, 249, 226, 255);

            private static Color GetAggroColor()
            {
                return AggroColor;
            }

            private SpriteRenderer[] layers;
            private SpriteRenderer source;
            private MaterialPropertyBlock propertyBlock;

            internal void Bind(TopdownActorRenderingMetadata actor)
            {
                layers = GetComponentsInChildren<SpriteRenderer>(true);
                propertyBlock = new MaterialPropertyBlock();

                foreach (SpriteRenderer layer in layers)
                {
                    if (layer != null)
                        layer.sortingOrder = -2;
                }

                source = FindSource(actor);
            }

            private static SpriteRenderer FindSource(TopdownActorRenderingMetadata actor)
            {
                if (actor.bodyRenderer != null)
                    return actor.bodyRenderer;

                foreach (SpriteRenderer renderer in actor.body.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer != null)
                        return renderer;
                }

                return null;
            }

            private void LateUpdate()
            {
                if (layers == null || source == null)
                    return;

                Sprite sprite = source.sprite;
                bool visible = sprite != null && source.enabled && source.gameObject.activeInHierarchy && source.color.a > 0.02f;

                foreach (SpriteRenderer layer in layers)
                {
                    if (layer == null)
                        continue;

                    layer.enabled = visible;
                    if (!visible)
                        continue;

                    layer.sprite = sprite;
                    layer.GetPropertyBlock(propertyBlock);
                    Color color = GetAggroColor();
                    propertyBlock.SetColor(OutlineColor1, color);
                    propertyBlock.SetColor(OutlineColor2, color);
                    propertyBlock.SetFloat(OutlineThickness, 1.5f);
                    layer.SetPropertyBlock(propertyBlock);
                }
            }
        }
    }
}




