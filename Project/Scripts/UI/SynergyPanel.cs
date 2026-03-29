using Godot;
using System.Collections.Generic;

/// <summary>
/// 羁绊面板UI - 实时展示当前激活的羁绊
/// 支持自适应高度
/// </summary>
public partial class SynergyPanel : Control
{
    private SynergyManager _synergyManager;
    private VBoxContainer _listContainer;
    private ColorRect _bgRect;
    private ColorRect _sepRect;

    public override void _Ready()
    {
        Resized += OnResized;

        _synergyManager = GetTree().Root.GetNode<Main>("Main").GetNode<SynergyManager>("SynergyManager");
        _synergyManager.SynergiesUpdated += RefreshDisplay;

        // 从预制场景获取子节点
        _bgRect = GetNode<ColorRect>("BgRect");
        _sepRect = GetNode<ColorRect>("SepRect");
        _listContainer = GetNode<VBoxContainer>("ListContainer");

        RefreshDisplay();
    }

    private void OnResized()
    {
        // 更新背景和分隔线大小
        if (_bgRect != null) _bgRect.Size = Size;
        if (_sepRect != null)
        {
            _sepRect.Size = new Vector2(Size.X - 20, 1);
        }
        // 更新列表容器大小
        if (_listContainer != null)
        {
            _listContainer.Size = new Vector2(Size.X - 16, Size.Y - 40);
        }
    }

    public void RefreshDisplay()
    {
        foreach (var child in _listContainer.GetChildren())
            child.QueueFree();

        var synergies = _synergyManager.GetAllSynergiesWithCount();
        foreach (var (data, count, tier) in synergies)
        {
            _listContainer.AddChild(CreateSynergyRow(data, count, tier));
        }
    }

    private Control CreateSynergyRow(SynergyData data, int count, int tier)
    {
        float panelWidth = Size.X > 0 ? Size.X - 8 : 172;

        var row = new Panel();
        row.CustomMinimumSize = new Vector2(panelWidth, 42);

        var style = new StyleBoxFlat();
        bool active = tier > 0;
        style.BgColor = active
            ? new Color(data.SynergyColor.R * 0.3f, data.SynergyColor.G * 0.3f, data.SynergyColor.B * 0.3f, 0.9f)
            : new Color(0.1f, 0.13f, 0.2f, 0.7f);
        if (active)
        {
            style.BorderColor = data.SynergyColor;
            style.BorderWidthLeft = 3;
        }
        style.CornerRadiusTopLeft = 4;
        style.CornerRadiusTopRight = 4;
        style.CornerRadiusBottomLeft = 4;
        style.CornerRadiusBottomRight = 4;
        row.AddThemeStyleboxOverride("panel", style);

        // 颜色标记
        var dot = new ColorRect();
        dot.Color = active ? data.SynergyColor : new Color(0.3f, 0.3f, 0.3f);
        dot.Size = new Vector2(6, 34);
        dot.Position = new Vector2(4, 4);
        row.AddChild(dot);

        // 羁绊名称
        var nameLabel = new Label();
        nameLabel.Text = data.SynergyName;
        nameLabel.AddThemeColorOverride("font_color", active ? Colors.White : new Color(0.5f, 0.5f, 0.5f));
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        nameLabel.Position = new Vector2(14, 4);
        row.AddChild(nameLabel);

        // 计数指示
        var countLabel = new Label();
        var nextThreshold = GetNextThreshold(data, count);
        countLabel.Text = $"{count}/{nextThreshold}";
        countLabel.AddThemeColorOverride("font_color", active ? data.SynergyColor : new Color(0.5f, 0.5f, 0.5f));
        countLabel.AddThemeFontSizeOverride("font_size", 11);
        countLabel.Position = new Vector2(panelWidth - 50, 4);
        row.AddChild(countLabel);

        // 效果描述
        if (tier > 0)
        {
            var effectLabel = new Label();
            int effectIdx = Mathf.Min(tier - 1, data.EffectDescriptions.Length - 1);
            effectLabel.Text = data.EffectDescriptions[effectIdx];
            effectLabel.AddThemeColorOverride("font_color", data.SynergyColor.Lightened(0.3f));
            effectLabel.AddThemeFontSizeOverride("font_size", 9);
            effectLabel.Position = new Vector2(14, 23);
            effectLabel.Size = new Vector2(panelWidth - 20, 14);
            row.AddChild(effectLabel);
        }

        // Tooltip
        row.TooltipText = $"{data.SynergyName}\n{data.Description}\n" +
                          string.Join("\n", GetThresholdDescs(data));

        return row;
    }

    private int GetNextThreshold(SynergyData data, int count)
    {
        foreach (var t in data.Thresholds)
            if (count < t) return t;
        return data.Thresholds[^1];
    }

    private string[] GetThresholdDescs(SynergyData data)
    {
        var result = new string[data.Thresholds.Length];
        for (int i = 0; i < data.Thresholds.Length; i++)
        {
            var desc = i < data.EffectDescriptions.Length ? data.EffectDescriptions[i] : "";
            result[i] = $"({data.Thresholds[i]}人) {desc}";
        }
        return result;
    }
}
