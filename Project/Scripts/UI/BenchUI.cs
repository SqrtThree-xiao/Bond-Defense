using Godot;
using System.Collections.Generic;

/// <summary>
/// 待部署区 - 场景层 Node2D
/// 
/// 架构：
///   BenchSlotBase（底座槽位）— 永久存在，提供空位视觉和命中区域
///   Hero 模型（英雄预制） — 购买后动态挂载到槽位上，卖出/放置后卸载
///   
/// 职责分离：
///   - 底座槽位：视觉占位 + 矩形命中（用于拖拽/出售交互）
///   - 英雄模型：从 HeroStorage 缩放引用，展示英雄外观和属性
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

    public override void _Ready()
    {
        _gameManager = GetTree().Root.GetNode<Main>("Main").GetNode<GameManager>("GameManager");
        _battlefield = GetTree().Root.GetNode<Main>("Main").GetNode<Battlefield>("Battlefield");
        _gameManager.BenchChanged += RefreshDisplay;

        BuildBackground();
        BuildSlotBases();
        RefreshDisplay();
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

    // ─────────────── 英雄模型管理 ───────────────

    /// <summary>
    /// 刷新显示：对比 bench 数据与当前槽位，挂载/卸载英雄模型
    /// </summary>
    public void RefreshDisplay()
    {
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

        // 英雄从 HeroStorage 暂时移到槽位下（不改变 ownership）
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

        // 从槽位移除，归还 HeroStorage
        if (hero.GetParent() == slot)
            slot.RemoveChild(hero);

        var storage = _battlefield.HeroStorage;
        if (storage != null && !IsInstanceValid(hero))
        {
            // 英雄已被 QueueFree，无需归还
        }
        else if (storage != null)
        {
            storage.AddChild(hero);
        }

        hero.Scale = Vector2.One;
        hero.Position = Vector2.Zero;
    }

    // ─────────────── 输入处理 ───────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mb || !mb.Pressed)
            return;

        // 全局鼠标 → BenchUI 本地坐标
        Transform2D inv = GlobalTransform.AffineInverse();
        Vector2 local = inv * GetGlobalMousePosition();

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slotHeroes[i] == null) continue;

            Vector2 sp = _slots[i].Position;
            Vector2 slotSize = GameConst.Visual.BenchSlotSize;
            Rect2 rect = new Rect2(sp, slotSize);

            if (rect.HasPoint(local))
            {
                if (mb.ButtonIndex == MouseButton.Right)
                    _gameManager.SellHero(_slotHeroes[i], true);
                else if (mb.ButtonIndex == MouseButton.Left)
                    StartDrag(_slotHeroes[i], i);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    private void StartDrag(Hero hero, int slotIndex)
    {
        // 隐藏该槽位的英雄模型
        hero.Visible = false;

        // 开始拖拽（Battlefield 会创建拖拽预览）
        _battlefield.StartDragFromBench(hero);
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
