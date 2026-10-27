namespace AutoDuty.Configurations;

using System.Collections.Generic;
using System.Linq;

public sealed class MathematicsOreConfig
{
    public bool Enabled { get; set; }
    public List<uint> Ores { get; set; } = [46850];
    public int NextIndex { get; set; }
    public static readonly uint[] Allowed = [46850, 50058, 50977, 47750];
    public static readonly string[] Names = ["上弦月矿石", "下弦月矿石", "朔月矿石", "新月矿石"];
    public const uint Currency = 48;
    public const int Threshold = 1500;
    public const int Quantity = 3;
    public const int UnitCost = 500;

    public bool Valid => Ores is { Count: >= 1 and <= 4 } && Ores.Distinct().Count() == Ores.Count && Ores.All(Allowed.Contains);
    public bool ShouldBuy(int balance) => Enabled && Valid && balance > Threshold;
    public uint NextOre => Valid ? Ores[((NextIndex % Ores.Count) + Ores.Count) % Ores.Count] : 0;
    public static bool VerifyPurchase(int beforeItems, int afterItems, int beforeCurrency, int afterCurrency, int quantity)
        => quantity is 1 or Quantity && afterItems - beforeItems == quantity && beforeCurrency - afterCurrency == quantity * UnitCost;
    public bool CommitPurchase(int beforeItems, int afterItems, int beforeCurrency, int afterCurrency)
    {
        if (!Valid || !VerifyPurchase(beforeItems, afterItems, beforeCurrency, afterCurrency, Quantity)) return false;
        NextIndex = (((NextIndex % Ores.Count) + Ores.Count) % Ores.Count + 1) % Ores.Count;
        return true;
    }
}
