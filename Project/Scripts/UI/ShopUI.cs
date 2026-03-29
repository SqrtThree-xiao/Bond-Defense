using Godot;
using System.Collections.Generic;

/// <summary>
/// 商店UI - 展示商店英雄卡片，处理购买、刷新、锁定
/// 支持自适应宽度
/// </summary>
public partial class ShopUI : Control
{
    private GameManager _gameManager;
    private HBoxContainer _slotsContainer;
    private Button _refreshBtn;
    private Button _lockBtn;

    [Signal]
    public delegate void HeroBoughtEventHandler(int slotIndex);

    public override void _Ready()
    {
        _gameManager = GetTree().Root.GetNode<Main>("Main").GetNode<GameManager>("GameManager");
        _gameManager.ShopRefreshed += RefreshDisplay;
        _gameManager.StateChanged += OnStateChanged;

        BuildUI();
        RefreshDisplay();
    }

    private void BuildUI()
    {
        // 商店面板背景
        var bg = new ColorRect();
        bg.Color = new Color(0.08f, 0.12f, 0.2f, 0.95f);
        bg.Name = "ShopBg";
        AddChild(bg);

        // 标题
        var title = new Label();
        title.Text = "商 店";
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        title.Position = new Vector2(10, 5);
        AddChild(title);

        // 英雄槽位容器
        _slotsContainer = new HBoxContainer();
        _slotsContainer.Position = new Vector2(10, 28);
        _slotsContainer.AddThemeConstantOverride("separation", 8);
        AddChild(_slotsContainer);

        // 刷新按钮
        _refreshBtn = MakeButton("刷新(2金)", new Color(0.2f, 0.5f, 0.8f));
        _refreshBtn.Size = new Vector2(120, 35);
        _refreshBtn.Pressed += () => _gameManager.RefreshShop(true);
        AddChild(_refreshBtn);

        // 锁定按钮
        _lockBtn = MakeButton("🔓 锁定", new Color(0.5f, 0.3f, 0.1f));
        _lockBtn.Size = new Vector2(120, 35);
        _lockBtn.Pressed += () =>
        {
            _gameManager.ToggleShopLock();
            UpdateLockButton();
        };
        AddChild(_lockBtn);
    }

    public override void _Process(double delta)
    {
        // 响应尺寸变化，更新按钮位置
        float w = Size.X;
        float h = Size.Y;

        var bg = GetNodeOrNull<ColorRect>("ShopBg");
        if (bg != null) bg.Size = new Vector2(w, h);

        if (_refreshBtn != null)
        {
            _refreshBtn.Position = new Vector2(w - 140, 28);
        }
        if (_lockBtn != null)
        {
            _lockBtn.Position = new Vector2(w - 140, 70);
        }
    }

    private Button MakeButton(string text, Color color)
    {
        var btn = new Button();
        btn.Text = text;
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeFontSizeOverride("font_size", 12);
        var style = new StyleBoxFlat();
        style.BgColor = color;
        style.CornerRadiusTopLeft = 4;
        style.CornerRadiusTopRight = 4;
        style.CornerRadiusBottomLeft = 4;
        style.CornerRadiusBottomRight = 4;
        btn.AddThemeStyleboxOverride("normal", style);
        var hoverStyle = style.Duplicate() as StyleBoxFlat;
        hoverStyle.BgColor = color.Lightened(0.2f);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        return btn;
    }

    private void UpdateLockButton()
    {
        _lockBtn.Text = _gameManager.IsShopLocked ? "🔒 已锁定" : "🔓 锁定";
    }

    public void RefreshDisplay()
    {
        foreach (var child in _slotsContainer.GetChildren())
            child.QueueFree();

        var slots = _gameManager.GetShopSlots();
        for (int i = 0; i < 5; i++)
        {
            if (i < slots.Count)
                _slotsContainer.AddChild(CreateHeroCard(slots[i], i));
            else
                _slotsContainer.AddChild(CreateEmptyCard());
        }
    }

    private Control CreateHeroCard(HeroData data, int index)
    {
        var card = new Button();
        card.CustomMinimumSize = new Vector2(104, 78);
        card.FocusMode = Control.FocusModeEnum.None;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.15f, 0.2f, 0.35f);
        style.BorderColor = data.HeroColor;
        style.BorderWidthBottom = 2;
        style.BorderWidthTop = 2;
        style.BorderWidthLeft = 2;
        style.BorderWidthRight = 2;
        style.CornerRadiusTopLeft = 5;
        style.CornerRadiusTopRight = 5;
        style.CornerRadiusBottomLeft = 5;
        style.CornerRadiusBottomRight = 5;
        card.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = style.Duplicate() as StyleBoxFlat;
        hoverStyle.BgColor = new Color(0.25f, 0.35f, 0.55f);
        card.AddThemeStyleboxOverride("hover", hoverStyle);

        // 英雄颜色图标
        var icon = new ColorRect();
        icon.Color = data.HeroColor;
        icon.Size = new Vector2(32, 32);
        icon.Position = new Vector2(36, 8);
        card.AddChild(icon);

        // 稀有度星星
        var rarityLabel = new Label();
        rarityLabel.Text = new string('★', data.Rarity);
        rarityLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        rarityLabel.AddThemeFontSizeOverride("font_size", 9);
        rarityLabel.Position = new Vector2(2, 3);
        card.AddChild(rarityLabel);

        // 英雄名称
        var nameLabel = new Label();
        nameLabel.Text = data.HeroName;
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        nameLabel.AddThemeFontSizeOverride("font_size", 11);
        nameLabel.Position = new Vector2(0, 42);
        nameLabel.Size = new Vector2(104, 16);
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        card.AddChild(nameLabel);

        // 标签
        var tagLabel = new Label();
        tagLabel.Text = string.Join(" ", data.Tags);
        tagLabel.AddThemeFontSizeOverride("font_size", 9);
        tagLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 0.7f));
        tagLabel.Position = new Vector2(0, 57);
        tagLabel.Size = new Vector2(104, 14);
        tagLabel.HorizontalAlignment = HorizontalAlignment.Center;
        card.AddChild(tagLabel);

        // 价格
        var priceLabel = new Label();
        priceLabel.Text = $"💰{data.Price}";
        priceLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        priceLabel.AddThemeFontSizeOverride("font_size", 11);
        priceLabel.Position = new Vector2(74, 3);
        priceLabel.Size = new Vector2(30, 16);
        priceLabel.HorizontalAlignment = HorizontalAlignment.Right;
        card.AddChild(priceLabel);

        // Tooltip
        card.TooltipText = $"{data.HeroName}\n{string.Join(", ", data.Tags)}\n技能: {data.SkillName} - {data.SkillDescription}\n攻击: {data.BaseAttack}  攻速: {data.BaseAttackSpeed}  范围: {data.BaseRange}";

        int capturedIndex = index;
        card.Pressed += () =>
        {
            if (_gameManager.CurrentState == GameManager.GameState.Prepare)
                _gameManager.BuyHero(capturedIndex);
        };

        return card;
    }

    private Control CreateEmptyCard()
    {
        var card = new ColorRect();
        card.CustomMinimumSize = new Vector2(104, 78);
        card.Color = new Color(0.1f, 0.15f, 0.25f, 0.5f);
        return card;
    }

    private void OnStateChanged(int state)
    {
        bool isPrepare = state == (int)GameManager.GameState.Prepare;
        _refreshBtn.Disabled = !isPrepare;
        _lockBtn.Disabled = !isPrepare;
    }
}
