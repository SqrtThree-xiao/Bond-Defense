using Godot;
using System.Collections.Generic;

/// <summary>
/// 待部署区UI - 展示已购买英雄，支持拖拽到战场
/// 支持自适应宽度
/// </summary>
public partial class BenchUI : Control
{
    private GameManager _gameManager;
    private Battlefield _battlefield;
    private HBoxContainer _benchContainer;
    private Hero _draggingHero = null;
    private ColorRect _bgRect;

    public override void _Ready()
    {
        _gameManager = GetTree().Root.GetNode<Main>("Main").GetNode<GameManager>("GameManager");
        _battlefield = GetTree().Root.GetNode<Main>("Main").GetNode<Battlefield>("Battlefield");
        _gameManager.BenchChanged += RefreshDisplay;

        // 从预制场景获取子节点
        _bgRect = GetNode<ColorRect>("BgRect");
        _benchContainer = GetNode<HBoxContainer>("BenchContainer");
    }

    public override void _Process(double delta)
    {
        // 响应尺寸变化
        if (_bgRect != null) _bgRect.Size = Size;
    }

    public void RefreshDisplay()
    {
        foreach (var child in _benchContainer.GetChildren())
            child.QueueFree();

        var bench = _gameManager.Bench;
        for (int i = 0; i < 8; i++)
        {
            if (i < bench.Count)
                _benchContainer.AddChild(CreateHeroSlot(bench[i], i));
            else
                _benchContainer.AddChild(CreateEmptySlot());
        }
    }

    private Control CreateHeroSlot(Hero hero, int benchIndex)
    {
        var slot = new Panel();
        slot.CustomMinimumSize = new Vector2(76, 56);
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.15f, 0.22f, 0.38f);
        style.BorderColor = hero.Data.HeroColor;
        style.BorderWidthBottom = 2;
        style.BorderWidthTop = 2;
        style.BorderWidthLeft = 2;
        style.BorderWidthRight = 2;
        style.CornerRadiusTopLeft = 4;
        style.CornerRadiusTopRight = 4;
        style.CornerRadiusBottomLeft = 4;
        style.CornerRadiusBottomRight = 4;
        slot.AddThemeStyleboxOverride("panel", style);

        // 颜色圆点
        var icon = new ColorRect();
        icon.Color = hero.Data.HeroColor;
        icon.Size = new Vector2(24, 24);
        icon.Position = new Vector2(26, 6);
        slot.AddChild(icon);

        // 星级
        var star = new Label();
        star.Text = hero.Star switch { 1 => "★", 2 => "★★", 3 => "★★★", _ => "" };
        star.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f));
        star.AddThemeFontSizeOverride("font_size", 9);
        star.Position = new Vector2(0, 2);
        star.Size = new Vector2(76, 12);
        star.HorizontalAlignment = HorizontalAlignment.Center;
        slot.AddChild(star);

        // 名字
        var name = new Label();
        name.Text = hero.Data.HeroName;
        name.AddThemeColorOverride("font_color", Colors.White);
        name.AddThemeFontSizeOverride("font_size", 10);
        name.Position = new Vector2(0, 34);
        name.Size = new Vector2(76, 14);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        slot.AddChild(name);

        // 右键出售
        slot.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Right)
                {
                    _gameManager.SellHero(hero, true);
                }
                else if (mb.ButtonIndex == MouseButton.Left)
                {
                    // 开始拖拽到战场
                    StartDrag(hero);
                }
            }
        };

        slot.TooltipText = $"{hero.Data.HeroName} {hero.Star}★\n{string.Join(", ", hero.Data.Tags)}\n攻击:{hero.FinalAttack:F0} 攻速:{hero.FinalAttackSpeed:F1} 范围:{hero.FinalRange:F0}\n右键出售";
        slot.SetMeta("hero", hero);

        return slot;
    }

    private Control CreateEmptySlot()
    {
        var slot = new ColorRect();
        slot.CustomMinimumSize = new Vector2(76, 56);
        slot.Color = new Color(0.08f, 0.13f, 0.22f, 0.5f);
        return slot;
    }

    private void StartDrag(Hero hero)
    {
        _draggingHero = hero;
        // 先隐藏 bench 中被拖拽的卡牌（避免闪烁）
        foreach (var child in _benchContainer.GetChildren())
        {
            if (child.HasMeta("hero") && child.GetMeta("hero").AsGodotObject() == hero)
            {
                ((Control)child).Visible = false;
                break;
            }
        }
        // 再将英雄移入战场（先设坐标再显示，避免在原点闪烁一帧）
        _battlefield.StartDragFromBench(hero);
    }
}
