using Godot;
using System.Collections.Generic;

/// <summary>
/// 商店UI - 展示商店英雄卡片，处理购买、刷新、锁定
/// 卡牌通过预制场景加载，代码只负责数据绑定
/// </summary>
public partial class ShopUI : Control
{
    private GameManager _gameManager;
    private HBoxContainer _slotsContainer;
    private Button _refreshBtn;
    private Button _lockBtn;

    /// <summary>英雄卡牌预制场景路径</summary>
    private const string HERO_CARD_SCENE = "res://Scenes/UI/HeroCard.tscn";
    /// <summary>空槽位预制场景路径</summary>
    private const string EMPTY_CARD_SCENE = "res://Scenes/UI/EmptyCard.tscn";

    private PackedScene _heroCardScene;
    private PackedScene _emptyCardScene;

    [Signal]
    public delegate void HeroBoughtEventHandler(int slotIndex);

    public override void _Ready()
    {
        _gameManager = GetTree().Root.GetNode<Main>("Main").GetNode<GameManager>("GameManager");
        _gameManager.ShopRefreshed += RefreshDisplay;
        _gameManager.StateChanged += OnStateChanged;

        // 预加载卡牌预制场景
        _heroCardScene = GD.Load<PackedScene>(HERO_CARD_SCENE);
        _emptyCardScene = GD.Load<PackedScene>(EMPTY_CARD_SCENE);

        // 从预制场景获取子节点
        _slotsContainer = GetNode<HBoxContainer>("SlotsContainer");
        _refreshBtn = GetNode<Button>("RefreshBtn");
        _lockBtn = GetNode<Button>("LockBtn");

        // 刷新按钮样式（场景中只有文字，按钮样式在代码中设置以保证一致性）
        ApplyButtonStyle(_refreshBtn, new Color(0.2f, 0.5f, 0.8f));
        ApplyButtonStyle(_lockBtn, new Color(0.5f, 0.3f, 0.1f));

        _refreshBtn.Pressed += () => _gameManager.RefreshShop(true);
        _lockBtn.Pressed += () =>
        {
            _gameManager.ToggleShopLock();
            UpdateLockButton();
        };

        RefreshDisplay();
    }

    public override void _Process(double delta)
    {
        // 响应尺寸变化，更新按钮位置
        float w = Size.X;

        if (_refreshBtn != null)
        {
            _refreshBtn.Position = new Vector2(w - 140, 28);
        }
        if (_lockBtn != null)
        {
            _lockBtn.Position = new Vector2(w - 140, 70);
        }
    }

    /// <summary>
    /// 为已有按钮应用样式（按钮本身从预制场景获取）
    /// </summary>
    private void ApplyButtonStyle(Button btn, Color color)
    {
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
    }

    private void UpdateLockButton()
    {
        _lockBtn.Text = _gameManager.IsShopLocked ? "🔒 已锁定" : "🔓 锁定";
    }

    /// <summary>
    /// 从预制场景实例化英雄卡牌并绑定数据
    /// 注意：必须先 AddChild 再 SetData，否则 HeroCard._Ready 未执行导致子节点引用为 null
    /// </summary>
    private HeroCard CreateHeroCard(HeroData data, int index)
    {
        var card = _heroCardScene.Instantiate<HeroCard>();
        int capturedIndex = index;
        card.Pressed += () =>
        {
            if (_gameManager.CurrentState != GameManager.GameState.Prepare) return;
            if (_gameManager.Bench.Count >= 8 || _gameManager.Gold < data.Price)
            {
                // 前置校验失败：卡牌抖动 + 调用 BuyHero 触发 ShowMessage Toast
                ShakeCard(card);
                _gameManager.BuyHero(capturedIndex);
                return;
            }
            _gameManager.BuyHero(capturedIndex);
        };
        return card;
    }

    /// <summary>
    /// 卡牌抖动反馈（购买失败时）
    /// </summary>
    private void ShakeCard(Control card)
    {
        if (card == null || !IsInstanceValid(card)) return;
        var tween = CreateTween();
        float origX = card.Position.X;
        tween.TweenProperty(card, "position:x", origX - 4, 0.05);
        tween.TweenProperty(card, "position:x", origX + 4, 0.05);
        tween.TweenProperty(card, "position:x", origX - 3, 0.05);
        tween.TweenProperty(card, "position:x", origX + 3, 0.05);
        tween.TweenProperty(card, "position:x", origX, 0.05);
    }

    public void RefreshDisplay()
    {
        // 清空旧卡牌
        foreach (var child in _slotsContainer.GetChildren())
            child.QueueFree();

        var slots = _gameManager.GetShopSlots();
        for (int i = 0; i < 5; i++)
        {
            if (i < slots.Count)
            {
                var heroCard = CreateHeroCard(slots[i], i);
                _slotsContainer.AddChild(heroCard);
                // AddChild 后 _Ready 已执行，子节点引用已初始化，可以安全绑定数据
                heroCard.SetData(slots[i], i);
            }
            else
            {
                _slotsContainer.AddChild(CreateEmptyCard());
            }
        }
    }

    /// <summary>
    /// 从预制场景实例化空槽位
    /// </summary>
    private Control CreateEmptyCard()
    {
        return _emptyCardScene.Instantiate<ColorRect>();
    }

    private void OnStateChanged(int state)
    {
        bool isPrepare = state == (int)GameManager.GameState.Prepare;
        _refreshBtn.Disabled = !isPrepare;
        _lockBtn.Disabled = !isPrepare;
    }
}
