using System;
using System.Linq;
using AutoDuty.Helpers;
using ECommons.DalamudServices;

namespace AutoDuty;

public sealed partial class AutoDuty
{
    private readonly BossProgress bossProgress = new();
    internal DutyDataTemporary EnsureDutyContext()
    {
        if (this.DutyData != null) return this.DutyData;
        Svc.Log.Warning($"[AutoDuty] Restoring missing duty context at step {this.indexer}; preserving the current route and attempt.");
        this.DutyData = new DutyDataTemporary();
        NavigationDiagnostics.Record("duty-context-restored");
        return this.DutyData;
    }
    private BossProgress.Key BossKey(PathAction step) => new(Svc.ClientState.TerritoryType, step.Note.Trim(), step.Position);

    internal bool BossAlreadyDefeated(PathAction step) => this.bossProgress.IsDefeated(this.BossKey(step))
        || (this.IsCompletedBossRoute() && ReferenceEquals(step, this.Actions[this.indexer]));

    internal bool IsTrackedBoss(PathAction step, uint entityId, uint baseId)
        => this.bossProgress.Matches(this.BossKey(step), entityId, baseId);

    private void TickBossProgress()
    {
        if (!InDungeon) { this.bossProgress.Reset(); return; }
        if (!Player.Available || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return;
        if (this.Actions.Count == 0) return;
        var objects = Svc.Objects.OfType<Dalamud.Game.ClientState.Objects.Types.IBattleChara>().ToArray();
        var objectives = ObjectHelper.BossObjectives();
        var steps = this.Actions.Select((action, index) => (action, index)).Where(x => x.action.Name == "Boss").ToArray();
        foreach (var (step, _) in steps)
        {
            var candidates = ObjectHelper.BossCandidatesForStep(step, objects);
            var key = this.BossKey(step);
            foreach (var boss in candidates.Where(o => o.CurrentHp > 0 && !o.IsDead
                && BossIdentity.OwnedBy(step, steps.Select(x => x.action), o.Position,
                    a => a.Position == System.Numerics.Vector3.Zero ? Player.Position : a.Position)))
                this.bossProgress.Remember(key, boss.EntityId, boss.BaseId, boss.Name.TextValue);
            // Track primitive identity, not a nullable DutyData wrapper. A corpse
            // may lose its boss rank/module, and unnamed legacy steps have no note.
            var tracked = objects.Where(o => this.bossProgress.Matches(key, o.EntityId, o.BaseId)).ToArray();
            bool live = tracked.Any(o => o.CurrentHp > 0 && !o.IsDead);
            // Only a name read from an already identified live entity may correlate
            // a localized objective. Route notes never identify an entity or a kill.
            var names = this.bossProgress.Names(key).ToArray();
            // An unnamed nearby corpse cannot be assigned to every Boss step in range.
            bool dead = tracked.Any(o => o.MaxHp > 0 && o.CurrentHp == 0 && o.IsDead);
            bool objective = objectives.Any(item => BossProgress.ObjectiveComplete(item.Text, item.Current, item.Needed, names));
            if (this.bossProgress.Observe(key, live, dead, objective))
            {
                Svc.Log.Info($"[AutoDuty] Boss defeated: {step.Note} ({(objective ? "completed-objective" : "observed-death")})");
                NavigationDiagnostics.Record("boss-defeated", detail: new { step.Note, step.Position, dead, objective });
            }
        }
    }
}
