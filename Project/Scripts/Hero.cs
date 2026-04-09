using Godot;
using System.Collections.Generic;

/// <summary>
/// 英雄实例节点 - 自动攻击射程内的敌人
/// 视觉元素从 Hero.tscn 预制场景加载，代码只需获取子节点引用
/// </summary>
public partial class Hero : Node2D
{
	public HeroData Data { get; set; }
	public int Star { get; set; } = 1;

	private BuffComponent _buffComp;
	private float _attackTimer = 0f;
	private Enemy _currentTarget = null;
	private List<Enemy> _enemiesInRange = new();

	// 预制场景子节点引用（在 _Ready 中从场景树获取）
	private Polygon2D _body;
	private Line2D _border;
	private Label _nameLabel;
	private Label _starLabel;
	private Control _rangeCircle;
	private Area2D _clickArea;

	/// <summary>预制场景路径</summary>
	private const string HeroScenePath = "res://Scenes/Hero.tscn";

	/// <summary>缓存加载的预制场景</summary>
	private static PackedScene _heroScene;

	// 事件
	[Signal]
	public delegate void HeroClickedEventHandler(Hero hero);

	// 最终属性（含Buff）
	public float FinalAttack => _buffComp != null ? _buffComp.GetFinalAttack(Data.BaseAttack * Data.GetStarMultiplier(Star)) : Data.BaseAttack;
	public float FinalAttackSpeed => _buffComp != null ? _buffComp.GetFinalAttackSpeed(Data.BaseAttackSpeed) : Data.BaseAttackSpeed;
	public float FinalRange => _buffComp != null ? _buffComp.GetFinalRange(Data.BaseRange) : Data.BaseRange;

	public BuffComponent BuffComp => _buffComp;

	/// <summary>
	/// 静态工厂方法：创建英雄实例（从 Hero.tscn 预制加载）
	/// </summary>
	public static Hero Create(HeroData data, int star = 1)
	{
		if (_heroScene == null)
			_heroScene = GD.Load<PackedScene>(HeroScenePath);

		var hero = _heroScene.Instantiate<Hero>();
		hero.Data = data;
		hero.Star = star;
		return hero;
	}

	public override void _Ready()
	{
		// 获取预制场景中的子节点引用
		_body = GetNode<Polygon2D>("Body");
		_border = GetNode<Line2D>("Border");
		_nameLabel = GetNode<Label>("NameLabel");
		_starLabel = GetNode<Label>("StarLabel");
		_rangeCircle = GetNode<Control>("RangeCircle");
		_clickArea = GetNode<Area2D>("ClickArea");

		// BuffComponent 由预制场景中的节点承担，但脚本需要动态绑定
		// 如果预制中已有 BuffComponent 节点，直接获取；否则创建
		_buffComp = GetNodeOrNull<BuffComponent>("BuffComponent");
		if (_buffComp == null)
		{
			_buffComp = new BuffComponent();
			AddChild(_buffComp);
		}
		_buffComp.BuffChanged += OnBuffChanged;

		// 绑定点击事件
		_clickArea.InputEvent += OnAreaInput;

		// 初始化视觉
		ApplyDataVisual();
	}

	/// <summary>
	/// 根据 Data 和 Star 设置视觉
	/// </summary>
	private void ApplyDataVisual()
	{
		if (Data == null) return;

		// 英雄颜色
		_body.Color = Data.HeroColor;

		// 名称
		_nameLabel.Text = Data.HeroName;

		// 星级
		UpdateStarLabel();
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
		ShowAttackFlash();
	}

	private void ShowAttackFlash()
	{
		if (_body == null) return;
		var tween = CreateTween();
		tween.TweenProperty(_body, "color", Colors.White, GameConst.Visual.AttackFlashDuration);
		tween.TweenProperty(_body, "color", Data.HeroColor, GameConst.Visual.AttackFlashRecover);
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
			_body.Color = Star switch
			{
				2 => Data.HeroColor.Lerp(Colors.Gold, 0.3f),
				3 => Data.HeroColor.Lerp(Colors.Gold, 0.6f),
				_ => Data.HeroColor
			};
		}
		// 升星时边框也更新为金色
		if (_border != null)
		{
			_border.DefaultColor = Star switch
			{
				2 => new Color(1f, 0.85f, 0.3f),
				3 => new Color(1f, 0.75f, 0.1f),
				_ => Colors.White
			};
		}
	}

	// ─────────────── 合成动画 ───────────────

	/// <summary>
	/// 播放被消耗英雄的飞入目标动画：从当前位置飞向目标全局坐标，
	/// 到达后自动缩小消失。
	/// 动画完成后发射 MergeConsumedFinished 信号，调用方可 QueueFree 该英雄。
	/// </summary>
	[Signal]
	public delegate void MergeConsumedFinishedEventHandler(Hero hero);

	/// <summary>合成动画中：是否已被消耗（防止重复触发）</summary>
	private bool _isMerging = false;

	public bool IsMerging => _isMerging;

	/// <summary>
	/// 开始合成消耗动画：先飞向目标位置，再缩小消失。
	/// </summary>
	/// <param name="targetGlobalPos">目标英雄的全局坐标</param>
	public void PlayMergeConsumeAnimation(Vector2 targetGlobalPos)
	{
		if (_isMerging) return;
		_isMerging = true;

		// 阶段1：飞向目标
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Quad);

		// 飞行过程中逐渐缩小 + 变淡
		float flyDur = GameConst.Visual.MergeFlyDuration;
		tween.TweenProperty(this, "global_position", targetGlobalPos, flyDur);
		tween.Parallel().TweenProperty(this, "scale", new Vector2(0.5f, 0.5f), flyDur);
		tween.Parallel().TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0.4f), flyDur);

		// 阶段2：到达后进一步缩小消失
		float shrinkDur = GameConst.Visual.MergeShrinkDuration;
		tween.TweenProperty(this, "scale", new Vector2(0.01f, 0.01f), shrinkDur);
		tween.Parallel().TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), shrinkDur);

		// 动画结束通知调用方销毁
		tween.TweenCallback(Callable.From(() =>
		{
			EmitSignal(SignalName.MergeConsumedFinished, this);
		}));
	}

	/// <summary>
	/// 播放目标英雄的升星弹跳动画：
	/// 先短暂放大再回弹到正常大小，伴随金色闪光。
	/// 应在 Star 属性已更新、RefreshVisual 已调用之后播放。
	/// </summary>
	public void PlayMergeStarUpAnimation()
	{
		var tween = CreateTween();
		float dur = GameConst.Visual.MergeBounceDuration;

		// 弹跳放大 → 回弹
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Back);
		float peakScale = GameConst.Visual.MergeBounceScale;
		tween.TweenProperty(this, "scale", new Vector2(peakScale, peakScale), dur * 0.55f);

		tween.SetEase(Tween.EaseType.In);
		tween.SetTrans(Tween.TransitionType.Elastic);
		tween.TweenProperty(this, "scale", Vector2.One, dur * 0.45f);

		// 同步闪光效果：body 颜色闪金 → 恢复
		if (_body != null && Data != null)
		{
			Color goldFlash = GameConst.Visual.MergeGlowColor;
			Color finalColor = _body.Color; // RefreshVisual 已设好的颜色

			var flashTween = CreateTween();
			flashTween.TweenProperty(_body, "color", goldFlash, dur * 0.25f);
			flashTween.TweenProperty(_body, "color", finalColor, dur * 0.6f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Expo);
		}
	}
}
