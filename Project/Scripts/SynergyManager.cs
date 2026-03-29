using Godot;
using System.Collections.Generic;

/// <summary>
/// 羁绊管理单例 - 统计场上英雄标签、激活羁绊效果
/// </summary>
public partial class SynergyManager : Node
{
    private static SynergyManager _instance;
    public static SynergyManager Instance => _instance;

    // 所有已定义的羁绊
    private List<SynergyData> _allSynergies = new();

    // 当前激活的羁绊 <Tag, Count>
    private Dictionary<string, int> _tagCounts = new();
    // 当前激活的羁绊 <Tag, Tier>
    public Dictionary<string, int> ActiveSynergies { get; private set; } = new();

    [Signal]
    public delegate void SynergiesUpdatedEventHandler();

    public override void _Ready()
    {
        _instance = this;
        LoadSynergyData();
    }

    private void LoadSynergyData()
    {
        var cfg = ConfigLoader.Instance;
        if (cfg != null && cfg.Synergies.Count > 0)
        {
            foreach (var sc in cfg.Synergies.Values)
            {
                _allSynergies.Add(SynergyDataFromConfig(sc));
            }
            GD.Print($"[SynergyManager] Loaded {_allSynergies.Count} synergies from config.");
            return;
        }

        // 回退：硬编码默认数据
        GD.PrintErr("[SynergyManager] ConfigLoader not ready, using fallback synergy data.");
        _allSynergies.Add(CreateSynergy(
            "人类", "人类",
            "人类英雄相互鼓励，提升攻击力",
            new int[] { 2, 4 },
            new string[] { "+20%攻击", "+40%攻击" },
            new float[] { 0.20f, 0.40f },
            new float[] { 0f, 0f },
            new float[] { 0f, 0f },
            new Color(0.9f, 0.7f, 0.3f)
        ));
        _allSynergies.Add(CreateSynergy(
            "精灵", "精灵",
            "精灵感应自然，提升攻击速度和范围",
            new int[] { 2, 4 },
            new string[] { "+20%攻速", "+25%攻速+15%范围" },
            new float[] { 0f, 0f },
            new float[] { 0.20f, 0.25f },
            new float[] { 0f, 0.15f },
            new Color(0.3f, 0.9f, 0.5f)
        ));
        _allSynergies.Add(CreateSynergy(
            "战士", "战士",
            "战士并肩作战，提升攻击力和攻速",
            new int[] { 2, 3 },
            new string[] { "+15%攻击+10%攻速", "+30%攻击+20%攻速" },
            new float[] { 0.15f, 0.30f },
            new float[] { 0.10f, 0.20f },
            new float[] { 0f, 0f },
            new Color(0.9f, 0.4f, 0.2f)
        ));
        _allSynergies.Add(CreateSynergy(
            "法师", "法师",
            "法师之间共鸣，大幅提升攻击范围",
            new int[] { 2, 3 },
            new string[] { "+30%范围", "+60%范围+15%攻击" },
            new float[] { 0f, 0.15f },
            new float[] { 0f, 0f },
            new float[] { 0.30f, 0.60f },
            new Color(0.5f, 0.3f, 0.9f)
        ));
        _allSynergies.Add(CreateSynergy(
            "野兽", "野兽",
            "野兽本能觉醒，攻击速度大幅提升",
            new int[] { 2, 4 },
            new string[] { "+25%攻速", "+50%攻速" },
            new float[] { 0f, 0f },
            new float[] { 0.25f, 0.50f },
            new float[] { 0f, 0f },
            new Color(0.6f, 0.3f, 0.1f)
        ));
    }

    /// <summary>将 SynergyConfig（配置表）转换为运行时 SynergyData</summary>
    private SynergyData SynergyDataFromConfig(SynergyConfig sc)
    {
        // 解析加成数字（配置表存字符串如 "0.2"）
        float ParseF(string s) => float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;

        var atkBonuses   = sc.AtkBonus.ConvertAll(ParseF).ToArray();
        var spdBonuses   = sc.SpdBonus.ConvertAll(ParseF).ToArray();
        var rangeBonuses = sc.RangeBonus.ConvertAll(ParseF).ToArray();

        // 若数组长度不足2，补零
        float[] Pad2(float[] arr) => arr.Length >= 2 ? arr : new float[] {
            arr.Length > 0 ? arr[0] : 0f,
            arr.Length > 1 ? arr[1] : 0f
        };

        // 颜色：用标签哈希给一个固定颜色（配置表暂无颜色字段）
        Color tagColor = sc.Tag switch
        {
            "人类" => new Color(0.9f, 0.7f, 0.3f),
            "精灵" => new Color(0.3f, 0.9f, 0.5f),
            "战士" => new Color(0.9f, 0.4f, 0.2f),
            "法师" => new Color(0.5f, 0.3f, 0.9f),
            "野兽" => new Color(0.6f, 0.3f, 0.1f),
            _      => Colors.White,
        };

        return CreateSynergy(
            sc.Name, sc.Tag,
            sc.Descriptions.Count > 0 ? sc.Descriptions[0] : "",
            new int[] { sc.Tier1, sc.Tier2 },
            sc.Descriptions.ToArray(),
            Pad2(atkBonuses),
            Pad2(spdBonuses),
            Pad2(rangeBonuses),
            tagColor
        );
    }

    private SynergyData CreateSynergy(
        string name, string tag, string desc,
        int[] thresholds, string[] effectDescs,
        float[] atkBonuses, float[] atkSpeedBonuses, float[] rangeBonuses,
        Color color)
    {
        var s = new SynergyData();
        s.SynergyName = name;
        s.Tag = tag;
        s.Description = desc;
        s.Thresholds = thresholds;
        s.EffectDescriptions = effectDescs;
        s.AttackBonuses = atkBonuses;
        s.AttackSpeedBonuses = atkSpeedBonuses;
        s.RangeBonuses = rangeBonuses;
        s.SynergyColor = color;
        return s;
    }

    /// <summary>
    /// 重新计算所有羁绊（在英雄变化时调用）
    /// </summary>
    public void RecalculateSynergies(List<Hero> allHeroes)
    {
        _tagCounts.Clear();
        ActiveSynergies.Clear();

        // 统计所有标签
        foreach (var hero in allHeroes)
        {
            if (hero == null || hero.Data == null) continue;
            foreach (var tag in hero.GetActiveTags())
            {
                if (string.IsNullOrEmpty(tag)) continue;
                _tagCounts.TryAdd(tag, 0);
                _tagCounts[tag]++;
            }
        }

        // 确定激活的羁绊
        foreach (var synergy in _allSynergies)
        {
            _tagCounts.TryGetValue(synergy.Tag, out int count);
            int tier = synergy.GetActiveTier(count);
            if (tier > 0)
                ActiveSynergies[synergy.Tag] = tier;
        }

        // 先清除所有英雄的羁绊Buff
        foreach (var hero in allHeroes)
            hero.BuffComp?.ClearBuffs();

        // 重新应用羁绊效果
        foreach (var hero in allHeroes)
        {
            if (hero.Data == null) continue;
            foreach (var tag in hero.GetActiveTags())
            {
                if (!ActiveSynergies.ContainsKey(tag)) continue;
                var synergy = _allSynergies.Find(s => s.Tag == tag);
                if (synergy == null) continue;

                _tagCounts.TryGetValue(tag, out int count);
                float atkBonus = synergy.GetAttackBonus(count);
                float atkSpeedBonus = synergy.GetAttackSpeedBonus(count);
                float rangeBonus = synergy.GetRangeBonus(count);
                hero.BuffComp?.ApplySynergyBuff(atkBonus, atkSpeedBonus, rangeBonus);
            }
        }

        EmitSignal(SignalName.SynergiesUpdated);
    }

    /// <summary>
    /// 获取所有羁绊数据列表（用于UI展示）
    /// </summary>
    public List<(SynergyData data, int count, int tier)> GetAllSynergiesWithCount()
    {
        var result = new List<(SynergyData, int, int)>();
        foreach (var synergy in _allSynergies)
        {
            _tagCounts.TryGetValue(synergy.Tag, out int count);
            int tier = synergy.GetActiveTier(count);
            result.Add((synergy, count, tier));
        }
        return result;
    }

    public SynergyData GetSynergyByTag(string tag)
    {
        return _allSynergies.Find(s => s.Tag == tag);
    }
}
