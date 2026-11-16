using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;

namespace AutoDuty.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Data;
    using IPC;
    using static Data.Classes;

    internal static class LevelingHelper
    {
        private static Content[] levelingDuties = [];

        internal static int PlanRevision { get; private set; }
        public static void ResetLevelingDuties()
        {
            levelingDuties = [];
            PlanRevision++;
        }

        public static readonly uint[] levelingList =
        [
            1036u, // 15 Sastasha
            1037u, // 16 TamTara Deepcroft
            1039u, // 24 The Thousand Maws of Toto-Rak
            1041u, // 32 Brayflox's Longstop
            1303u, // 38 Cutter's Cry
            1042u, // 41 Stone Vigil
            1330u, // 44 Dzemael Darkhold
            1331u, // 47 Aurum Vale

            1367u, // 63 Shisui of the Violet Tides
            1144u, // 67 Doma Castle
            1145u, // 69 Castrum Abania
            837u,  // 71 Holminster
            821u,  // 73 Dohn Mheg
            823u,  // 75 Qitana
            836u,  // 77 Malikah's Well
            822u,  // 79 Mt. Gulg
            952u,  // 81 Tower of Zot
            969u,  // 83 Tower of Babil
            970u,  // 85 Vanaspati,
            974u,  // 87 Ktisis Hyperboreia
            978u,  // 89 Aitiascope
            1167u, // 91 Ihuykatumu
            1193u, // 93 Worqor Zormor
            1194u, // 95 The Skydeep Cenote
            1198u, // 97 Vanguard
            1208u, // 99 Origenics
        ];

        public static readonly uint[] levelingListExperimental =
        [
            
        ];

        internal static Content[] LevelingDuties
        {
            get
            {
                if (levelingDuties.Length <= 0)
                {
                    IEnumerable<uint> ids = levelingList;

                    if (IPCSubscriber_Common.IsReady("SkipCutscene") || Skippy_IPCSubscriber.MSQSkipEnabled())
                    {
                        ids = ids.Concat([
                            1048u, // 50 Porta Decumana
                        ]);
                    } else
                    {
                        ids = ids.Concat([
                            1043u, // 50 Castrum Meridianum
                            1366u, // 51 Dusk Vigil
                            1064u, // 53 Sohm Al
                            1065u, // 55 The Aery
                            1066u, // 57 The Vault
                            1109u, // 59 The Great Gubal Library])
                            1142u, // 61 Sirensong Sea
                        ]);
                    }


                    if (Configuration.DutyConfig.LevelingListExperimentalEntries)
                        ids = ids.Concat(levelingListExperimental);

                    levelingDuties = [.. ids.Select(id => ContentHelper.DictionaryContent.GetValueOrDefault(id)).Where(c => c != null).Cast<Content>().OrderBy(x => x.ClassJobLevelRequired).ThenBy(x => x.ItemLevelRequired).ThenBy(x => x.ExVersion).ThenBy(x => x.DawnIndex)];
                }
                var config = Configuration.DutyConfig;
                return levelingDuties.Concat(config.LevelingIncludedDuties.Select(id => ContentHelper.DictionaryContent.GetValueOrDefault(id)).OfType<Content>())
                    .DistinctBy(c => c.TerritoryType).Where(c => IsAllowed(c.TerritoryType))
                    .OrderBy(c => c.ClassJobLevelRequired).ThenBy(c => c.ItemLevelRequired).ThenBy(c => c.ExVersion).ThenBy(c => c.DawnIndex).ToArray();
            }
        }

        // Game catalog is deliberately independent of both the default list and installed scripts.
        internal static Content[] Catalog
        {
            get
            {
                var planned = LevelingDuties.Select(c => c.TerritoryType).Concat(levelingList).ToHashSet();
                return ContentHelper.DictionaryContent.Values
                    .Where(c => c.ContentType == 2 || planned.Contains(c.TerritoryType))
                    .OrderBy(c => c.ClassJobLevelRequired).ThenBy(c => c.TerritoryType).ToArray();
            }
        }

        internal static bool IsPlanned(uint territory) => LevelingDuties.Any(c => c.TerritoryType == territory);

        internal static LevelingDutyRule Rule(uint territory) => Configuration.DutyConfig.LevelingDutyRules.GetValueOrDefault(territory) ?? new();

        internal static ContentPathsManager.DutyPath? SelectedRoute(uint territory)
        {
            if (!ContentPathsManager.DictionaryPaths.TryGetValue(territory, out var container) || container.Paths.Count == 0) return null;
            var path = container.SelectPath(out _);
            if (path?.PathFile?.Meta.Jobs is { } jobs && !jobs.HasJob(Player.Job))
            {
                bool explicitlySelected = Configuration.Meta.PathSelectionsByPath.TryGetValue(territory, out var selections)
                    && selections != null && selections.TryGetValue(path.FileName, out var selectedJobs) && selectedJobs.HasJob(Player.Job);
                if (!explicitlySelected) return null;
            }
            return path?.PathFile?.Actions.Count > 0 ? path : null;
        }

        internal static RouteMetricSummary? Evidence(ContentPathsManager.DutyPath path, DutyMode mode, int? level = null)
        {
            string hash = AutomationReports.Fingerprint(path.Actions);
            string runMode = mode + (QueueHelper.ShouldBeUnSynced() ? "/Unsynced" : "/Synced");
            return AutomationReports.Summarize(Configurations.ConfigurationMain.Instance.stats.routeRuns.Where(r =>
                r.TerritoryId == path.container.ID && r.RouteHash == hash && r.JobId == (uint)Player.Job
                && r.Mode == runMode && r.PartySize == Math.Max(1, Svc.Party.Length)
                && r.Multibox == Multibox.MultiboxUtility.Config.MultiBox && (!level.HasValue || r.StartingLevel == level)))
                .FirstOrDefault();
        }

        internal static Content? SelectForLevel(short level, DutyMode mode)
        {
            if (level < 15 || level >= 100 || mode is not (DutyMode.Support or DutyMode.Trust)) return null;
            var eligible = LevelingDuties.Where(c => c.DutyModes.HasFlag(mode)
                && c.CanRun(level, mode) && SelectedRoute(c.TerritoryType) != null).ToArray();
            var candidates = eligible.Select(c => new LevelingCandidate(c.TerritoryType, c.ClassJobLevelRequired));
            uint? selected = LevelingPlanRanking.Choose(candidates);
            return eligible.FirstOrDefault(c => c.TerritoryType == selected);
        }

        internal static unsafe string EligibilityDetail(Content duty, short level, DutyMode mode)
        {
            uint id = duty.TerritoryType;
            if (Configuration.DutyConfig.LevelingExcludedDuties.Contains(id)) return "manually-excluded";
            if (Configuration.DutyConfig.LevelingAutoExcluded.Contains(id)) return "auto-excluded-after-failures";
            if (!IsPlanned(id)) return "not-in-plan";
            if (!duty.DutyModes.HasFlag(mode)) return "unsupported-mode";
            if (duty.ClassJobLevelRequired > level) return $"requires-level-{duty.ClassJobLevelRequired}";
            if (!FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.IsInstanceContentUnlocked(duty.Id)) return "not-unlocked";
            if (SelectedRoute(id) == null) return "no-enabled-route-for-job";
            if (InventoryHelper.CurrentItemLevel < duty.ItemLevelRequired) return $"requires-ilvl-{duty.ItemLevelRequired}-current-{InventoryHelper.CurrentItemLevel}";
            return duty.CanRun(level, mode) ? "eligible" : "trust-or-entry-requirements-unmet";
        }

        internal static bool IsAllowed(uint territory)
        {
            var config = Configuration.DutyConfig;
            return !config.LevelingExcludedDuties.Contains(territory) && !config.LevelingAutoExcluded.Contains(territory)
                && (!config.LevelingIncludedOnly || config.LevelingIncludedDuties.Contains(territory));
        }

        internal static void RecordFailure(uint territory)
        {
            var config = Configuration.DutyConfig;
            int count = config.LevelingFailureCounts.GetValueOrDefault(territory) + 1;
            config.LevelingFailureCounts[territory] = count;
            if (config.LevelingAutoExcludeAfterFailures > 0 && count >= config.LevelingAutoExcludeAfterFailures && config.LevelingAutoExcluded.Add(territory))
            {
                Svc.Log.Warning($"Leveling duty {territory} auto-excluded after {count} failed runs");
                Svc.Chat.Print($"[AutoDuty] Leveling duty {territory} auto-excluded after {count} failed runs.");
            }
            ResetLevelingDuties();
            Configurations.ConfigurationProfileV2.Save();
        }

        internal static void RecordSuccess(uint territory)
        {
            var config = Configuration.DutyConfig;
            config.LevelingFailureCounts.Remove(territory);
            if (config.LevelingResetAutoExcludedOnSuccess) config.LevelingAutoExcluded.Remove(territory);
            ResetLevelingDuties();
        }

        internal static Content? SelectHighestLevelingRelevantDuty(LevelingMode mode)
        {
            Content? curContent = null;
            short lvl = PlayerHelper.GetCurrentLevelFromSheet();
            Svc.Log.Debug($"Leveling Mode: Searching for highest relevant leveling duty, Player Level: {lvl}");
            CombatRole combatRole = Player.Job.GetCombatRole();

            bool trust = mode.IsTrustLeveling();
            if (trust)
            {
                if (TrustHelper.Members.All(tm => !tm.Value.LevelIsSet))
                {
                    Svc.Log.Debug($"Leveling Mode: All trust members levels are not set, returning");
                    return null;
                }

                TrustMember?[] memberTest = new TrustMember?[3];

                switch (mode)
                {
                    case LevelingMode.Trust_Group:
                    {
                        foreach ((TrustMemberName _, TrustMember member) in TrustHelper.Members)
                        {
                            if (member.Level < lvl && member.Level < member.LevelCap && member.LevelIsSet && memberTest.CanSelectMember(member, combatRole))
                                lvl = (short)member.Level;
                            Svc.Log.Debug($"Leveling Mode: Checking {member.Name} level which is {member.Level}, lowest level is now {lvl}");
                        }

                        break;
                    }
                    case LevelingMode.Trust_Solo:
                    {
                        int memberIndex = 0;
                        foreach ((TrustMemberName _, TrustMember member) in TrustHelper.Members.OrderByDescending(tm => tm.Value.Level))
                        {
                            if (member.LevelIsSet && memberTest.CanSelectMember(member, combatRole)) 
                                memberTest[memberIndex++] = member;
                            Svc.Log.Debug($"Leveling Mode: Checking {member.Name} level which is {member.Level}");

                            if (memberIndex >= 3)
                            {
                                short newLvl = (short)(memberTest[2]?.Level ?? 0);

                                if (newLvl < lvl)
                                    lvl = newLvl;
                                break;
                            }
                        }

                        if (memberIndex < 3)
                        {
                            Svc.Log.Debug($"Leveling Mode: Not enough trust members available for solo leveling, returning");
                            return null;
                        }

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }
            }

            if ((lvl < 15 && !trust) || (trust && lvl < 71) || combatRole == CombatRole.NonCombat || lvl >= 100)
            {
                Svc.Log.Debug($"Leveling Mode: Lowest level is out of range (support<15 and trust<71) at {lvl} or we are not on a combat role {combatRole} or we (support) or we and all trust members are capped, returning");
                return null;
            }

            LevelingDuties.Each(x => Svc.Log.Debug($"Leveling Mode: Duties: {x.Name} CanRun: {x.CanRun(lvl)}{(trust ? $" | CanTrustRun : {x.CanTrustRun()}" : "")}"));
            curContent = SelectForLevel(lvl, trust ? DutyMode.Trust : Configuration.Meta.DutyModeEnum);

            var queueMode = trust ? DutyMode.Trust : Configuration.Meta.DutyModeEnum;
            Svc.Log.Info($"Leveling selection: job={Player.Job}, level={lvl}, ilvl={InventoryHelper.CurrentItemLevel}, mode={queueMode}, selected={curContent?.TerritoryType.ToString() ?? "none"}");
            foreach (var candidate in Catalog.Where(c => c.ClassJobLevelRequired <= lvl && c.ClassJobLevelRequired > (curContent?.ClassJobLevelRequired ?? 0)))
                Svc.Log.Info($"Leveling candidate {candidate.TerritoryType} ({candidate.Name}): {EligibilityDetail(candidate, lvl, queueMode)}");

            Svc.Log.Debug($"Leveling Mode: We found {curContent?.Name ?? "no duty"} to run");

            if (trust && curContent != null)
                if (!TrustHelper.SetLevelingTrustMembers(curContent, mode))
                {
                    Svc.Log.Debug($"Leveling Mode: We were unable to set our LevelingTrustMembers");
                    curContent = null;
                }

            return curContent;
        }
    }
}
