namespace AutoDuty.Helpers;

using Configurations;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using ECommons;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using IPC;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Numerics;

public sealed class MathematicsOreLoopConfig : ActiveLoopActionConfig<MathematicsOreHelper, MathematicsOreLoopConfig>
{
    public override string OverlayName => "Mathematics ore";
    public override void OnGuiSettings() { }
}

public sealed unsafe class MathematicsOreHelper : ActiveHelperBase<MathematicsOreHelper, MathematicsOreLoopConfig>
{
    public override string Name => nameof(MathematicsOreHelper);
    public override string DisplayName => "数理神典石购买矿石";
    protected override int TimeOut { get; set; } = 0; // Own watchdog fails the whole loop instead of silently requeueing.
    protected override string[] AddonsToClose { get; } = ["ShopExchangeCurrency", "SelectIconString", "SelectString", "Talk", "TelepotTown"];
    private enum Phase { Travel, Shop, Confirm, Verify }
    internal enum TestMode { None, VisitShop, BuyOne }
    private TestMode testMode;
    private int PurchaseQuantity => testMode == TestMode.BuyOne ? 1 : MathematicsOreConfig.Quantity;
    private Phase phase;
    private DateTime deadline;
    private DateTime nextTick;
    private uint ore;
    private int beforeItems;
    private int beforeCurrency;
    private bool interacted;
    private bool villageTransferRequested;
    private bool submitted;
    private bool confirmed;
    private bool menuSelected;
    private int nativeTransferAttempts;
    private DateTime nativeListReadyAt;
    private DateTime nextNativeTransfer;
    private DateTime nextCityTeleport;
    private DateTime crystalProbeUntil;
    private DateTime nextCrystalInteraction;
    private DateTime nextCrystalDiagnostic;
    private DateTime statusSince;
    internal static string Progress { get; private set; } = "未启动";

    private void Report(string status)
    {
        Plugin.action = status;
        if (Progress == status)
        {
            if (DateTime.UtcNow - statusSince > TimeSpan.FromSeconds(30)) Fail("步骤超过 30 秒未完成：" + status);
            return;
        }
        Progress = status;
        statusSince = DateTime.UtcNow;
        Svc.Chat.Print("[AutoDuty] " + status);
        Svc.Log.Info("MathematicsOre: " + status);
    }
    private MathematicsOreConfig config = null!;
    private const uint Npc = 1053904;
    private static bool InVillage => Svc.ClientState.TerritoryType is 1269 or 1278;

    internal static void EnqueueCheck()
    {
        Plugin.taskManager.EnqueueDelay(1000); // Let reward currency settle after the ready gate.
        Plugin.taskManager.Enqueue(() =>
        {
            var config = Configuration.MathematicsOre;
            if (config.Enabled && !config.Valid)
            { Svc.Chat.PrintError("[AutoDuty] 矿石列表无效，已停止挂机。"); Plugin.Stage = Stage.Stopped; return; }
            // Read the wallet here, after the client has settled outside the duty.
            if (config.ShouldBuy(InventoryHelper.ItemCount(MathematicsOreConfig.Currency))) Invoke();
        }, "Mathematics-WalletCheck");
        Plugin.taskManager.Enqueue(() => State != ActionState.Running, "Mathematics-Wait", new ECommons.Automation.NeoTaskManager.TaskManagerConfiguration(int.MaxValue));
        Plugin.taskManager.Enqueue(() => PlayerHelper.IsReadyFull, "Mathematics-Ready");
    }

    internal override void Start()
    {
        if (State == ActionState.Running) return;
        config = Configuration.MathematicsOre;
        if (!config.ShouldBuy(InventoryHelper.ItemCount(MathematicsOreConfig.Currency))) return;
        if (InDungeon) return;
        Begin(TestMode.None);
    }

    internal static void Test(TestMode mode)
    {
        if (mode == TestMode.None || State == ActionState.Running || Plugin.Stage != Stage.Stopped
            || Plugin.States != PluginState.None || ActiveHelper.AnyHelperRunning()
            || InDungeon || !PlayerHelper.IsReadyFull || !Configuration.MathematicsOre.Valid) return;
        if (mode == TestMode.BuyOne && InventoryHelper.ItemCount(MathematicsOreConfig.Currency) < MathematicsOreConfig.UnitCost)
        { Svc.Chat.PrintError("[AutoDuty] 试买 1 个需要至少 500 数理神典石；可使用仅打开商店测试。"); return; }
        Instance.config = Configuration.MathematicsOre;
        Instance.Begin(mode);
    }

    private void Begin(TestMode mode)
    {
        testMode = mode;
        phase = Phase.Travel; deadline = DateTime.UtcNow.AddMinutes(5); nextTick = DateTime.MinValue;
        ore = config.NextOre;
        interacted = submitted = confirmed = menuSelected = villageTransferRequested = false;
        nativeTransferAttempts = 0;
        nativeListReadyAt = nextNativeTransfer = DateTime.MinValue;
        nextCityTeleport = DateTime.MinValue;
        crystalProbeUntil = nextCrystalInteraction = nextCrystalDiagnostic = DateTime.MinValue;
        Progress = "准备出发"; statusSince = DateTime.UtcNow;
        base.Start();
    }

    private void Fail(string message)
    {
        Svc.Chat.PrintError("[AutoDuty] 矿石购买失败，已停止挂机：" + message);
        Progress = "已停止：" + message;
        Svc.Log.Error(message);
        Plugin.Stage = Stage.Stopped; // Aborts queued tasks and stops every active helper.
    }

    internal override void Stop()
    {
        GotoHelper.ForceStop();
        if (VNavmesh_IPCSubscriber.IsEnabled) VNavmesh_IPCSubscriber.Path_Stop();
        base.Stop();
    }

    private static bool Ready(string name, out AtkUnitBase* addon)
        => GenericHelpers.TryGetAddonByName(name, out addon) && GenericHelpers.IsAddonReady(addon);

    // Map coordinates are converted using the live map scale/offset, not treated as world coordinates.
    private static Vector3 Position(uint territory, float x, float y)
    {
        var map = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territory).Map.Value;
        return new((x - 1) * 2048 / 41 - 1024f / (map.SizeFactor / 100f) - map.OffsetX,
            0, (y - 1) * 2048 / 41 - 1024f / (map.SizeFactor / 100f) - map.OffsetY);
    }

    private void TravelFromCityCrystal()
    {
        // Aetheryte 239 is the invisible Phantom Village destination in group 14.
        string village = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(1278).PlaceName.Value.Name.ExtractText();
        if (string.IsNullOrWhiteSpace(village))
        { Fail("缺少幻境村传送目的地数据。"); return; }
        // Handle menus before movement: the crystal can be untargetable while its menu is open.
        if (Ready("SelectYesno", out var confirmation))
        {
            Report("正在确认进入幻境村");
            if (State != ActionState.Running) return;
            var prompt = new AddonMaster.SelectYesno(confirmation) { RespectDisabledButtons = true };
            if (interacted && villageTransferRequested && prompt.Text.Contains(village, StringComparison.Ordinal)) prompt.Yes();
            else Fail("大水晶传送确认框不匹配：" + prompt.Text);
            return;
        }
        if (Ready("TelepotTown", out var town))
        {
            if (!interacted) { Fail("测试前已有都市传送窗口，请关闭后重试。"); return; }
            Report(villageTransferRequested ? "已请求幻境村传送，等待确认或切换地图" : "正在选择都市传送目的地：幻境村");
            if (State != ActionState.Running) return;
            // Let the list settle, then allow one delayed second activation (selection/confirmation).
            DateTime selectionTime = DateTime.UtcNow;
            if (nativeListReadyAt == DateTime.MinValue) nativeListReadyAt = selectionTime.AddSeconds(1);
            if (selectionTime < nativeListReadyAt || selectionTime < nextNativeTransfer || nativeTransferAttempts >= 2) return;
            // Use the native agent, not the legacy AtkValue layout (slot 0 is Bool on CN).
            var agent = AgentTelepotTown.Instance();
            if (agent == null || agent->Data == null)
            { Report("等待都市传送数据加载"); return; }
            var data = agent->Data;
            if (data->AetheryteCount == 0) return;
            if (data->AetheryteCount > data->Entries.Length)
            { Fail("都市传送目的地数量超出原生列表范围。"); return; }
            int selected = -1;
            for (int i = 0; i < data->AetheryteCount; i++)
            {
                var entry = data->Entries[i];
                Svc.Log.Info($"MathematicsOre aethernet: index={i}, id={entry.AetheryteId}, territory={entry.TerritoryTypeId}, locked={entry.IsLocked}, unusable={entry.IsUnusable}, current={entry.IsCurrent}");
                if (entry.AetheryteId != 239) continue;
                if (selected != -1) { Fail("都市传送中出现重复的幻境村目的地。"); return; }
                // Regional client layouts/state transitions can disagree with these flags.
                // Log them, but let the normal game interaction enforce unlock/availability rules.
                selected = i;
            }
            if (selected < 0)
            { Fail("都市传送原生列表中找不到幻境村（Aetheryte 239）；请检查解锁条件。"); return; }
            if (data->CurrentAetheryte == selected)
            { Fail("原生传送列表将幻境村识别为当前水晶，数据不匹配；停止传送。"); return; }
            villageTransferRequested = true;
            nativeTransferAttempts++;
            nextNativeTransfer = selectionTime.AddSeconds(2);
            Svc.Log.Info($"MathematicsOre: native TeleportToAetheryte index={selected}, destination=239, attempt={nativeTransferAttempts}/2");
            agent->TeleportToAetheryte((byte)selected);
            return;
        }
        if (Ready("SelectString", out var selection))
        {
            if (!interacted) { Fail("测试前已有菜单打开，请关闭后重试。"); return; }
            Report("正在选择大水晶菜单");
            if (State != ActionState.Running || villageTransferRequested) return;
            var entries = new AddonMaster.SelectString(selection).Entries;
            var direct = entries.Where(e => e.Text.Contains(village, StringComparison.Ordinal)).ToArray();
            if (direct.Length == 1)
            { villageTransferRequested = true; direct[0].Select(); return; }
            var aethernet = entries.Where(e => e.Text.Contains("Aethernet", StringComparison.OrdinalIgnoreCase)
                || e.Text.Contains("都市传送", StringComparison.Ordinal) || e.Text.Contains("都市傳送", StringComparison.Ordinal)
                || e.Text.Contains("以太之网", StringComparison.Ordinal) || e.Text.Contains("以太之網", StringComparison.Ordinal)).ToArray();
            if (aethernet.Length == 1) { aethernet[0].Select(); return; }
            Fail("无法识别大水晶菜单：" + string.Join(" | ", entries.Select(e => e.Text))); return;
        }
        if (villageTransferRequested) { Report("已选择幻境村，等待切换地图"); return; }
        if (!PlayerHelper.IsReadyFull || PlayerHelper.IsCasting || PlayerHelper.InCombat)
        { Report("等待角色恢复可交互状态"); return; }
        // Aetheryte object DataId is the Aetheryte sheet ID, not a Level row ID.
        // Do not dereference exported Level references: these differ across client data versions.
        var crystal = Svc.Objects.FirstOrDefault(o => o.ObjectKind == ObjectKind.Aetheryte
            && o.BaseId == 216 && o.IsTargetable);
        if (crystal == null)
        {
            Report("等待图莱尤拉大水晶对象加载（DataId=216）");
            return;
        }
        // The game's aetheryte interaction range need not equal the collision radius.
        // First try the actual interaction at the teleport landing spot, before requiring a mesh.
        DateTime now = DateTime.UtcNow;
        if (crystalProbeUntil == DateTime.MinValue) crystalProbeUntil = now.AddSeconds(4);
        Vector2 offset = new(Player.Position.X - crystal.Position.X, Player.Position.Z - crystal.Position.Z);
        float distance = offset.Length();
        float radius = Math.Max(0, crystal.HitboxRadius);
        bool probing = now < crystalProbeUntil;
        if (now >= nextCrystalDiagnostic)
        {
            nextCrystalDiagnostic = now.AddSeconds(5);
            Svc.Log.Info($"MathematicsOre crystal: player={Player.Position}, crystal={crystal.Position}, horizontal={distance:F2}, radius={radius:F2}, vnav={VNavmesh_IPCSubscriber.IsEnabled}");
        }
        if (FFXIVClientStructs.FFXIV.Client.Game.Conditions.Instance()->Mounted)
        {
            Report("正在下坐骑以交互大水晶");
            if (State == ActionState.Running) ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
            return;
        }
        if (probing || distance <= radius + 3)
        {
            Report("正在尝试大水晶交互，等待菜单");
            if (State != ActionState.Running) return;
            if (VNavmesh_IPCSubscriber.IsEnabled) VNavmesh_IPCSubscriber.Path_Stop();
        }
        else
        {
            Report("正在靠近大水晶（同时尝试交互）");
            if (State != ActionState.Running) return;
            if (!VNavmesh_IPCSubscriber.IsEnabled) { Fail("需要靠近大水晶，但 vnavmesh 未启用。"); return; }
            if (!VNavmesh_IPCSubscriber.Nav_IsReady)
            {
                if (now >= nextCrystalInteraction) Svc.Log.Info("MathematicsOre crystal: waiting for navmesh to become ready");
            }
            else
            {
                Vector2 direction = Vector2.Normalize(offset);
                Vector3 edge = crystal.Position + new Vector3(direction.X, 0, direction.Y) * (radius + 2);
                edge.Y = Player.Position.Y;
                var floor = VNavmesh_IPCSubscriber.Query_Mesh_PointOnFloor(edge, false, 5);
                // Outside projection can fail at the raised aetheryte plinth. Try the landing-side midpoint.
                if (!floor.HasValue)
                {
                    Vector3 midpoint = Vector3.Lerp(Player.Position, edge, 0.5f);
                    floor = VNavmesh_IPCSubscriber.Query_Mesh_PointOnFloor(midpoint, false, 5);
                }
                if (!floor.HasValue)
                { Fail($"大水晶附近找不到可寻路地面：player={Player.Position}, crystal={crystal.Position}, edge={edge}"); return; }
                if (now >= nextCrystalInteraction)
                    Svc.Log.Info($"MathematicsOre crystal: approach={floor.Value}, pathRunning={VNavmesh_IPCSubscriber.Path_IsRunning}, pathfinding={VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress}");
                MovementHelper.Move(floor.Value, 0.25f, 1);
            }
        }
        // Bounded by the step watchdog. A failed interaction is no longer gated on Move() completing.
        if (distance <= 25 && now >= nextCrystalInteraction)
        {
            nextCrystalInteraction = now.AddSeconds(2);
            ObjectHelper.InteractWithObject(crystal); interacted = true;
        }

    }

    protected override void HelperUpdate(IFramework framework)
    {
        if (State != ActionState.Running) return;
        try
        {
            UpdateCore(framework);
        }
        catch (Exception ex)
        {
            // Stop immediately so a bad sheet or addon layout cannot throw on every frame.
            Svc.Log.Error($"MathematicsOre exception: phase={phase}, territory={Svc.ClientState.TerritoryType}, progress={Progress}\n{ex}");
            Fail($"{Progress}：{ex.GetType().Name}: {ex.Message}");
        }
    }

    private void UpdateCore(IFramework framework)
    {
        if (Plugin.Stage == Stage.Paused) return;
        if (DateTime.UtcNow > deadline) { Fail("移动、菜单或交易超时；请检查解锁任务及背包。"); return; }
        if (DateTime.UtcNow < nextTick) return;
        nextTick = DateTime.UtcNow.AddMilliseconds(500);
        if (InDungeon) { Fail("购买期间意外进入副本。"); return; }
        // Process crystal menus without the generic readiness gate (menus can occupy the player).
        if (phase == Phase.Travel && !InVillage)
        {
            if (!Player.Available || Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51]) return;
            if (Svc.ClientState.TerritoryType == 1185)
            {
                TravelFromCityCrystal(); return;
            }
            Report("正在传送至图莱尤拉大水晶");
            if (State != ActionState.Running || !PlayerHelper.IsReadyFull || PlayerHelper.IsCasting || PlayerHelper.InCombat) return;
            if (DateTime.UtcNow >= nextCityTeleport)
            {
                nextCityTeleport = DateTime.UtcNow.AddSeconds(10);
                if (!TeleportHelper.TeleportAction(216)) Fail("无法发起图莱尤拉传送，请检查水晶解锁及传送条件。");
            }
            return;
        }
        if (!PlayerHelper.IsValid || PlayerHelper.IsCasting || PlayerHelper.InCombat) return;
        if (GotoHelper.State == ActionState.Running) return;
        if (phase == Phase.Travel)
        {
            interacted = false;
            var npc = ObjectHelper.GetObjectByDataId(Npc);
            if (npc == null)
            {
                var source = Position(Svc.ClientState.TerritoryType, 6.9f, 7.3f);
                var floor = VNavmesh_IPCSubscriber.Query_Mesh_PointOnFloor(source, false, 5);
                if (floor == null) { Fail("找不到尔密娜附近地面。"); return; }
                if (MovementHelper.Move(floor.Value, 0.25f, 3)) Fail("没有找到尔密娜；请确认幻境武器相关任务已解锁。");
                return;
            }
            if (!MovementHelper.Move(npc, 0.25f, 3)) return;
            if (Ready("SelectYesno", out _) || Ready("ShopExchangeCurrency", out _) || Ready("SelectIconString", out _))
            { Fail("开始交互前已有菜单打开，请关闭后重试。"); return; }
            phase = Phase.Shop; deadline = DateTime.UtcNow.AddSeconds(30);
            ObjectHelper.InteractWithObject(npc); interacted = true; return;
        }
        if (!InVillage || ObjectHelper.GetObjectByDataId(Npc) is not { } merchant || ObjectHelper.GetDistanceToPlayer(merchant) > 5)
        { Fail("已离开尔密娜附近。"); return; }
        if (phase == Phase.Shop)
        {
            if (Ready("SelectIconString", out var menu))
            {
                if (menuSelected) return;
                string shopName = Svc.Data.GetExcelSheet<SpecialShop>().GetRow(1770925).Name.ExtractText();
                var entries = new AddonMaster.SelectIconString(menu).Entries.Where(e => e.Text.Contains(shopName, StringComparison.Ordinal)).ToArray();
                if (entries.Length != 1) { Fail("未找到幻境武器素材菜单。"); return; }
                entries[0].Select(); menuSelected = true; return;
            }
            if (!Ready("ShopExchangeCurrency", out var shop))
            {
                if (Ready("Talk", out var talk)) new AddonMaster.Talk(talk).Click();
                return;
            }
            // Guard the ECommons basic shop layout before reading its arrays.
            if (shop->AtkValuesCount < 1314 || shop->AtkValues[4].UInt is 0 or > 60
                || shop->AtkValuesCount < 1310 + shop->AtkValues[4].UInt)
            { Fail("不支持的货币商店布局。"); return; }
            var master = new AddonMaster.ShopExchangeCurrency(shop);
            var item = master.BasicShopItems.SingleOrDefault(i => i.ItemId == ore);
            if (item == null || master.CurrencyId != MathematicsOreConfig.Currency || item.CostAmount != MathematicsOreConfig.UnitCost)
            { Fail("物品、币种或单价不匹配；不执行购买。"); return; }
            if (testMode == TestMode.VisitShop)
            {
                Svc.Chat.Print("[AutoDuty] 测试完成：已到达尔密娜并验证矿石、币种及单价；未购买。测试结束后关闭商店。");
                this.Stop(); return;
            }
            if (Ready("SelectYesno", out _) || InventoryHelper.SlotsFree == 0 || InventoryHelper.ItemCount(ore) > 999 - PurchaseQuantity)
            { Fail("已有确认框或背包空间不足。"); return; }
            beforeItems = InventoryHelper.ItemCount(ore); beforeCurrency = InventoryHelper.ItemCount(MathematicsOreConfig.Currency);
            if (testMode == TestMode.None ? beforeCurrency <= MathematicsOreConfig.Threshold : beforeCurrency < PurchaseQuantity * MathematicsOreConfig.UnitCost) { Fail("交易前币量已变化。"); return; }
            submitted = true; phase = Phase.Confirm; deadline = DateTime.UtcNow.AddSeconds(15);
            item.Select(PurchaseQuantity); return;
        }
        if (phase == Phase.Confirm)
        {
            if (Ready("SelectYesno", out var yesno))
            {
                var dialog = new AddonMaster.SelectYesno(yesno) { RespectDisabledButtons = true };
                string itemName = Svc.Data.GetExcelSheet<Item>().GetRow(ore).Name.ExtractText();
                if (!submitted || confirmed || !dialog.Text.Contains(itemName, StringComparison.Ordinal))
                { Fail("购买确认框不匹配。"); return; }
                confirmed = true; phase = Phase.Verify; dialog.Yes(); return;
            }
            if (InventoryHelper.ItemCount(ore) != beforeItems) phase = Phase.Verify;
        }
        if (phase == Phase.Verify)
        {
            int items = InventoryHelper.ItemCount(ore), currency = InventoryHelper.ItemCount(MathematicsOreConfig.Currency);
            if (items == beforeItems || currency == beforeCurrency) return;
            if (testMode != TestMode.None)
            {
                if (!MathematicsOreConfig.VerifyPurchase(beforeItems, items, beforeCurrency, currency, PurchaseQuantity))
                { Fail("测试交易实收物品或扣币数量不匹配，请人工检查。"); return; }
                Svc.Chat.Print("[AutoDuty] 试买完成：矿石 ×1，花费 500 数理神典石。轮换顺序未改变，不启动副本挂机。");
                this.Stop(); return;
            }
            if (!config.CommitPurchase(beforeItems, items, beforeCurrency, currency))
            { Fail("实收物品或扣币数量不是 3 个 / 1500，请人工检查。"); return; }
            ConfigurationProfileV2.Save();
            Svc.Chat.Print($"[AutoDuty] 已购买 {Svc.Data.GetExcelSheet<Item>().GetRow(ore).Name.ExtractText()} ×3，继续挂机。");
            this.Stop();
        }
    }
}
