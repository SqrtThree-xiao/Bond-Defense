using Godot;
using System.Collections.Generic;

/// <summary>
/// 英雄实例节点 - 自动攻击射程内的敌人
/// </summary>
public partial class Hero : Node2D
{
    [Export] public HeroData Data { get; set; }
    [Export] public int Star { get; set; } = 1;     // 当前星级

    private BuffComponent _buffComp;
    private float _attackTimer = 0f;
    private Enemy _currentTarget = null;
    private List<Enemy> _enemiesInRange = new();

    // UI组件
    private Polygon2D _body;
    private Label _nameLabel;
    private Label _starLabel;
    private Control _rangeCircle;

    // 事件
    [Signal]
    public delegate void HeroClickedEventHandler(Hero hero);

    // 最终属性（含Buff）
    public float FinalAttack => _buffComp != null ? _buffComp.GetFinalAttack(Data.BaseAttack * Data.GetStarMultiplier(Star)) : Data.BaseAttack;
    public float FinalAttackSpeed => _buffComp != null ? _buffComp.GetFinalAttackSpeed(Data.BaseAttackSpeed) : Data.BaseAttackSpeed;
    public float FinalRange => _buffComp != null ? _buffComp.GetFinalRange(Data.BaseRange) : Data.BaseRange;

    public BuffComponent BuffComp => _buffComp;

    public override void _Ready()
    {
        _buffComp = new BuffComponent();
        AddChild(_buffComp);
        _buffComp.BuffChanged += OnBuffChanged;

        BuildVisual();
    }

    private void BuildVisual()
    {
        if (Data == null) return;

        // 英雄主体（八边形）
        _body = new Polygon2D();
        _body.Color = Data.HeroColor;
        var points = new Vector2[8];
        float r = 22f;
        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.Pi * 2 * i / 8 - Mathf.Pi / 2;
            points[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }
        _body.Polygon = points;
        AddChild(_body);

        // 边框（Line2D）
        var border = new Line2D();
        border.Width = 2f;
        border.DefaultColor = Colors.White;
        var borderPoints = new Vector2[9];
        for (int i = 0; i < 9; i++)
        {
            float angle = Mathf.Pi * 2 * (i % 8) / 8 - Mathf.Pi / 2;
            borderPoints[i] = new Vector2(Mathf.Cos(angle) * 22f, Mathf.Sin(angle) * 22f);
        }
        border.Points = borderPoints;
        AddChild(border);

        // 名称
        _nameLabel = new Label();
        _nameLabel.Text = Data.HeroName;
        _nameLabel.AddThemeColorOverride("font_color", Colors.White);
        _nameLabel.AddThemeFontSizeOverride("font_size", 11);
        _nameLabel.Position = new Vector2(-25, 26);
        _nameLabel.Size = new Vector2(50, 16);
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_nameLabel);

        // 星级
        _starLabel = new Label();
        UpdateStarLabel();
        _starLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f));
        _starLabel.AddThemeFontSizeOverride("font_size", 12);
        _starLabel.Position = new Vector2(-15, -38);
        _starLabel.Size = new Vector2(30, 16);
        _starLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_starLabel);

        // 攻击范围显示圆（调试用，默认隐藏）
        _rangeCircle = new Control();
        _rangeCircle.Visible = false;
        AddChild(_rangeCircle);

        // 点击区域
        var area = new Area2D();
        var col = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24f;
        col.Shape = shape;
        area.AddChild(col);
        area.InputPickable = true;
        area.InputEvent += OnAreaInput;
        AddChild(area);
    }

    private void UpdateStarLabel()
    {
        if (_starLabel == null) return;
        _starLabel.Text = Star switch { 1 => "★", 2 => "★★", 3 => "★★★", _ => "" };
    }

    private void OnAreaInput(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            EmitSignal(SignalName.HeroClicked, this);
        }
    }

    private void OnBuffChanged()
    {
        // Buff变化时更新攻击范围等视觉
        UpdateRangeVisual();
    }

    private void UpdateRangeVisual()
    {
        // 可在此更新攻击范围圆圈大小
    }

    public override void _Process(double delta)
    {
        if (Data == null) return;

        // 清理失效目标
        _enemiesInRange.RemoveAll(e => !IsInstanceValid(e) || e.IsDead);

        // 选择目标（优先最近的、已在射程内的）
        _currentTarget = FindBestTarget();

        // 攻击计时
        if (_currentTarget != null)
        {
            _attackTimer += (float)delta;
            float interval = 1f / FinalAttackSpeed;
            if (_attackTimer >= interval)
            {
                _attackTimer = 0f;
                DoAttack(_currentTarget);
            }
        }
        else
        {
            _attackTimer = 0f;
        }
    }

    private Enemy FindBestTarget()
    {
        // 从全局获取在范围内的敌人
        var battlefieldNode = GetTree().GetFirstNodeInGroup("battlefield");
        if (battlefieldNode is not Battlefield battlefield) return null;

        Enemy best = null;
        float bestProgress = -1f;
        float range = FinalRange;

        foreach (var enemy in battlefield.ActiveEnemies)
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead) continue;
            float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
            if (dist <= range && enemy.PathProgress > bestProgress)
            {
                bestProgress = enemy.PathProgress;
                best = enemy;
            }
        }
        return best;
    }

    private void DoAttack(Enemy target)
    {
        if (!IsInstanceValid(target) || target.IsDead) return;
        target.TakeDamage(FinalAttack);

        // 攻击特效（简单闪烁）
        ShowAttackFlash();
    }

    private void ShowAttackFlash()
    {
        if (_body == null) return;
        var tween = CreateTween();
        tween.TweenProperty(_body, "color", Colors.White, 0.05f);
        tween.TweenProperty(_body, "color", Data.HeroColor, 0.1f);
    }

    /// <summary>
    /// 获取英雄标签（含升星额外标签）
    /// </summary>
    public string[] GetActiveTags()
    {
        return Data.Tags;
    }

    /// <summary>
    /// 显示/隐藏攻击范围
    /// </summary>
    public void ShowRange(bool show)
    {
        // 通过绘制圆圈展示
        if (show)
            QueueRedraw();
    }

    public override void _Draw()
    {
        // 绘制攻击范围圆（选中时显示）
    }

    /// <summary>
    /// 升星后更新视觉
    /// </summary>
    public void RefreshVisual()
    {
        UpdateStarLabel();
        if (_body != null && Data != null)
        {
            // 升星颜色变化
            _body.Color = Star switch
            {
                2 => Data.HeroColor.Lerp(Colors.Gold, 0.3f),
                3 => Data.HeroColor.Lerp(Colors.Gold, 0.6f),
                _ => Data.HeroColor
            };
        }
    }
}
