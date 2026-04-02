using Godot;

/// <summary>
/// 商店英雄卡牌 - 从预制场景加载，通过 SetData() 绑定 HeroData
/// 布局和基础样式在 tscn 中定义，动态数据和边框色在代码中设置
/// </summary>
[GlobalClass]
public partial class HeroCard : Button
{
    private ColorRect _iconRect;
    private Label _rarityLabel;
    private Label _nameLabel;
    private Label _tagLabel;
    private Label _priceLabel;
    private Label _soldOutLabel;

    public int SlotIndex { get; private set; } = -1;

    /// <summary>是否已售罄</summary>
    public bool IsSoldOut { get; private set; } = false;

    public override void _Ready()
    {
        // 通过路径获取子节点（不依赖 [Export] 绑定）
        _iconRect = GetNode<ColorRect>("IconRect");
        _rarityLabel = GetNode<Label>("RarityLabel");
        _nameLabel = GetNode<Label>("NameLabel");
        _tagLabel = GetNode<Label>("TagLabel");
        _priceLabel = GetNode<Label>("PriceLabel");

        // 设置默认按钮样式（背景+边框，边框色在 SetData 中动态更新）
        ApplyDefaultStyle(new Color(0.15f, 0.2f, 0.35f), Colors.White);
    }

    public override void _GuiInput(InputEvent @event)
    {
        // 拦截鼠标拖拽事件，阻止传播到 Battlefield 的 _Input 拖拽系统
        // 不拦截点击事件，保留 Button.Pressed 信号正常工作
        if (@event is InputEventMouseMotion)
        {
            AcceptEvent();
        }
    }

    /// <summary>
    /// 绑定英雄数据到卡牌UI
    /// </summary>
    public void SetData(HeroData data, int index)
    {
        SlotIndex = index;

        // 更新边框色为英雄颜色
        UpdateBorderColor(data.HeroColor);

        if (_iconRect != null)
            _iconRect.Color = data.HeroColor;

        if (_rarityLabel != null)
            _rarityLabel.Text = new string('★', data.Rarity);

        if (_nameLabel != null)
            _nameLabel.Text = data.HeroName;

        if (_tagLabel != null)
            _tagLabel.Text = string.Join(" ", data.Tags);

        if (_priceLabel != null)
            _priceLabel.Text = $"💰{data.Price}";

        TooltipText = $"{data.HeroName}\n{string.Join(", ", data.Tags)}\n技能: {data.SkillName} - {data.SkillDescription}\n攻击: {data.BaseAttack}  攻速: {data.BaseAttackSpeed}  范围: {data.BaseRange}";
    }

    private void ApplyDefaultStyle(Color bgColor, Color borderColor)
    {
        int radius = 5;
        int border = 2;

        var style = new StyleBoxFlat();
        style.BgColor = bgColor;
        style.BorderColor = borderColor;
        style.BorderWidthBottom = border;
        style.BorderWidthTop = border;
        style.BorderWidthLeft = border;
        style.BorderWidthRight = border;
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
        AddThemeStyleboxOverride("normal", style);

        var hoverStyle = style.Duplicate() as StyleBoxFlat;
        hoverStyle.BgColor = new Color(0.25f, 0.35f, 0.55f);
        AddThemeStyleboxOverride("hover", hoverStyle);
    }

    /// <summary>
    /// 更新按钮边框颜色（保留其他样式）
    /// </summary>
    private void UpdateBorderColor(Color color)
    {
        if (GetThemeStylebox("normal") is StyleBoxFlat style)
        {
            style.BorderColor = color;
        }
        if (GetThemeStylebox("hover") is StyleBoxFlat hoverStyle)
        {
            hoverStyle.BorderColor = color;
        }
    }

    /// <summary>
    /// 设置售罄状态 - 灰化整体、禁用交互、显示"已售罄"覆盖
    /// </summary>
    public void SetSoldOut(bool soldOut)
    {
        IsSoldOut = soldOut;
        Disabled = soldOut;
        MouseDefaultCursorShape = soldOut ? CursorShape.Forbidden : CursorShape.PointingHand;

        if (soldOut)
        {
            // 整体灰化
            Modulate = new Color(0.4f, 0.4f, 0.4f, 0.7f);

            // 售罄覆盖标签
            if (_soldOutLabel == null)
            {
                _soldOutLabel = new Label();
                _soldOutLabel.Name = "SoldOutLabel";
                _soldOutLabel.Text = "已售罄";
                _soldOutLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f, 0.9f));
                _soldOutLabel.AddThemeFontSizeOverride("font_size", 12);
                _soldOutLabel.HorizontalAlignment = HorizontalAlignment.Center;
                _soldOutLabel.VerticalAlignment = VerticalAlignment.Center;
                _soldOutLabel.AnchorLeft = 0;
                _soldOutLabel.AnchorTop = 0;
                _soldOutLabel.AnchorRight = 1;
                _soldOutLabel.AnchorBottom = 1;
                AddChild(_soldOutLabel);
            }
            _soldOutLabel.Visible = true;
        }
        else
        {
            // 恢复正常
            Modulate = Colors.White;

            if (_soldOutLabel != null)
                _soldOutLabel.Visible = false;
        }
    }
}
