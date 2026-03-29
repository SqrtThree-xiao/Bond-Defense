using Godot;
using System.Collections.Generic;

/// <summary>
/// 英雄配置数据（对应 tables/data/hero.xlsx）
/// </summary>
public class HeroConfig
{
    public int    Id           { get; set; }
    public string Name         { get; set; } = "";
    public string Rarity       { get; set; } = "common";   // common / rare / epic
    public int    Cost         { get; set; }
    public int    Attack       { get; set; }
    public float  AttackSpeed  { get; set; }
    public float  Range        { get; set; }
    public int    Hp           { get; set; }
    public List<string> Tags   { get; set; } = new();
    public string Color        { get; set; } = "ffffff";
}

/// <summary>
/// 羁绊配置数据（对应 tables/data/synergy.xlsx）
/// </summary>
public class SynergyConfig
{
    public int    Id           { get; set; }
    public string Name         { get; set; } = "";
    public string Tag          { get; set; } = "";
    public int    Tier1        { get; set; }
    public int    Tier2        { get; set; }
    public List<string> Descriptions { get; set; } = new();
    public List<string> AtkBonus     { get; set; } = new();
    public List<string> SpdBonus     { get; set; } = new();
    public List<string> RangeBonus   { get; set; } = new();
}

/// <summary>
/// 波次配置数据（对应 tables/data/wave.xlsx）
/// </summary>
public class WaveConfig
{
    public int   Id            { get; set; }
    public int   EnemyCount    { get; set; }
    public int   BaseHp        { get; set; }
    public float Speed         { get; set; }
    public float SpawnInterval { get; set; }
    public int   KillReward    { get; set; }
    public int   WaveReward    { get; set; }
}

/// <summary>
/// 敌人配置数据（对应 tables/data/enemy.xlsx）
/// </summary>
public class EnemyConfig
{
    public int    Id           { get; set; }
    public string Name         { get; set; } = "";
    public int    Hp           { get; set; }
    public float  Speed        { get; set; }
    public int    KillReward   { get; set; }
    public int    LifeDamage   { get; set; }
    public string Color        { get; set; } = "e74c3c";
}

/// <summary>
/// 商店配置数据（对应 tables/data/shop.xlsx）
/// </summary>
public class ShopConfig
{
    public int   Id           { get; set; }
    public int   RefreshCost  { get; set; }
    public int   LockCost     { get; set; }
    public int   FreeRefresh  { get; set; }
    public List<object> HeroPool { get; set; } = new();
}
