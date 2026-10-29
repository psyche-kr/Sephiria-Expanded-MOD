using System;
using System.Collections.Generic;

namespace SephiriaExpanded
{
    internal sealed class DamageMeterModel
    {
        private const float CombatTimeout = 5f;
        private const float BattleEndGrace = 1.5f;
        private static readonly Dictionary<string, double> NoSources = new Dictionary<string, double>();

        private readonly Dictionary<uint, double> damage = new Dictionary<uint, double>();
        private readonly Dictionary<uint, Dictionary<string, double>> sources = new Dictionary<uint, Dictionary<string, double>>();
        private readonly Dictionary<uint, double> best = new Dictionary<uint, double>();
        private readonly List<uint> keys = new List<uint>();
        private float closedSeconds;
        private float lastClosed;
        private bool sawGameBattle;
        private bool dummyVictim;
        private bool strongVictim;

        private bool IsDummy
        {
            get { return dummyVictim && !strongVictim; }
        }

        internal bool InCombat { get; private set; }
        internal float CombatStart { get; private set; }
        internal float LastHit { get; private set; }

        internal double Of(uint netId)
        {
            double value;
            return damage.TryGetValue(netId, out value) ? value : 0d;
        }

        internal double BestOf(uint netId)
        {
            double value;
            return best.TryGetValue(netId, out value) ? value : 0d;
        }

        internal Dictionary<string, double> SourcesOf(uint netId)
        {
            Dictionary<string, double> value;
            return sources.TryGetValue(netId, out value) ? value : NoSources;
        }

        internal float Seconds(float now)
        {
            if (CombatStart <= 0f) return 0f;

            return Math.Max(0f, (InCombat ? now : LastHit) - CombatStart);
        }

        internal float TotalSeconds(float now)
        {
            return closedSeconds + (InCombat ? Seconds(now) : 0f);
        }

        internal double PerSecond(uint netId, float now)
        {
            return Of(netId) / Math.Max(1f, Seconds(now));
        }

        internal void AddDamage(float now, uint netId, string source, double amount, bool gameInBattle)
        {
            if (amount <= 0d) return;

            if (!InCombat)
            {
                if (IsDummy && CombatStart > 0f && !gameInBattle && now - LastHit <= CombatTimeout * 2f)
                {
                    InCombat = true;
                    closedSeconds -= lastClosed;
                    lastClosed = 0f;
                }
                else
                {
                    if (CombatStart > 0f) ClearEncounter();

                    CombatStart = now;
                    InCombat = true;
                    sawGameBattle = gameInBattle;
                }
            }

            LastHit = now;
            damage[netId] = Of(netId) + amount;

            Dictionary<string, double> bySource;
            if (!sources.TryGetValue(netId, out bySource))
            {
                bySource = new Dictionary<string, double>();
                sources[netId] = bySource;
            }

            double current;
            bySource.TryGetValue(source, out current);
            bySource[source] = current + amount;
        }

        internal void Tick(float now, bool gameInBattle)
        {
            if (!InCombat) return;

            if (gameInBattle && !IsDummy) sawGameBattle = true;

            keys.Clear();
            keys.AddRange(damage.Keys);
            foreach (uint netId in keys)
            {
                double perSecond = PerSecond(netId, now);
                if (perSecond > BestOf(netId)) best[netId] = perSecond;
            }

            bool ended = sawGameBattle && !IsDummy ? !gameInBattle && now - LastHit > BattleEndGrace : now - LastHit > CombatTimeout;
            if (!ended) return;

            InCombat = false;
            lastClosed = Math.Max(0f, LastHit - CombatStart);
            closedSeconds += lastClosed;
        }

        internal void NoteVictim(bool dummy, bool strong)
        {
            if (dummy) dummyVictim = true;
            if (strong) strongVictim = true;
        }

        internal void Clear()
        {
            ClearEncounter();
            InCombat = false;
            CombatStart = 0f;
            LastHit = 0f;
            closedSeconds = 0f;
            lastClosed = 0f;
            sawGameBattle = false;
        }

        private void ClearEncounter()
        {
            damage.Clear();
            sources.Clear();
            best.Clear();
            dummyVictim = false;
            strongVictim = false;
        }
    }
}
