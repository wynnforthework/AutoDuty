using System;
using System.Linq;
using AutoDuty.Helpers;
using ECommons.DalamudServices;

namespace AutoDuty;

public sealed partial class AutoDuty
{
    private readonly BossProgress bossProgress = new();
    private BossProgress.Key BossKey(PathAction step) => new(Svc.ClientState.TerritoryType, step.Note.Trim(), step.Position);

    internal bool BossAlreadyDefeated(PathAction step) => this.bossProgress.IsDefeated(this.BossKey(step));

    private void TickBossProgress()
    {
        if (!InDungeon) { this.bossProgress.Reset(); return; }
        if (!Player.Available || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return;
        if (this.Actions.Count == 0) return;
        var objects = Svc.Objects.OfType<Dalamud.Game.ClientState.Objects.Types.IBattleChara>().ToArray();
        var objectives = ObjectHelper.BossObjectives();
        foreach (var step in this.Actions.Where(a => a.Name == "Boss"))
        {
            var candidates = ObjectHelper.BossCandidatesForStep(step, objects);
            bool live = candidates.Any(o => o.CurrentHp > 0 && !o.IsDead);
            var names = ObjectHelper.BossNamesForStep(step);
            // An unnamed nearby corpse cannot be assigned to every Boss step in range.
            bool dead = candidates.Any(o => o.MaxHp > 0 && o.CurrentHp == 0 && o.IsDead
                && (names.Length > 0 || (ReferenceEquals(step, this.pathAction)
                    && this.DutyData?.bossObject?.EntityId == o.EntityId)));
            bool objective = objectives.Any(item => BossProgress.ObjectiveComplete(item.Text, item.Current, item.Needed, names));
            if (this.bossProgress.Observe(this.BossKey(step), live, dead, objective))
            {
                Svc.Log.Info($"[AutoDuty] Boss defeated: {step.Note} ({(objective ? "completed-objective" : "observed-death")})");
                NavigationDiagnostics.Record("boss-defeated", detail: new { step.Note, step.Position, dead, objective });
            }
        }
    }
}
