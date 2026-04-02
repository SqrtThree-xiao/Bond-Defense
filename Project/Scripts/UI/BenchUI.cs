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
	private int _dragSlotIndex = -1;       // 来源 bench 槽位（仅 bench 来源时有意义）
	private Vector2I? _dragBattlefieldCell; // 来源战场格子（仅战场来源时有意义）
	private Vector2 _dragOffset;
	private Node2D _dragLayer;             // 拖拽置顶层（挂在 SceneTree.Root）
	private bool _isDragFromBattlefield;   // true=从战场拖，false=从bench拖

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
				_UpdateHoverHighlights();
				return;
			}

			if (@event is InputEventMouseButton mb && !mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				_ClearHoverHighlights();
				_Drop();
				return;
			}
			return;
		}

		// ── 未拖拽：检测点击开始拖拽 / 右键出售 ──
		if (@event is not InputEventMouseButton mb2 || !mb2.Pressed)
			return;

		if (mb2.ButtonIndex == MouseButton.Right)
		{
			// 右键：先检测 bench 槽位，再检测战场英雄
			Vector2 mouseGlobal = GetGlobalMousePosition();
			int hitSlot = GetSlotIndexAt(mouseGlobal);
			if (hitSlot >= 0 && _slotHeroes[hitSlot] != null)
			{
				_gameManager.SellHero(_slotHeroes[hitSlot], true);
				return;
			}
			// 战场英雄右键出售（由 Battlefield 处理或在此处理）
			return;
		}

		if (mb2.ButtonIndex != MouseButton.Left) return;

		Vector2 mousePos = GetGlobalMousePosition();

		// 优先检测 bench 槽位点击
		int benchSlot = GetSlotIndexAt(mousePos);
		if (benchSlot >= 0 && _slotHeroes[benchSlot] != null)
		{
			_StartDragFromBench(benchSlot);
			return;
		}

		// 其次检测战场英雄点击（仅战斗准备阶段）
		if (_gameManager.CurrentState == GameManager.GameState.Prepare)
		{
			var heroAtPos = _battlefield.GetHeroAtWorldPos(mousePos);
			if (heroAtPos != null)
			{
				_StartDragFromBattlefield(heroAtPos);
				return;
			}
		}
	}

	// ─────────────── 悬停高亮 ───────────────

	private int _lastHoveredBenchSlot = -1;
	private Vector2I? _lastHoveredCell;

	/// <summary>
	/// 拖拽中实时更新目标位置的高亮反馈
	/// </summary>
	private void _UpdateHoverHighlights()
	{
		Vector2 mouseGlobal = GetGlobalMousePosition();

		// ── 战场格子高亮 ──
		var cell = _battlefield.WorldToGrid(mouseGlobal);
		if (_battlefield.IsValidCell(cell.X, cell.Y))
		{
			// 清除上一次的格子高亮
			if (_lastHoveredCell.HasValue && _lastHoveredCell.Value != cell)
			{
				var old = _lastHoveredCell.Value;
				_battlefield.HighlightCell(old.X, old.Y, Battlefield.HighlightType.None);
			}

			if (_isDragFromBattlefield)
			{
				// 从战场拖：空格可放，有英雄可交换，原位不高亮
				if (cell == _dragBattlefieldCell)
				{
					_battlefield.HighlightCell(cell.X, cell.Y, Battlefield.HighlightType.None);
				}
				else if (_battlefield.IsCellEmpty(cell.X, cell.Y))
					_battlefield.HighlightCell(cell.X, cell.Y, Battlefield.HighlightType.Valid);
				else
					_battlefield.HighlightCell(cell.X, cell.Y, Battlefield.HighlightType.Swap);
			}
			else
			{
				// 从 bench 拖：空格可放，有英雄不可放
				if (_battlefield.IsCellEmpty(cell.X, cell.Y))
					_battlefield.HighlightCell(cell.X, cell.Y, Battlefield.HighlightType.Valid);
				else
					_battlefield.HighlightCell(cell.X, cell.Y, Battlefield.HighlightType.Invalid);
			}

			_lastHoveredCell = cell;
		}
		else
		{
			// 鼠标不在战场区域，清除高亮
			if (_lastHoveredCell.HasValue)
			{
				var old = _lastHoveredCell.Value;
				_battlefield.HighlightCell(old.X, old.Y, Battlefield.HighlightType.None);
				_lastHoveredCell = null;
			}
		}

		// ── Bench 槽位高亮（与战场互斥：同位置不可能同时命中） ──
		int hoverSlot = GetSlotIndexAt(mouseGlobal);
		bool benchHoverChanged = (hoverSlot != _lastHoveredBenchSlot);

		if (benchHoverChanged)
		{
			// 清除旧的 bench 高亮
			if (_lastHoveredBenchSlot >= 0 && _lastHoveredBenchSlot < _slots.Count)
				_slots[_lastHoveredBenchSlot].SetHoverHighlight(BenchHighlightType.None);
		}

		// 设置新的 bench 高亮（仅在目标槽位有效时）
		if (hoverSlot >= 0 && hoverSlot != _dragSlotIndex)
		{
			BenchHighlightType benchHl = _CalcBenchHighlightType(hoverSlot);
			_slots[hoverSlot].SetHoverHighlight(benchHl);
		}

		if (benchHoverChanged)
			_lastHoveredBenchSlot = hoverSlot;
	}

	/// <summary>
	/// 根据拖拽来源和目标 bench 槽位状态，计算高亮类型
	/// </summary>
	private BenchHighlightType _CalcBenchHighlightType(int targetSlot)
	{
		bool targetHasHero = _slotHeroes[targetSlot] != null;
		bool benchFull = _gameManager.Bench.Count >= GameConst.Game.BenchCapacity;
		bool slotInBench = targetSlot < _gameManager.Bench.Count;

		if (_isDragFromBattlefield)
		{
			// 从战场拖回 bench
			if (targetHasHero)
			{
				// 目标有英雄：bench 未满时可交换，满了则 Invalid
				return benchFull ? BenchHighlightType.Invalid : BenchHighlightType.Swap;
			}
			else if (slotInBench)
			{
				// 空槽位且在 bench 范围内：可放置
				return BenchHighlightType.Valid;
			}
			else
			{
				// 超出 bench 当前长度（bench 已满）
				return BenchHighlightType.Invalid;
			}
		}
		else
		{
			// 从 bench 内部拖拽
			if (targetHasHero)
				return BenchHighlightType.Swap; // 可交换
			else if (slotInBench)
				return BenchHighlightType.Valid; // 可移动到空位
			else
				return BenchHighlightType.Invalid; // 超出范围
		}
	}

	private void _ClearHoverHighlights()
	{
		if (_lastHoveredCell.HasValue)
		{
			var old = _lastHoveredCell.Value;
			_battlefield.HighlightCell(old.X, old.Y, Battlefield.HighlightType.None);
			_lastHoveredCell = null;
		}
		if (_lastHoveredBenchSlot >= 0 && _lastHoveredBenchSlot < _slots.Count)
		{
			_slots[_lastHoveredBenchSlot].SetHoverHighlight(BenchHighlightType.None);
			_lastHoveredBenchSlot = -1;
		}
	}

	// ─────────────── 拖拽流程 ───────────────

	/// <summary>
	/// 从 bench 槽位开始拖拽
	/// </summary>
	private void _StartDragFromBench(int slotIndex)
	{
		Hero hero = _slotHeroes[slotIndex];
		if (hero == null || _dragLayer == null) return;

		_draggingHero = hero;
		_dragSlotIndex = slotIndex;
		_dragBattlefieldCell = null;
		_isDragFromBattlefield = false;

		// 计算鼠标到英雄中心的偏移
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

	/// <summary>
	/// 从战场格子开始拖拽（仅战斗准备阶段）
	/// </summary>
	private void _StartDragFromBattlefield(Hero hero)
	{
		if (hero == null || _dragLayer == null) return;

		var cell = _battlefield.FindHeroCell(hero);
		if (cell == null) return;

		_draggingHero = hero;
		_dragSlotIndex = -1;
		_dragBattlefieldCell = cell;
		_isDragFromBattlefield = true;

		// 计算偏移
		Vector2 heroWorldPos = _battlefield.GridToWorld(cell.Value.X, cell.Value.Y);
		_dragOffset = heroWorldPos - GetGlobalMousePosition() + DragCursorOffset;

		// 从战场格子移除（RemoveHeroFromCell 只清空 grid 数据，需手动 reparent）
		_battlefield.RemoveHeroFromCell(cell.Value.X, cell.Value.Y);
		if (hero.GetParent() != null)
			hero.GetParent().RemoveChild(hero);
		_dragLayer.AddChild(hero);

		hero.GlobalPosition = GetGlobalMousePosition() + _dragOffset;
	}

	private void _Drop()
	{
		if (_draggingHero == null) return;

		Vector2 mouseGlobal = GetGlobalMousePosition();
		Vector2I cell = _battlefield.WorldToGrid(mouseGlobal);

		// ─────────────────────────────────────────
		// 优先级1：检测是否落在战场有效格子上
		// ─────────────────────────────────────────
		if (_battlefield.IsValidCell(cell.X, cell.Y))
		{
			if (_isDragFromBattlefield)
			{
				// 从战场拖 → 战场
				if (cell == _dragBattlefieldCell)
				{
					// 拖回原位
					_battlefield.PlaceHero(_draggingHero, cell.X, cell.Y);
					_EndDrag();
					return;
				}
				else if (_battlefield.IsCellEmpty(cell.X, cell.Y))
				{
					// 移动到空格
					_battlefield.PlaceHero(_draggingHero, cell.X, cell.Y);
					_EndDrag();
					return;
				}
				else
				{
					// 与目标格子英雄交换
					// RemoveHeroFromCell 只清 grid，target 仍挂在 Battlefield 下
					// PlaceHero 内部会 hero.GetParent()?.RemoveChild + AddChild
					var target = _battlefield.RemoveHeroFromCell(cell.X, cell.Y);
					_battlefield.PlaceHero(_draggingHero, cell.X, cell.Y);
					if (target != null && _dragBattlefieldCell.HasValue)
					{
						// 原格已空（_StartDragFromBattlefield 时清的），直接放置
						_battlefield.PlaceHero(target, _dragBattlefieldCell.Value.X, _dragBattlefieldCell.Value.Y);
					}
					_EndDrag();
					return;
				}
			}
			else
			{
				// 从 bench 拖 → 战场
				if (_battlefield.IsCellEmpty(cell.X, cell.Y))
				{
					// PlaceHero 内部会从 _dragLayer RemoveChild 再 AddChild 到战场
					// PlaceHero 触发 HeroPlaced → OnHeroPlaced → bench.Remove + BenchChanged
					_battlefield.PlaceHero(_draggingHero, cell.X, cell.Y);
					_draggingHero = null;
					_dragSlotIndex = -1;
					// _EndDrag 会尝试从 _dragLayer 移除（英雄已不在了，安全跳过）
					_EndDrag();
					return;
				}
				// 目标格子有英雄，不放置（继续检测 bench）
			}
		}

		// ─────────────────────────────────────────
		// 优先级2：检测是否落在 bench 槽位上
		// ─────────────────────────────────────────
		int targetSlot = GetSlotIndexAt(mouseGlobal);
		if (targetSlot >= 0)
		{
			if (_isDragFromBattlefield)
			{
				// 从战场拖 → bench：归还到 bench
				bool benchFull = _gameManager.Bench.Count >= GameConst.Game.BenchCapacity;

				if (benchFull)
				{
					// bench 满了，无法归还，走优先级3归回原格
				}
				else if (_slotHeroes[targetSlot] != null)
				{
					// 目标 bench 槽位有英雄：不允许交换（战场英雄和 bench 英雄类型不同，交换无意义）
					// 走优先级3归回原格
				}
				else if (targetSlot < _gameManager.Bench.Count)
				{
					// 放到空 bench 槽位（在有效范围内）
					// 英雄当前在 _dragLayer 下，手动 reparent 到 bench slot
					_FinishDragToBenchSlot(targetSlot);
					return;
				}
				else
				{
					// targetSlot 超出 bench 当前长度，追加到末尾
					_FinishDragToBenchSlot(_gameManager.Bench.Count);
					return;
				}
			}
			else
			{
				// 从 bench 拖 → bench
				if (targetSlot == _dragSlotIndex)
				{
					// 拖回原位
					_FinishDragToBenchSlot(_dragSlotIndex);
					return;
				}
				else if (_slotHeroes[targetSlot] != null)
				{
					// 交换两个 bench 槽位的英雄
					Hero targetHero = _slotHeroes[targetSlot];

					// 把拖拽中的英雄放到目标槽位
					_dragLayer.RemoveChild(_draggingHero);
					_slots[targetSlot].AddChild(_draggingHero);
					_ApplyBenchHeroStyle(_draggingHero);
					_slots[targetSlot].SetHeroAccent(true, _draggingHero.Data.HeroColor);
					_slotHeroes[targetSlot] = _draggingHero;

					// 把目标英雄放到原槽位
					_BindHeroToSlot(_dragSlotIndex, targetHero);
					_slotHeroes[_dragSlotIndex] = targetHero;

					_gameManager.SwapBenchHeroes(_dragSlotIndex, targetSlot);
					_draggingHero = null;
					_EndDrag();
					return;
				}
				else if (targetSlot < _gameManager.Bench.Count)
				{
					// 移动到空 bench 槽位
					_dragLayer.RemoveChild(_draggingHero);
					_slots[targetSlot].AddChild(_draggingHero);
					_ApplyBenchHeroStyle(_draggingHero);
					_slots[targetSlot].SetHeroAccent(true, _draggingHero.Data.HeroColor);
					_slotHeroes[targetSlot] = _draggingHero;

					_gameManager.SwapBenchHeroes(_dragSlotIndex, targetSlot);
					_draggingHero = null;
					_EndDrag();
					return;
				}
			}
		}

		// ─────────────────────────────────────────
		// 优先级3：未命中任何有效目标 → 归还原位
		// ─────────────────────────────────────────
		if (_isDragFromBattlefield && _dragBattlefieldCell.HasValue)
		{
			// 战场英雄归还原格子（PlaceHero 内部处理 reparent）
			_battlefield.PlaceHero(_draggingHero, _dragBattlefieldCell.Value.X, _dragBattlefieldCell.Value.Y);
		}
		else
		{
			// bench 英雄归还原槽位
			_FinishDragToBenchSlot(_dragSlotIndex);
		}

		_EndDrag();
	}

	/// <summary>
	/// 将正在拖拽的英雄放到指定 bench 槽位（从 _dragLayer reparent）
	/// 处理战场→bench 和 bench→原位 等场景
	/// </summary>
	private void _FinishDragToBenchSlot(int slotIndex)
	{
		if (_draggingHero == null) return;

		// 从 _dragLayer 移除，挂到 bench slot
		if (_draggingHero.GetParent() == _dragLayer)
			_dragLayer.RemoveChild(_draggingHero);

		_BindHeroToSlot(slotIndex, _draggingHero);
		_slotHeroes[slotIndex] = _draggingHero;

		// 如果是战场→bench 归还，需要手动加入 bench 列表并触发信号
		if (_isDragFromBattlefield && !_gameManager.BenchAny(b => b == _draggingHero))
		{
			_gameManager.ForceAddToBench(_draggingHero);
		}

		_draggingHero.Scale = new Vector2(GameConst.Visual.BenchHeroScale, GameConst.Visual.BenchHeroScale);
		_draggingHero = null;
		_dragSlotIndex = -1;
	}

	/// <summary>
	/// 设置英雄在 bench 槽位中的缩放和位置
	/// </summary>
	private void _ApplyBenchHeroStyle(Hero hero)
	{
		hero.Scale = new Vector2(GameConst.Visual.BenchHeroScale, GameConst.Visual.BenchHeroScale);
		hero.Position = new Vector2(
			GameConst.Visual.BenchSlotSize.X / 2f,
			GameConst.Visual.BenchSlotSize.Y / 2f + 4f
		);
	}

	/// <summary>
	/// 结束拖拽，重置所有状态
	/// </summary>
	private void _EndDrag()
	{
		if (_draggingHero != null)
		{
			if (_draggingHero.GetParent() == _dragLayer)
				_dragLayer.RemoveChild(_draggingHero);
			_draggingHero = null;
		}
		_dragSlotIndex = -1;
		_dragBattlefieldCell = null;
		_isDragFromBattlefield = false;
	}
}

/// <summary>
/// 底座槽位 - 空位视觉 + 英雄边框颜色
/// 永久存在于场景中，不随英雄买卖而销毁/创建
/// </summary>
/// <summary>
/// Bench 槽位悬停高亮类型（与 Battlefield.HighlightType 对齐）
/// </summary>
public enum BenchHighlightType
{
	None,       // 恢复正常显示
	Valid,      // 绿色：可放置（空槽位）
	Swap,       // 黄色：可交换（有英雄的槽位）
	Invalid     // 红色：不可放置（bench 满等）
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

	// 状态
	private bool _hasHero;
	private Color _heroColor;
	private BenchHighlightType _hoverType;

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
		_hasHero = hasHero;
		_heroColor = heroColor;
		_hoverType = BenchHighlightType.None;
		_ApplyVisuals();
	}

	/// <summary>
	/// 设置拖拽悬停高亮（支持多种高亮类型）
	/// </summary>
	public void SetHoverHighlight(BenchHighlightType type)
	{
		_hoverType = type;
		_ApplyVisuals();
	}

	/// <summary>
	/// 统一视觉刷新：根据 _hasHero + _hoverType 决定最终颜色
	/// </summary>
	private void _ApplyVisuals()
	{
		// 悬停优先级高于英雄颜色
		if (_hoverType != BenchHighlightType.None)
		{
			switch (_hoverType)
			{
				case BenchHighlightType.Valid:
					_borderTop.Color = new Color(0.3f, 0.8f, 0.4f, 1f);
					_borderBottom.Color = new Color(0.3f, 0.8f, 0.4f, 1f);
					_borderLeft.Color = new Color(0.3f, 0.8f, 0.4f, 1f);
					_borderRight.Color = new Color(0.3f, 0.8f, 0.4f, 1f);
					_bgRect.Color = new Color(0.1f, 0.25f, 0.15f, 0.9f);
					break;
				case BenchHighlightType.Swap:
					_borderTop.Color = new Color(0.85f, 0.8f, 0.25f, 1f);
					_borderBottom.Color = new Color(0.85f, 0.8f, 0.25f, 1f);
					_borderLeft.Color = new Color(0.85f, 0.8f, 0.25f, 1f);
					_borderRight.Color = new Color(0.85f, 0.8f, 0.25f, 1f);
					_bgRect.Color = new Color(0.2f, 0.18f, 0.08f, 0.9f);
					break;
				case BenchHighlightType.Invalid:
					_borderTop.Color = new Color(0.8f, 0.3f, 0.3f, 1f);
					_borderBottom.Color = new Color(0.8f, 0.3f, 0.3f, 1f);
					_borderLeft.Color = new Color(0.8f, 0.3f, 0.3f, 1f);
					_borderRight.Color = new Color(0.8f, 0.3f, 0.3f, 1f);
					_bgRect.Color = new Color(0.2f, 0.08f, 0.08f, 0.9f);
					break;
			}
			return;
		}

		// 正常状态
		if (_hasHero)
		{
			_borderTop.Color = _heroColor;
			_borderBottom.Color = _heroColor;
			_borderLeft.Color = _heroColor;
			_borderRight.Color = _heroColor;
			_bgRect.Color = new Color(0.12f, 0.18f, 0.3f, 0.9f);
		}
		else
		{
			var borderColor = new Color(0.25f, 0.35f, 0.5f, 0.6f);
			_borderTop.Color = borderColor;
			_borderBottom.Color = borderColor;
			_borderLeft.Color = borderColor;
			_borderRight.Color = borderColor;
			_bgRect.Color = new Color(0.08f, 0.13f, 0.22f, 0.5f);
		}
	}
}
