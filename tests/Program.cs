using AutoDuty.Configurations;
int checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
var config = new MathematicsOreConfig();
Check(!config.ShouldBuy(2000), "Default OFF");
config.Enabled = true;
Check(config.Valid && config.NextOre == 46850, "Default one ore");
Check(!config.ShouldBuy(1500), "Strictly greater than threshold");
Check(config.ShouldBuy(1501), "1501 triggers");
Check(!config.ShouldBuy(0), "Empty wallet");
Check(!config.CommitPurchase(10, 10, 2000, 2000) && config.NextIndex == 0, "No completion cannot advance");
Check(!config.CommitPurchase(10, 13, 2000, 1000), "Wrong currency delta cannot advance");
Check(!config.CommitPurchase(10, 12, 2000, 500), "Partial quantity cannot advance");
Check(config.CommitPurchase(10, 13, 2000, 500) && config.NextIndex == 0, "Single selection wraps");
config.Ores = [46850, 50058, 50977, 47750];
foreach (uint id in new uint[] { 46850, 50058, 50977, 47750, 46850 })
{
 Check(config.NextOre == id, "Ordered rotation " + id);
 Check(config.CommitPurchase(0, 3, 1600, 100), "Verified purchase advances");
}
int saved = config.NextIndex;
Check(!config.CommitPurchase(3, 6, 1600, 1599) && config.NextIndex == saved, "Failed purchase preserves pointer");
config.Ores = [];
Check(!config.Valid && !config.ShouldBuy(2000) && config.NextOre == 0, "Zero choices invalid");
config.Ores = [46850, 46850];
Check(!config.Valid, "Duplicates invalid");
config.Ores = [123];
Check(!config.Valid, "Other items forbidden");
config.Ores = [46850, 50058]; config.NextIndex = -1;
Check(config.NextOre == 50058, "Negative saved cursor normalized");
config.NextIndex = 999;
Check(config.NextOre == 50058, "Large saved cursor normalized");
Check(config.CommitPurchase(0, 3, 2000, 500) && config.NextOre == 46850, "Two-item sequence wraps");
Console.WriteLine($"PASS: {checks} ore policy checks");

Check(MathematicsOreConfig.VerifyPurchase(0, 1, 500, 0, 1), "One-item test accepts exact deltas");
Check(!MathematicsOreConfig.VerifyPurchase(0, 3, 1500, 0, 1), "One-item test rejects three items");
Check(!MathematicsOreConfig.VerifyPurchase(0, 1, 500, 1, 1), "One-item test rejects wrong charge");
Check(!MathematicsOreConfig.VerifyPurchase(0, 0, 500, 0, 1), "One-item test rejects missing item");
Check(!MathematicsOreConfig.VerifyPurchase(0, 0, 0, 0, 0), "Zero quantity cannot verify purchase");
Console.WriteLine($"PASS: {checks} total ore policy checks including test purchases");
