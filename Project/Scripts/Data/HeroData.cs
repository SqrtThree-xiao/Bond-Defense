using Godot;
using System.Collections.Generic;

/// <summary>
/// 英雄数据资源 - 定义英雄的基础属性、标签和技能
/// </summary>
[GlobalClass]
public partial class HeroData : Resource
{
    [Export] public string HeroName { get; set; } = "英雄";
    [Export] public string Description { get; set; } = "";
    [Export] public int Rarity { get; set; } = 1;   // 1=普通 2=精英 3=传奇
    [Export] public int Price { get; set; } = 3;

    // 基础属性
    [Export] public float BaseAttack { get; set; } = 50f;
    [Export] public float BaseAttackSpeed { get; set; } = 1f;  // 每秒攻击次数
    [Export] public float BaseRange { get; set; } = 150f;      // 攻击范围像素
    [Export] public float BaseHp { get; set; } = 300f;

    // 标签（用于羁绊判定）
    [Export] public string[] Tags { get; set; } = System.Array.Empty<string>();

    // 技能
    [Export] public string SkillName { get; set; } = "";
    [Export] public string SkillDescription { get; set; } = "";
    [Export] public float SkillValue { get; set; } = 0f;

    // 颜色（用于区分英雄）
    [Export] public Color HeroColor { get; set; } = new Color(0.4f, 0.6f, 1f);

    /// <summary>
    /// 获取升星倍率
    /// </summary>
    public float GetStarMultiplier(int star)
    {
        return star switch
        {
            1 => 1.0f,
            2 => 1.8f,
            3 => 3.5f,
            _ => 1.0f
        };
    }
}
