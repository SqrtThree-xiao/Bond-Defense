using Godot;
using System.Collections.Generic;

/// <summary>
/// 待部署区 - 场景层 Node2D
/// 
/// 架构：
///   BenchSlotBase（底座槽位）— 永久存在，提供空位视觉和命中区域
///   Hero 模型（英雄预制） — 购买后动态挂载到槽位上，卖出/放置后卸载
///   
/// 拖拽职责：
///   - 本类统一管理所有拖拽逻辑（bench 内交换 + bench→战场放置 + 拖回归还）
///   - 拖拽时 Hero 节点临时挂到 UILayer 下以置顶显示
/// </summary>
public partial class BenchUI : Node2D
{
	private GameManager _gameManager;
	private Battlefield _battlefield;

	/// <summary>固定数量的底座槽位</summary>
	private readonly List<BenchSlotBase> _slots = new();

	/// <summary>当前挂载到槽位的英雄节点（索引对应 _slots）</summary>
	private readonly Hero[] _slotHeroes = new Hero[GameConst.Game.BenchCapacity];

	/// <summary>待部署区尺寸（由外部布局设置）</summary>
	public Vector2 AreaSize { get; set; } = new Vector2(600f, 140f);

	/// <summary>自动计算的列数</summary>
	private int _cols = 8;

	// 背景面板
	private NinePatchRect _bgRect;
	private Label _titleLabel;

	// ─────────────── 拖拽状态 ───────────────
	private Hero _draggingHero;
	private int _dragSlotIndex = -1;
	private Vector2 _dragOffset;
	private Node2D _dragLayer;   // 拖拽置顶层（挂在 CanvasLayer 下）

	/// <summary>拖拽鼠标偏移量（英雄中心对准鼠标）</summary>
	private static readonly Vector2 DragCursorOffset = new Vector2(0f, -8f);

	public override void _Ready()
	{
		_gameManager = GetTree().Root.GetNode<Main>("Main").GetNode<GameManager>("GameManager");
		_battlefield = GetTree().Root.GetNode<Main>("Main").GetNode<Battlefield>("Battlefield");
		_gameManager.BenchChanged += RefreshDisplay;

		// 延迟创建拖拽置顶层（_Ready 时 Root 可能仍在 blocked 状态，无法 AddChild）
		Callable.From(_CreateDragLayer).CallDeferred();

		BuildBackground();
		BuildSlotBases();
		RefreshDisplay();
	}

	// ─────────────── 拖拽置顶层 ───────────────

	private void _CreateDragLayer()
	{
		_dragLayer = new Node2D();
		_dragLayer.Name = "BenchDragLayer";
		_dragLayer.ZIndex = 100;
		GetTree().Root.AddChild(_dragLayer);
	}

	// ─────────────── 背景构建 ───────────────

	private void BuildBackground()
	{
		// 半透明背景
		_bgRect = new NinePatchRect();
		_bgRect.Size = AreaSize;
		_bgRect.PatchMarginLeft = 4;
		_bgRect.PatchMarginRight = 4;
		_bgRect.PatchMarginTop = 4;
		_bgRect.PatchMarginBottom = 4;
		_bgRect.Modulate = new Color(1f, 1f, 1f, 0.85f);
		_bgRect.SetProcess(false);
		AddChild(_bgRect);

		// 标题
		_titleLabel = new Label();
		_titleLabel.Text = "待部署";
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.9f));
		_titleLabel.AddThemeFontSizeOverride("font_size", 11);
		_titleLabel.Position = new Vector2(6f, 2f);
		_titleLabel.Size = new Vector2(50f, 16f);
		AddChild(_titleLabel);
	}

	// ─────────────── 底座槽位池 ───────────────

	/// <summary>
	/// 一次性创建全部底座槽位（空位），后续只挂载/卸载英雄模型
	/// </summary>
	private void BuildSlotBases()
	{
		for (int i = 0; i < GameConst.Game.BenchCapacity; i++)
		{
			var slot = new BenchSlotBase();
			slot.Position = GetSlotPosition(i);
			AddChild(slot);
			_slots.Add(slot);
			_slotHeroes[i] = null;
		}
	}

	/// <summary>
	/// 仅调整槽位位置，不重建节点
	/// </summary>
	public void UpdateLayout(Vector2 position, Vector2 size)
	{
		Position = position;
		AreaSize = size;

		float slotW = GameConst.Visual.BenchSlotSize.X;
		float slotGap = GameConst.Visual.BenchSlotGap;
		float padding = GameConst.Visual.BenchPadding;

		_cols = Mathf.Max(1, (int)((size.X - padding * 2f + slotGap) / (slotW + slotGap)));

		if (_bgRect != null)
			_bgRect.Size = size;

		// 仅重排位置
		for (int i = 0; i < _slots.Count; i++)
			_slots[i].Position = GetSlotPosition(i);
	}

	private Vector2 GetSlotPosition(int index)
	{
		float slotW = GameConst.Visual.BenchSlotSize.X;
		float slotH = GameConst.Visual.BenchSlotSize.Y;
		float slotGap = GameConst.Visual.BenchSlotGap;
		float padding = GameConst.Visual.BenchPadding;
		float titleH = GameConst.Visual.BenchTitleHeight;

		int col = index % _cols;
		int row = index / _cols;
		return new Vector2(
			padding + col * (slotW + slotGap),
			titleH + padding + row * (slotH + slotGap)
		);
	}

	/// <summary>
	/// 将全局坐标转换为 bench 槽位索引，返回 -1 表示不在任何槽位上
	/// </summary>
	private int GetSlotIndexAt(Vector2 globalPos)
	{
		Transform2D inv = GlobalTransform.AffineInverse();
		Vector2 local = inv * globalPos;

		for (int i = 0; i < _slots.Count; i++)
		{
			Vector2 sp = _slots[i].Position;
			Rect2 rect = new Rect2(sp, GameConst.Visual.BenchSlotSize);
			if (rect.HasPoint(local))
				return i;
		}
		return -1;
	}

	// ─────────────── 英雄模型管理 ───────────────

	/// <summary>
	/// 刷新显示：对比 bench 数据与当前槽位，挂载/卸载英雄模型
	/// </summary>
	public void RefreshDisplay()
	{
		// 拖拽中不刷新，避免干扰
		if (_draggingHero != null) return;

		var bench = _gameManager.Bench;

		for (int i = 0; i < GameConst.Game.BenchCapacity; i++)
		{
			Hero newHero = i < bench.Count ? bench[i] : null;
			Hero currentHero = _slotHeroes[i];

			if (newHero == currentHero)
				continue; // 无变化

			// 卸载旧英雄模型
			if (currentHero != null)
			{
				_UnbindHeroFromSlot(i, currentHero);
				_slotHeroes[i] = null;
			}

			// 挂载新英雄模型
			if (newHero != null)
			{
				_BindHeroToSlot(i, newHero);
				_slotHeroes[i] = newHero;
			}
		}
	}

	/// <summary>
	/// 将英雄节点作为子节点挂到槽位上（缩放展示）
	/// </summary>
	private void _BindHeroToSlot(int slotIndex, Hero hero)
	{
		var slot = _slots[slotIndex];

		// 英雄从当前父节点移到槽位下
		if (hero.GetParent() != null)
			hero.GetParent().RemoveChild(hero);
		slot.AddChild(hero);

		// 缩放到适合槽位的尺寸
		hero.Scale = new Vector2(GameConst.Visual.BenchHeroScale, GameConst.Visual.BenchHeroScale);
		hero.Position = new Vector2(
			GameConst.Visual.BenchSlotSize.X / 2f,
			GameConst.Visual.BenchSlotSize.Y / 2f + 4f
		);
		hero.Visible = true;

		// 英雄边框颜色（强化显示）
		slot.SetHeroAccent(true, hero.Data.HeroColor);
	}

	/// <summary>
	/// 将英雄从槽位卸下，归还到 HeroStorage
	/// </summary>
	private void _UnbindHeroFromSlot(int slotIndex, Hero hero)
	{
		var slot = _slots[slotIndex];

		slot.SetHeroAccent(false, Colors.White); // 清除英雄颜色

		// 仅当 hero 确实挂在对应 slot 下时才移除（防止从战场等错误位置偷走英雄）
		if (hero.GetParent() == slot)
		{
			slot.RemoveChild(hero);

			var storage = GetTree().Root.GetNode<Main>("Main").GetNode<Node>("HeroStorage");
			if (storage != null && IsInstanceValid(hero))
			{
				storage.AddChild(hero);
			}
		}

		hero.Scale = Vector2.One;
		hero.Position = Vector2.Zero;
	}

	// ─────────────── 输入处理 ───────────────

	/// <summary>
	/// 用 _Input 而非 _UnhandledInput，确保不被 CanvasLayer 上的 Control 拦截
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		// ── 拖拽中：跟踪鼠标移动和释放 ──
		if (_draggingHero != null)
		{
			if (@event is InputEventMouseMotion)
			{
				_draggingHero.GlobalPosition = GetGlobalMousePosition() + _dragOffset;
				return;
			}

			if (@event is InputEventMouseButton mb && !mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				_Drop();
				return;
			}
			return;
		}

		// ── 未拖拽：检测点击开始拖拽 / 右键出售 ──
		if (@event is not InputEventMouseButton mb2 || !mb2.Pressed)
			return;

		Vector2 mouseGlobal = GetGlobalMousePosition();
		int hitSlot = GetSlotIndexAt(mouseGlobal);
		if (hitSlot < 0 || _slotHeroes[hitSlot] == null) return;

		if (mb2.ButtonIndex == MouseButton.Right)
		{
			_gameManager.SellHero(_slotHeroes[hitSlot], true);
		}
		else if (mb2.ButtonIndex == MouseButton.Left)
		{
			_StartDrag(hitSlot);
		}
	}

	// ─────────────── 拖拽流程 ───────────────

	private void _StartDrag(int slotIndex)
	{
		Hero hero = _slotHeroes[slotIndex];
		if (hero == null || _dragLayer == null) return;

		_draggingHero = hero;
		_dragSlotIndex = slotIndex;

		// 计算鼠标到英雄中心的偏移（用于拖拽时保持鼠标相对位置）
		Vector2 heroCenter = _slots[slotIndex].GlobalPosition
			+ new Vector2(GameConst.Visual.BenchSlotSize.X / 2f, GameConst.Visual.BenchSlotSize.Y / 2f);
		_dragOffset = heroCenter - GetGlobalMousePosition() + DragCursorOffset;

		// 清除原槽位引用和视觉
		_slotHeroes[slotIndex] = null;
		_slots[slotIndex].SetHeroAccent(false, Colors.White);

		// 将英雄从槽位移到拖拽置顶层
		if (hero.GetParent() is BenchSlotBase slot)
			slot.RemoveChild(hero);
		_dragLayer.AddChild(hero);

		hero.Scale = new Vector2(GameConst.Visual.BenchHeroScale, GameConst.Visual.BenchHeroScale);
		hero.GlobalPosition = GetGlobalMousePosition() + _dragOffset;
	}

	private void _Drop()
	{
		if (_draggingHero == null) return;

		// 优先检测是否落在战场格子上
		Vector2 mouseGlobal = GetGlobalMousePosition();
		Vector2I cell = _battlefield.WorldToGrid(mouseGlobal);

		if (_battlefield.IsValidCell(cell.X, cell.Y))
		{
			if (_battlefield.IsCellEmpty(cell.X, cell.Y))
			{
				// ── 放置到战场空格 ──
				_battlefield.PlaceHero(_draggingHero, cell.X, cell.Y);
				_draggingHero.Scale = Vector2.One;
				_dragLayer.RemoveChild(_draggingHero);
				// PlaceHero 内部已处理 AddChild + 发信号
				_draggingHero = null;
				_dragSlotIndex = -1;
				return;
			}
		}

		// 检测是否落在 bench 槽位上
		int targetSlot = GetSlotIndexAt(mouseGlobal);
		if (targetSlot >= 0 && targetSlot != _dragSlotIndex)
		{
			if (_slotHeroes[targetSlot] != null)
			{
				// ── 交换：目标槽位有英雄 ──
				Hero targetHero = _slotHeroes[targetSlot];

				// 把被拖拽的英雄放到目标槽位
				_dragLayer.RemoveChild(_draggingHero);
				_slots[targetSlot].AddChild(_draggingHero);
				_draggingHero.Scale = new Vector2(GameConst.Visual.BenchHeroScale, GameConst.Visual.BenchHeroScale);
				_draggingHero.Position = new Vector2(
					GameConst.Visual.BenchSlotSize.X / 2f,
					GameConst.Visual.BenchSlotSize.Y / 2f + 4f
				);
				_slots[targetSlot].SetHeroAccent(true, _draggingHero.Data.HeroColor);
				_slotHeroes[targetSlot] = _draggingHero;

				// 把目标英雄放到原槽位
				_BindHeroToSlot(_dragSlotIndex, targetHero);
				_slotHeroes[_dragSlotIndex] = targetHero;

				// 通知数据层交换
				_gameManager.SwapBenchHeroes(_dragSlotIndex, targetSlot);

				_draggingHero = null;
				_dragSlotIndex = -1;
				return;
			}
			else if (targetSlot < _gameManager.Bench.Count)
			{
				// ── 移动：目标槽位为空且在有效范围内 ──
				_dragLayer.RemoveChild(_draggingHero);
				_slots[targetSlot].AddChild(_draggingHero);
				_draggingHero.Scale = new Vector2(GameConst.Visual.BenchHeroScale, GameConst.Visual.BenchHeroScale);
				_draggingHero.Position = new Vector2(
					GameConst.Visual.BenchSlotSize.X / 2f,
					GameConst.Visual.BenchSlotSize.Y / 2f + 4f
				);
				_slots[targetSlot].SetHeroAccent(true, _draggingHero.Data.HeroColor);
				_slotHeroes[targetSlot] = _draggingHero;

				_gameManager.SwapBenchHeroes(_dragSlotIndex, targetSlot);

				_draggingHero = null;
				_dragSlotIndex = -1;
				return;
			}
		}

		// ── 未命中任何目标 → 归还原槽位 ──
		_dragLayer.RemoveChild(_draggingHero);
		_BindHeroToSlot(_dragSlotIndex, _draggingHero);
		_slotHeroes[_dragSlotIndex] = _draggingHero;

		_draggingHero = null;
		_dragSlotIndex = -1;
	}
}

/// <summary>
/// 底座槽位 - 空位视觉 + 英雄边框颜色
/// 永久存在于场景中，不随英雄买卖而销毁/创建
/// </summary>
public partial class BenchSlotBase : Node2D
{
	/// <summary>槽位尺寸（从常量读取）</summary>
	private static Vector2 SlotSize => GameConst.Visual.BenchSlotSize;

	// 视觉子节点
	private ColorRect _bgRect;
	private ColorRect _borderTop;
	private ColorRect _borderBottom;
	private ColorRect _borderLeft;
	private ColorRect _borderRight;

	public override void _Ready()
	{
		_BuildVisual();
	}

	private void _BuildVisual()
	{
		Vector2 sz = SlotSize;
		float bw = 1.5f; // 边框宽度
		var emptyColor = new Color(0.08f, 0.13f, 0.22f, 0.5f);
		var borderColor = new Color(0.25f, 0.35f, 0.5f, 0.6f);

		// 空位背景
		_bgRect = new ColorRect();
		_bgRect.Size = sz;
		_bgRect.Color = emptyColor;
		AddChild(_bgRect);

		// 四边框
		_borderTop = new ColorRect() { Size = new Vector2(sz.X, bw), Position = Vector2.Zero, Color = borderColor };
		_borderBottom = new ColorRect() { Size = new Vector2(sz.X, bw), Position = new Vector2(0, sz.Y - bw), Color = borderColor };
		_borderLeft = new ColorRect() { Size = new Vector2(bw, sz.Y), Position = Vector2.Zero, Color = borderColor };
		_borderRight = new ColorRect() { Size = new Vector2(bw, sz.Y), Position = new Vector2(sz.X - bw, 0), Color = borderColor };
		AddChild(_borderTop);
		AddChild(_borderBottom);
		AddChild(_borderLeft);
		AddChild(_borderRight);
	}

	/// <summary>
	/// 设置英雄边框颜色
	/// </summary>
	/// <param name="hasHero">是否有英雄</param>
	/// <param name="heroColor">英雄颜色（hasHero=true 时使用）</param>
	public void SetHeroAccent(bool hasHero, Color heroColor)
	{
		if (!hasHero)
		{
			// 恢复空位
			var borderColor = new Color(0.25f, 0.35f, 0.5f, 0.6f);
			_borderTop.Color = borderColor;
			_borderBottom.Color = borderColor;
			_borderLeft.Color = borderColor;
			_borderRight.Color = borderColor;
			_bgRect.Color = new Color(0.08f, 0.13f, 0.22f, 0.5f);
		}
		else
		{
			_borderTop.Color = heroColor;
			_borderBottom.Color = heroColor;
			_borderLeft.Color = heroColor;
			_borderRight.Color = heroColor;
			_bgRect.Color = new Color(0.12f, 0.18f, 0.3f, 0.9f);
		}
	}
}
