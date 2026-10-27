namespace AutoDuty.Helpers;

using Configurations;
using Dalamud.Plugin.Services;
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
    protected override string[] AddonsToClose { get; } = ["ShopExchangeCurrency", "SelectIconString", "SelectString", "Talk"];
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
    private bool submitted;
    private bool confirmed;
    private bool menuSelected;
    private bool travelling;
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
        interacted = submitted = confirmed = menuSelected = travelling = false;
        base.Start();
    }

    private void Fail(string message)
    {
        Svc.Chat.PrintError("[AutoDuty] 矿石购买失败，已停止挂机：" + message);
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

    protected override void HelperUpdate(IFramework framework)
    {
        if (Plugin.Stage == Stage.Paused) return;
        if (DateTime.UtcNow > deadline) { Fail("移动、菜单或交易超时；请检查解锁任务及背包。"); return; }
        if (DateTime.UtcNow < nextTick) return;
        nextTick = DateTime.UtcNow.AddMilliseconds(500);
        if (!PlayerHelper.IsValid || PlayerHelper.IsCasting || PlayerHelper.InCombat) return;
        if (InDungeon) { Fail("购买期间意外进入副本。"); return; }
        if (GotoHelper.State == ActionState.Running) return;
        if (phase == Phase.Travel)
        {
            if (!InVillage)
            {
                if (Svc.ClientState.TerritoryType != 1185)
                {
                    if (travelling) { Fail("未能到达图莱尤拉尔。"); return; }
                    travelling = true; GotoHelper.Invoke(1185); return;
                }
                travelling = false;
                var harbor = Position(1185, 17.5f, 12.2f);
                // The entrance is the only targetable aetherial current at this specific harbor location.
                var name = Svc.Data.GetExcelSheet<EObjName>().GetRow(2013421).Singular.ExtractText();
                var entrances = Svc.Objects.Where(o => o.IsTargetable && o.Name.TextValue == name
                    && Vector2.Distance(new(o.Position.X, o.Position.Z), new(harbor.X, harbor.Z)) < 12).ToArray();
                if (entrances.Length != 1)
                {
                    var floor = VNavmesh_IPCSubscriber.Query_Mesh_PointOnFloor(harbor, false, 5);
                    if (floor == null) { Fail("找不到港口入口地面，请先手动前往幻境村。"); return; }
                    if (MovementHelper.Move(floor.Value, 0.25f, 3)) Fail("找不到唯一的幻境村入口，请确认已解锁。");
                    return;
                }
                if (!MovementHelper.Move(entrances[0], 0.25f, 3)) return;
                if (Ready("SelectYesno", out var entryPrompt))
                {
                    var prompt = new AddonMaster.SelectYesno(entryPrompt) { RespectDisabledButtons = true };
                    string villageName = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(1278).PlaceName.Value.Name.ExtractText();
                    if (interacted && prompt.Text.Contains(villageName, StringComparison.Ordinal)) prompt.Yes();
                    else Fail("幻境村入口确认框不匹配。");
                    return;
                }
                ObjectHelper.InteractWithObject(entrances[0]); interacted = true; return;
            }
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
