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

    public int SlotIndex { get; private set; } = -1;

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
}
