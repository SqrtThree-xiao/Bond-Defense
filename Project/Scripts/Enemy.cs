using Godot;

/// <summary>
/// 敌人节点 - 沿固定路径移动，到达终点扣除玩家生命
/// </summary>
public partial class Enemy : Node2D
{
    [Export] public float MaxHp { get; set; } = 100f;
    [Export] public float Speed { get; set; } = 80f;    // 像素/秒
    [Export] public int Reward { get; set; } = 5;       // 击杀金币

    public float CurrentHp { get; private set; }
    public bool IsDead { get; private set; } = false;
    public float PathProgress { get; private set; } = 0f;  // 路径进度(0-1)

    private Vector2[] _path;
    private int _pathIndex = 0;
    private Polygon2D _body;
    private ColorRect _hpBar;
    private ColorRect _hpBarBg;

    [Signal]
    public delegate void EnemyDiedEventHandler(Enemy enemy, bool reachedEnd);

    public override void _Ready()
    {
        CurrentHp = MaxHp;
        BuildVisual();
    }

    /// <summary>
    /// 设置像素坐标路径（旧接口，兼容保留）
    /// </summary>
    public void SetPath(Vector2[] path)
    {
        _path = path;
        _pathIndex = 0;
        if (_path != null && _path.Length > 0)
            GlobalPosition = _path[0];
    }

    /// <summary>
    /// 设置格子坐标路径（新接口，格子坐标 → 世界坐标 → 内部像素路径）
    /// </summary>
    public void SetGridPath(Vector2I[] gridPath, TileMapLayer tileMap)
    {
        if (gridPath == null || gridPath.Length == 0) return;

        // 格子坐标 → 世界坐标（格子中心点）
        var worldPath = new Vector2[gridPath.Length];
        float cs = GameConst.Grid.DefaultCellSize;
        for (int i = 0; i < gridPath.Length; i++)
        {
            var topLeft = tileMap.MapToLocal(gridPath[i]);
            worldPath[i] = topLeft + new Vector2(cs / 2f, cs / 2f);
        }
        SetPath(worldPath);
    }

    private void BuildVisual()
    {
        // 主体（菱形）
        _body = new Polygon2D();
        _body.Color = new Color(0.9f, 0.2f, 0.2f);
        _body.Polygon = new Vector2[] {
            new Vector2(0, -18),
            new Vector2(14, 0),
            new Vector2(0, 14),
            new Vector2(-14, 0)
        };
        AddChild(_body);

        // HP条背景
        _hpBarBg = new ColorRect();
        _hpBarBg.Color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        _hpBarBg.Position = new Vector2(-18, -28);
        _hpBarBg.Size = new Vector2(36, 5);
        AddChild(_hpBarBg);

        // HP条
        _hpBar = new ColorRect();
        _hpBar.Color = new Color(0.2f, 0.9f, 0.2f);
        _hpBar.Position = new Vector2(-18, -28);
        _hpBar.Size = new Vector2(36, 5);
        AddChild(_hpBar);
    }

    public override void _Process(double delta)
    {
        if (IsDead || _path == null || _pathIndex >= _path.Length) return;

        Vector2 target = _path[_pathIndex];
        Vector2 dir = (target - GlobalPosition);
        float dist = dir.Length();

        float moveAmount = Speed * (float)delta;
        if (dist <= moveAmount)
        {
            GlobalPosition = target;
            _pathIndex++;
            if (_pathIndex >= _path.Length)
            {
                // 到达终点
                Die(reachedEnd: true);
                return;
            }
        }
        else
        {
            GlobalPosition += dir.Normalized() * moveAmount;
        }

        // 更新路径进度
        if (_path.Length > 1)
            PathProgress = (float)_pathIndex / (_path.Length - 1);

        // 朝向
        if (dist > 1f)
        {
            float angle = dir.Normalized().Angle();
            Rotation = angle + Mathf.Pi / 2;
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        CurrentHp -= damage;
        UpdateHpBar();

        // 受击闪白
        var tween = CreateTween();
        tween.TweenProperty(_body, "color", Colors.White, 0.05f);
        tween.TweenProperty(_body, "color", new Color(0.9f, 0.2f, 0.2f), 0.1f);

        if (CurrentHp <= 0)
            Die(reachedEnd: false);
    }

    private void UpdateHpBar()
    {
        if (_hpBar == null) return;
        float ratio = Mathf.Max(0f, CurrentHp / MaxHp);
        _hpBar.Size = new Vector2(36 * ratio, 5);
        _hpBar.Color = ratio > 0.5f ? new Color(0.2f, 0.9f, 0.2f) :
                       ratio > 0.25f ? new Color(0.9f, 0.9f, 0.2f) :
                       new Color(0.9f, 0.2f, 0.2f);
    }

    private void Die(bool reachedEnd)
    {
        if (IsDead) return;
        IsDead = true;
        EmitSignal(SignalName.EnemyDied, this, reachedEnd);

        // 死亡特效
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, 0.2f);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
