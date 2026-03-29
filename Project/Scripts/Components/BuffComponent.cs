using Godot;
using System.Collections.Generic;

/// <summary>
/// 英雄实例上的Buff管理组件
/// </summary>
public partial class BuffComponent : Node
{
    private float _attackBonus = 0f;        // 攻击加成 (百分比)
    private float _attackSpeedBonus = 0f;   // 攻速加成 (百分比)
    private float _rangeBonus = 0f;         // 范围加成 (百分比)

    [Signal]
    public delegate void BuffChangedEventHandler();

    public float AttackBonus => _attackBonus;
    public float AttackSpeedBonus => _attackSpeedBonus;
    public float RangeBonus => _rangeBonus;

    /// <summary>
    /// 重置所有Buff
    /// </summary>
    public void ClearBuffs()
    {
        _attackBonus = 0f;
        _attackSpeedBonus = 0f;
        _rangeBonus = 0f;
        EmitSignal(SignalName.BuffChanged);
    }

    /// <summary>
    /// 应用羁绊效果
    /// </summary>
    public void ApplySynergyBuff(float atkBonus, float atkSpeedBonus, float rangeBonus)
    {
        _attackBonus += atkBonus;
        _attackSpeedBonus += atkSpeedBonus;
        _rangeBonus += rangeBonus;
        EmitSignal(SignalName.BuffChanged);
    }

    /// <summary>
    /// 获取最终攻击力（基础值 * (1 + 加成)）
    /// </summary>
    public float GetFinalAttack(float baseAttack)
    {
        return baseAttack * (1f + _attackBonus);
    }

    public float GetFinalAttackSpeed(float baseSpeed)
    {
        return baseSpeed * (1f + _attackSpeedBonus);
    }

    public float GetFinalRange(float baseRange)
    {
        return baseRange * (1f + _rangeBonus);
    }
}
