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
    private DateTime nextCityTeleport;
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
        nextCityTeleport = DateTime.MinValue;
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
        string destination = Svc.Data.GetExcelSheet<Aetheryte>().GetRow(239).AethernetName.Value.Name.ExtractText();
        string village = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(1278).PlaceName.Value.Name.ExtractText();
        if (string.IsNullOrWhiteSpace(destination) || string.IsNullOrWhiteSpace(village))
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
            Report("正在选择都市传送目的地：幻境村");
            if (State != ActionState.Running || villageTransferRequested) return;
            var reader = new ReaderTelepotTown(town);
            if (reader.NumEntries is 0 or > 20) { Fail("不支持的都市传送列表。"); return; }
            var names = reader.DestinationName;
            var entries = reader.DestinationData;
            int count = Math.Min((int)reader.NumEntries, Math.Min(names.Count, entries.Count));
            var matches = Enumerable.Range(0, count).Where(i => names[i].Name == destination).ToArray();
            if (matches.Length != 1)
            { Fail("大水晶都市传送列表中没有唯一的幻境村目的地；请确认已解锁。"); return; }
            villageTransferRequested = true;
            AddonHelper.FireCallBack(town, true, 11, entries[matches[0]].CallbackData);
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
        // Crystal centers can be above the floor or inside an unwalkable collision mesh.
        // Use horizontal edge distance; approach a reachable point outside its hitbox.
        Vector2 offset = new(Player.Position.X - crystal.Position.X, Player.Position.Z - crystal.Position.Z);
        float radius = Math.Max(0, crystal.HitboxRadius);
        if (offset.Length() > radius + 3)
        {
            Report("正在靠近大水晶（碰撞范围外）");
            if (State != ActionState.Running) return;
            Vector2 direction = Vector2.Normalize(offset);
            Vector3 edge = crystal.Position + new Vector3(direction.X, 0, direction.Y) * (radius + 2);
            edge.Y = Player.Position.Y;
            var floor = VNavmesh_IPCSubscriber.Nav_IsReady ? VNavmesh_IPCSubscriber.Query_Mesh_PointOnFloor(edge, false, 5) : null;
            if (floor.HasValue) MovementHelper.Move(floor.Value, 0.25f, 1);
            return;
        }
        Report("正在与图莱尤拉大水晶交互，等待菜单");
        if (State != ActionState.Running) return;
        if (VNavmesh_IPCSubscriber.IsEnabled) VNavmesh_IPCSubscriber.Path_Stop();
        if (FFXIVClientStructs.FFXIV.Client.Game.Conditions.Instance()->Mounted)
        {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9); return;
        }
        ObjectHelper.InteractWithObject(crystal); interacted = true;
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
