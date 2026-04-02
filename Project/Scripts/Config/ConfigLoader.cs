using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 配置表加载器 —— 从 GDExcelExporter 导出的 JSON 文件加载游戏配置
///
/// 使用方式（全局单例挂到 Main.cs 或直接静态访问）：
///   ConfigLoader.Instance.Heroes[1]   → 英雄1的配置
///   ConfigLoader.Instance.Waves[3]    → 第3波次配置
///   ConfigLoader.Instance.GetWave(3)  → 同上，带越界保护
///
/// JSON 路径规则（相对 res://）：
///   res://Resources/Config/hero/hero.json
///   res://Resources/Config/wave/wave.json
///   res://Resources/Config/synergy/synergy.json
///   res://Resources/Config/enemy/enemy.json
///   res://Resources/Config/shop/shop.json
/// </summary>
public partial class ConfigLoader : Node
{
    public static ConfigLoader Instance { get; private set; }

    // ─────────────────────────────────────────────
    // 配置表字典：key = id
    // ─────────────────────────────────────────────
    public Dictionary<int, HeroConfig>    Heroes   { get; private set; } = new();
    public Dictionary<int, SynergyConfig> Synergies { get; private set; } = new();
    public Dictionary<int, WaveConfig>    Waves    { get; private set; } = new();
    public Dictionary<int, EnemyConfig>   Enemies  { get; private set; } = new();
    public Dictionary<int, UIConfig>      UIs      { get; private set; } = new();
    public ShopConfig                     Shop     { get; private set; } = new();

    public override void _Ready()
    {
        Instance = this;
        LoadAll();
    }

    // ─────────────────────────────────────────────
    // 公共查询方法（带越界保护）
    // ─────────────────────────────────────────────

    public HeroConfig GetHero(int id)
    {
        return Heroes.TryGetValue(id, out var v) ? v : null;
    }

    public WaveConfig GetWave(int wave)
    {
        // 超出配置表范围时返回最后一波（无限关卡兼容）
        if (Waves.TryGetValue(wave, out var v)) return v;
        WaveConfig last = null;
        foreach (var kv in Waves)
            if (last == null || kv.Key > last.Id) last = kv.Value;
        return last;
    }

    public SynergyConfig GetSynergyByTag(string tag)
    {
        foreach (var s in Synergies.Values)
            if (s.Tag == tag) return s;
        return null;
    }

    public EnemyConfig GetEnemy(int id)
    {
        return Enemies.TryGetValue(id, out var v) ? v : null;
    }

    public UIConfig GetUI(int id)
    {
        return UIs.TryGetValue(id, out var v) ? v : null;
    }

    // ─────────────────────────────────────────────
    // 加载入口
    // ─────────────────────────────────────────────

    public void LoadAll()
    {
        Heroes    = LoadTable<HeroConfig>   ("res://Resources/Config/hero/hero.json",       ParseHero);
        Synergies = LoadTable<SynergyConfig>("res://Resources/Config/synergy/synergy.json", ParseSynergy);
        Waves     = LoadTable<WaveConfig>   ("res://Resources/Config/wave/wave.json",       ParseWave);
        Enemies   = LoadTable<EnemyConfig>  ("res://Resources/Config/enemy/enemy.json",     ParseEnemy);
        UIs       = LoadTable<UIConfig>     ("res://Resources/Config/ui/ui.json",           ParseUI);

        var shopDict = LoadTable<ShopConfig>("res://Resources/Config/shop/shop.json", ParseShop);
        if (shopDict.TryGetValue(1, out var shop))
            Shop = shop;

        GD.Print($"[ConfigLoader] Loaded: {Heroes.Count} heroes, {Waves.Count} waves, {Synergies.Count} synergies, {Enemies.Count} enemies, {UIs.Count} uis");
    }

    // ─────────────────────────────────────────────
    // 通用 JSON 表加载
    // ─────────────────────────────────────────────

    private Dictionary<int, T> LoadTable<T>(string resPath, Func<Godot.Collections.Dictionary, T> parser)
    {
        var result = new Dictionary<int, T>();
        if (!FileAccess.FileExists(resPath))
        {
            GD.PrintErr($"[ConfigLoader] File not found: {resPath}");
            return result;
        }

        using var f = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        string text = f.GetAsText();

        var json = new Godot.Json();
        var err = json.Parse(text);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[ConfigLoader] JSON parse error in {resPath}: {json.GetErrorMessage()} at line {json.GetErrorLine()}");
            return result;
        }

        var root = json.Data.AsGodotDictionary();
        foreach (string key in root.Keys)
        {
            var row = root[key].AsGodotDictionary();
            T item = parser(row);
            // 使用 id 字段作为字典 key
            int id = (int)row["id"].AsInt64();
            result[id] = item;
        }
        return result;
    }

    // ─────────────────────────────────────────────
    // 各表解析器
    // ─────────────────────────────────────────────

    private HeroConfig ParseHero(Godot.Collections.Dictionary row)
    {
        var cfg = new HeroConfig
        {
            Id          = (int)row["id"].AsInt64(),
            Name        = row["name"].AsString(),
            Rarity      = row["rarity"].AsString(),
            Cost        = (int)row["cost"].AsInt64(),
            Attack      = (int)row["attack"].AsInt64(),
            AttackSpeed = row["attack_speed"].AsSingle(),
            Range       = row["range"].AsSingle(),
            Hp          = (int)row["hp"].AsInt64(),
            Color       = row["color"].AsString(),
        };

        // tags: JSON2.0 导出 array_str 为 JSON 数组
        var tagsArr = row["tags"].AsGodotArray();
        foreach (var t in tagsArr)
            cfg.Tags.Add(t.AsString());

        return cfg;
    }

    private SynergyConfig ParseSynergy(Godot.Collections.Dictionary row)
    {
        var cfg = new SynergyConfig
        {
            Id       = (int)row["id"].AsInt64(),
            Name     = row["name"].AsString(),
            Tag      = row["tag"].AsString(),
            Tier1    = (int)row["tier1"].AsInt64(),
            Tier2    = (int)row["tier2"].AsInt64(),
            ColorHex = row.ContainsKey("color") ? row["color"].AsString() : "",
        };

        foreach (var v in row["descriptions"].AsGodotArray()) cfg.Descriptions.Add(v.AsString());
        foreach (var v in row["atk_bonus"].AsGodotArray())    cfg.AtkBonus.Add(v.AsString());
        foreach (var v in row["spd_bonus"].AsGodotArray())    cfg.SpdBonus.Add(v.AsString());
        foreach (var v in row["range_bonus"].AsGodotArray())  cfg.RangeBonus.Add(v.AsString());

        return cfg;
    }

    private WaveConfig ParseWave(Godot.Collections.Dictionary row)
    {
        return new WaveConfig
        {
            Id            = (int)row["id"].AsInt64(),
            EnemyCount    = (int)row["enemy_count"].AsInt64(),
            BaseHp        = (int)row["base_hp"].AsInt64(),
            Speed         = row["speed"].AsSingle(),
            SpawnInterval = row["spawn_interval"].AsSingle(),
            KillReward    = (int)row["kill_reward"].AsInt64(),
            WaveReward    = (int)row["wave_reward"].AsInt64(),
        };
    }

    private EnemyConfig ParseEnemy(Godot.Collections.Dictionary row)
    {
        return new EnemyConfig
        {
            Id          = (int)row["id"].AsInt64(),
            Name        = row["name"].AsString(),
            Hp          = (int)row["hp"].AsInt64(),
            Speed       = row["speed"].AsSingle(),
            KillReward  = (int)row["kill_reward"].AsInt64(),
            LifeDamage  = (int)row["life_damage"].AsInt64(),
            Color       = row["color"].AsString(),
        };
    }

    private ShopConfig ParseShop(Godot.Collections.Dictionary row)
    {
        var cfg = new ShopConfig
        {
            Id          = (int)row["id"].AsInt64(),
            RefreshCost = (int)row["refresh_cost"].AsInt64(),
            LockCost    = (int)row["lock_cost"].AsInt64(),
            FreeRefresh = (int)row["free_refresh"].AsInt64(),
        };
        foreach (var v in row["hero_pool"].AsGodotArray())
            cfg.HeroPool.Add(v.AsInt64());
        return cfg;
    }

    private UIConfig ParseUI(Godot.Collections.Dictionary row)
    {
        return new UIConfig
        {
            Id           = (int)row["id"].AsInt64(),
            Name         = row["name"].AsString(),
            Script       = row["script"].AsString(),
            ResourcePath = row["resource_path"].AsString(),
            UILayer      = row.ContainsKey("ui_layer") ? (int)row["ui_layer"].AsInt64() : 0,
            Description  = row.ContainsKey("description") ? row["description"].AsString() : "",
        };
    }
}
