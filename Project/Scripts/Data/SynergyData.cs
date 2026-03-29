using Godot;
using System.Collections.Generic;

/// <summary>
/// 羁绊定义数据资源
/// </summary>
[GlobalClass]
public partial class SynergyData : Resource
{
    [Export] public string SynergyName { get; set; } = "羁绊";
    [Export] public string Tag { get; set; } = "";          // 对应英雄标签
    [Export] public string Description { get; set; } = "";

    // 激活阈值 (如 [2, 4, 6])
    [Export] public int[] Thresholds { get; set; } = new int[] { 2, 4 };

    // 对应各阶效果描述
    [Export] public string[] EffectDescriptions { get; set; } = new string[] { "+15%攻击", "+30%攻击" };

    // 对应各阶数值加成 (攻击力百分比加成)
    [Export] public float[] AttackBonuses { get; set; } = new float[] { 0.15f, 0.30f };

    // 攻速加成
    [Export] public float[] AttackSpeedBonuses { get; set; } = new float[] { 0f, 0f };

    // 范围加成
    [Export] public float[] RangeBonuses { get; set; } = new float[] { 0f, 0f };

    // 颜色（UI展示用）
    [Export] public Color SynergyColor { get; set; } = new Color(1f, 0.8f, 0.2f);

    /// <summary>
    /// 根据当前计数获取激活的阶级(0=未激活)
    /// </summary>
    public int GetActiveTier(int count)
    {
        int tier = 0;
        for (int i = 0; i < Thresholds.Length; i++)
        {
            if (count >= Thresholds[i])
                tier = i + 1;
        }
        return tier;
    }

    public float GetAttackBonus(int count)
    {
        int tier = GetActiveTier(count) - 1;
        if (tier < 0 || tier >= AttackBonuses.Length) return 0f;
        return AttackBonuses[tier];
    }

    public float GetAttackSpeedBonus(int count)
    {
        int tier = GetActiveTier(count) - 1;
        if (tier < 0 || tier >= AttackSpeedBonuses.Length) return 0f;
        return AttackSpeedBonuses[tier];
    }

    public float GetRangeBonus(int count)
    {
        int tier = GetActiveTier(count) - 1;
        if (tier < 0 || tier >= RangeBonuses.Length) return 0f;
        return RangeBonuses[tier];
    }
}
