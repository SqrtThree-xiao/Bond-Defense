using Godot;
using System.Collections.Generic;

/// <summary>
/// 游戏主管理器 - 控制波次、准备/战斗状态切换、金币生命
/// </summary>
public partial class GameManager : Node
{
    // ─────────────── 游戏状态 ───────────────
    public enum GameState { Prepare, Battle, GameOver }
    public GameState CurrentState { get; private set; } = GameState.Prepare;

    // ─────────────── 资源 ───────────────
    public int Gold { get; private set; } = 30;
    public int Life { get; private set; } = 20;
    public int Wave { get; private set; } = 0;
    public int MaxWave { get; } = 10;

    // ─────────────── 子节点引用 ───────────────
    private Battlefield _battlefield;
    private SynergyManager _synergyManager;

    // ─────────────── 待部署区 ───────────────
    private List<Hero> _bench = new();  // 待部署区中的英雄
    public IReadOnlyList<Hero> Bench => _bench;

    // ─────────────── 商店 ───────────────
    private List<HeroData> _shopSlots = new();  // 当前商店5个槽
    private bool _shopLocked = false;
    private List<HeroData> _heroPool = new();  // 英雄池

    // ─────────────── 波次计时 ───────────────
    private float _spawnTimer = 0f;
    private int _spawnedCount = 0;
    private WaveConfig _currentWave;  // WaveConfig = ConfigModels.cs 中的类

    // ─────────────── 信号 ───────────────
    [Signal] public delegate void GoldChangedEventHandler(int newGold);
    [Signal] public delegate void LifeChangedEventHandler(int newLife);
    [Signal] public delegate void WaveChangedEventHandler(int wave, int maxWave);
    [Signal] public delegate void StateChangedEventHandler(int state);
    [Signal] public delegate void ShopRefreshedEventHandler();
    [Signal] public delegate void BenchChangedEventHandler();
    [Signal] public delegate void MergeAvailableEventHandler(string heroName);
    [Signal] public delegate void GameOverEventHandler(bool win);
    [Signal] public delegate void ShowMessageEventHandler(string msg);

    public override void _Ready()
    {
        _battlefield = GetParent().GetNode<Battlefield>("Battlefield");
        _synergyManager = GetParent().GetNode<SynergyManager>("SynergyManager");

        _battlefield.EnemyReachedEnd += OnEnemyReachedEnd;
        _battlefield.EnemyKilled += OnEnemyKilled;
        _battlefield.HeroPlaced += OnHeroPlaced;
        _battlefield.DragReturnedToBench += OnDragReturnedToBench;

        _synergyManager.SynergiesUpdated += OnSynergiesUpdated;

        InitHeroPool();
        RefreshShop();

        EmitSignal(SignalName.WaveChanged, Wave, MaxWave);
        EmitSignal(SignalName.GoldChanged, Gold);
        EmitSignal(SignalName.LifeChanged, Life);
    }

    private void InitHeroPool()
    {
        var cfg = ConfigLoader.Instance;
        if (cfg != null && cfg.Heroes.Count > 0)
        {
            // 从配置表构建英雄池
            foreach (var heroConfig in cfg.Heroes.Values)
            {
                _heroPool.Add(HeroDataFromConfig(heroConfig));
            }
            GD.Print($"[GameManager] Loaded {_heroPool.Count} heroes from config.");
        }
        else
        {
            // 回退：硬编码默认数据（ConfigLoader 尚未初始化时）
            GD.PrintErr("[GameManager] ConfigLoader not ready, using fallback hero data.");
            _heroPool.AddRange(new[]
            {
                MakeHero("骑士",    1, 3, 60f,  1.0f, 140f, 200f, new[]{"人类","战士"},  Colors.SkyBlue),
                MakeHero("弓手",    1, 3, 45f,  1.4f, 220f, 150f, new[]{"人类","精灵"},  Colors.LightGreen),
                MakeHero("法师",    2, 5, 80f,  0.8f, 200f, 180f, new[]{"精灵","法师"},  new Color(0.7f,0.4f,1f)),
                MakeHero("战士",    1, 3, 70f,  1.1f, 130f, 250f, new[]{"人类","战士"},  new Color(0.9f,0.5f,0.2f)),
                MakeHero("猎人",    1, 3, 50f,  1.5f, 240f, 160f, new[]{"人类","野兽"},  new Color(0.6f,0.8f,0.3f)),
                MakeHero("精灵法师",2, 5, 75f,  0.9f, 210f, 170f, new[]{"精灵","法师"},  new Color(0.3f,0.7f,0.9f)),
                MakeHero("野蛮人",  1, 3, 65f,  1.3f, 150f, 200f, new[]{"人类","野兽"},  new Color(0.8f,0.3f,0.2f)),
                MakeHero("德鲁伊",  2, 5, 55f,  0.8f, 180f, 220f, new[]{"精灵","法师"},  new Color(0.2f,0.7f,0.4f)),
            });
        }
    }

    /// <summary>将 HeroConfig（配置表）转换为运行时 HeroData</summary>
    private HeroData HeroDataFromConfig(HeroConfig cfg)
    {
        var d = new HeroData();
        d.HeroName        = cfg.Name;
        d.Rarity          = cfg.Rarity switch { "rare" => 2, "epic" => 3, _ => 1 };
        d.Price           = cfg.Cost;
        d.BaseAttack      = cfg.Attack;
        d.BaseAttackSpeed = cfg.AttackSpeed;
        d.BaseRange       = cfg.Range;
        d.BaseHp          = cfg.Hp;
        d.Tags            = cfg.Tags.ToArray();
        d.SkillName       = "";
        d.SkillDescription = "";
        // 颜色：配置表存 hex string，如 "5dade2"
        d.HeroColor = !string.IsNullOrEmpty(cfg.Color)
            ? new Color($"#{cfg.Color}")
            : Colors.White;
        return d;
    }

    private HeroData MakeHero(string name, int rarity, int price,
        float atk, float atkSpd, float range, float hp,
        string[] tags, Color color)
    {
        var d = new HeroData();
        d.HeroName = name;
        d.Rarity = rarity;
        d.Price = price;
        d.BaseAttack = atk;
        d.BaseAttackSpeed = atkSpd;
        d.BaseRange = range;
        d.BaseHp = hp;
        d.Tags = tags;
        d.SkillName = "";
        d.SkillDescription = "";
        d.HeroColor = color;
        return d;
    }

    // ─────────────────────── 商店逻辑 ───────────────────────

    public void RefreshShop(bool costGold = false)
    {
        if (costGold)
        {
            int refreshCost = ConfigLoader.Instance?.Shop?.RefreshCost ?? 2;
            if (Gold < refreshCost)
            {
                EmitSignal(SignalName.ShowMessage, $"金币不足！刷新需要{refreshCost}金币");
                return;
            }
            SpendGold(refreshCost);
        }

        _shopSlots.Clear();
        var pool = new List<HeroData>(_heroPool);
        for (int i = 0; i < 5 && pool.Count > 0; i++)
        {
            int idx = GD.RandRange(0, pool.Count - 1);
            _shopSlots.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        EmitSignal(SignalName.ShopRefreshed);
    }

    public List<HeroData> GetShopSlots() => new(_shopSlots);

    public void ToggleShopLock()
    {
        _shopLocked = !_shopLocked;
        EmitSignal(SignalName.ShowMessage, _shopLocked ? "商店已锁定" : "商店已解锁");
    }

    public bool IsShopLocked => _shopLocked;

    public bool BuyHero(int shopSlotIndex)
    {
        if (shopSlotIndex < 0 || shopSlotIndex >= _shopSlots.Count) return false;
        if (_bench.Count >= 8)
        {
            EmitSignal(SignalName.ShowMessage, "待部署区已满！");
            return false;
        }

        var heroData = _shopSlots[shopSlotIndex];
        if (Gold < heroData.Price)
        {
            EmitSignal(SignalName.ShowMessage, "金币不足！");
            return false;
        }

        SpendGold(heroData.Price);
        _shopSlots.RemoveAt(shopSlotIndex);

        // 创建英雄并加入待部署区
        var hero = CreateHeroInstance(heroData);
        _bench.Add(hero);

        EmitSignal(SignalName.BenchChanged);
        CheckMerge();
        return true;
    }

    // 用于临时持有待部署英雄的隐藏容器
    private Node _heroStorage;

    public Hero CreateHeroInstance(HeroData data)
    {
        var hero = new Hero();
        hero.Data = data;
        hero.Star = 1;
        // 加入存储节点以触发 _Ready（初始化 BuffComponent 等）
        if (_heroStorage == null)
        {
            _heroStorage = new Node();
            _heroStorage.Name = "HeroStorage";
            GetParent().AddChild(_heroStorage);
            // 让 Battlefield 知道 HeroStorage 的位置，用于归还拖拽失败的英雄
            _battlefield.HeroStorage = _heroStorage;
        }
        _heroStorage.AddChild(hero);
        return hero;
    }

    public void SellHero(Hero hero, bool fromBench)
    {
        int price = Mathf.Max(1, hero.Data.Price * hero.Star / 2);
        AddGold(price);

        if (fromBench)
        {
            _bench.Remove(hero);
            hero.QueueFree();
            EmitSignal(SignalName.BenchChanged);
        }
        else
        {
            // 从战场出售
            _battlefield.GetAllHeroes().ForEach(h =>
            {
                if (h == hero)
                {
                    // 找到格子并移除
                }
            });
            hero.QueueFree();
            UpdateSynergies();
        }
        EmitSignal(SignalName.ShowMessage, $"已出售 {hero.Data.HeroName}，获得 {price} 金币");
    }

    public bool MoveFromBenchToField(Hero hero, int col, int row)
    {
        if (!_bench.Contains(hero)) return false;
        bool placed = _battlefield.PlaceHero(hero, col, row);
        if (placed)
        {
            _bench.Remove(hero);
            EmitSignal(SignalName.BenchChanged);
            UpdateSynergies();
        }
        return placed;
    }

    // ─────────────────────── 升星合成 ───────────────────────

    private void CheckMerge()
    {
        var allHeroes = new List<Hero>(_bench);
        allHeroes.AddRange(_battlefield.GetAllHeroes());

        // 按名称和星级分组
        var groups = new Dictionary<string, List<Hero>>();
        foreach (var hero in allHeroes)
        {
            string key = $"{hero.Data.HeroName}_{hero.Star}";
            if (!groups.ContainsKey(key)) groups[key] = new();
            groups[key].Add(hero);
        }

        foreach (var (key, group) in groups)
        {
            if (group.Count >= 3 && group[0].Star < 3)
            {
                EmitSignal(SignalName.MergeAvailable, group[0].Data.HeroName);
            }
        }
    }

    public bool TryMerge(string heroName, int star)
    {
        var allHeroes = new List<Hero>(_bench);
        allHeroes.AddRange(_battlefield.GetAllHeroes());

        var matching = allHeroes.FindAll(h => h.Data.HeroName == heroName && h.Star == star);
        if (matching.Count < 3) return false;

        // 消耗3个英雄
        for (int i = 0; i < 3; i++)
        {
            var h = matching[i];
            _bench.Remove(h);
            // 若在战场，移出战场格子（简单处理：直接free）
            h.QueueFree();
        }

        // 生成升星英雄
        var newHero = CreateHeroInstance(matching[0].Data);
        newHero.Star = star + 1;
        newHero.RefreshVisual();

        if (_bench.Count < 8)
        {
            _bench.Add(newHero);
        }

        EmitSignal(SignalName.BenchChanged);
        UpdateSynergies();
        EmitSignal(SignalName.ShowMessage, $"{heroName} 升级为 {star + 1}星！");
        return true;
    }

    // ─────────────────────── 波次控制 ───────────────────────

    public void StartBattle()
    {
        if (CurrentState != GameState.Prepare) return;

        Wave++;
        CurrentState = GameState.Battle;
        EmitSignal(SignalName.StateChanged, (int)CurrentState);
        EmitSignal(SignalName.WaveChanged, Wave, MaxWave);

        // 优先从配置表读取本波参数
        var waveCfg = ConfigLoader.Instance?.GetWave(Wave);
        if (waveCfg != null)
        {
            _currentWave = waveCfg;
        }
        else
        {
            // 回退：公式计算
            _currentWave = new WaveConfig
            {
                Id            = Wave,
                EnemyCount    = 5 + Wave * 2,
                BaseHp        = 80 + Wave * 30,
                Speed         = 70f + Wave * 5f,
                SpawnInterval = Mathf.Max(0.3f, 1.5f - Wave * 0.05f),
                KillReward    = 5 + Wave,
                WaveReward    = 10 + Wave * 2,
            };
        }
        _spawnedCount = 0;
        _spawnTimer = 0f;
    }

    public override void _Process(double delta)
    {
        if (CurrentState != GameState.Battle) return;

        // 生成敌人
        if (_spawnedCount < _currentWave.EnemyCount)
        {
            _spawnTimer += (float)delta;
            if (_spawnTimer >= _currentWave.SpawnInterval)
            {
                _spawnTimer = 0f;
                _spawnedCount++;
                _battlefield.SpawnEnemy(_currentWave.BaseHp, _currentWave.Speed, _currentWave.KillReward);
            }
        }
        else if (_battlefield.ActiveEnemies.Count == 0)
        {
            // 所有敌人已处理，进入准备阶段
            EndWave();
        }
    }

    private void EndWave()
    {
        if (CurrentState != GameState.Battle) return;
        CurrentState = GameState.Prepare;
        EmitSignal(SignalName.StateChanged, (int)CurrentState);

        // 波次结束奖励（优先读配置表）
        int reward = _currentWave?.WaveReward ?? (10 + Wave * 2);
        AddGold(reward);
        EmitSignal(SignalName.ShowMessage, $"第{Wave}波结束！获得{reward}金币");

        if (!_shopLocked)
            RefreshShop();

        if (Wave >= MaxWave)
        {
            CurrentState = GameState.GameOver;
            EmitSignal(SignalName.GameOver, true);
        }
    }

    // ─────────────────────── 事件处理 ───────────────────────

    private void OnEnemyReachedEnd()
    {
        Life--;
        EmitSignal(SignalName.LifeChanged, Life);
        if (Life <= 0)
        {
            CurrentState = GameState.GameOver;
            EmitSignal(SignalName.GameOver, false);
        }
    }

    private void OnEnemyKilled(int reward)
    {
        AddGold(reward);
    }

    private void OnHeroPlaced(Hero hero, int col, int row)
    {
        // 如果英雄在待部署区中，从 bench 移除
        if (_bench.Remove(hero))
        {
            EmitSignal(SignalName.BenchChanged);
        }
        UpdateSynergies();
    }

    private void OnDragReturnedToBench()
    {
        // 从 bench 拖出但放置失败，bench 列表未改变，只需刷新 UI
        EmitSignal(SignalName.BenchChanged);
    }

    private void OnSynergiesUpdated()
    {
        // 羁绊更新时UI会通过信号刷新
    }

    // ─────────────────────── 工具方法 ───────────────────────

    private void AddGold(int amount)
    {
        Gold += amount;
        EmitSignal(SignalName.GoldChanged, Gold);
    }

    private void SpendGold(int amount)
    {
        Gold = Mathf.Max(0, Gold - amount);
        EmitSignal(SignalName.GoldChanged, Gold);
    }

    public void UpdateSynergies()
    {
        var allHeroes = _battlefield.GetAllHeroes();
        _synergyManager.RecalculateSynergies(allHeroes);
    }
}
