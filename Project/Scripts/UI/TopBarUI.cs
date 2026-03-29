using Godot;
using System.Collections.Generic;

/// <summary>
/// 顶部状态栏UI - 显示金币、波次、生命值
/// </summary>
public partial class TopBarUI : Control
{
    private GameManager _gameManager;
    private Label _goldLabel;
    private Label _lifeLabel;
    private Label _waveLabel;
    private Button _startBtn;
    private Label _stateLabel;

    public override void _Ready()
    {
        _gameManager = GetTree().Root.GetNode<Main>("Main").GetNode<GameManager>("GameManager");
        _gameManager.GoldChanged += (g) => { if (_goldLabel != null) _goldLabel.Text = $"💰 {g}"; };
        _gameManager.LifeChanged += (l) => { if (_lifeLabel != null) UpdateLife(l); };
        _gameManager.WaveChanged += (w, max) => { if (_waveLabel != null) _waveLabel.Text = $"波次 {w}/{max}"; };
        _gameManager.StateChanged += OnStateChanged;
        _gameManager.ShowMessage += ShowToast;
        _gameManager.GameOver += OnGameOver;
        _gameManager.MergeAvailable += OnMergeAvailable;

        BuildUI();
    }

    private void BuildUI()
    {
        // 背景
        var bg = new ColorRect();
        bg.Color = new Color(0.05f, 0.08f, 0.15f, 0.95f);
        bg.Size = new Vector2(900, 48);
        AddChild(bg);

        // 分隔线
        var sep = new ColorRect();
        sep.Color = new Color(0.3f, 0.4f, 0.6f, 0.5f);
        sep.Size = new Vector2(900, 1);
        sep.Position = new Vector2(0, 47);
        AddChild(sep);

        // 金币
        var goldIcon = new Label();
        goldIcon.Text = "💰";
        goldIcon.AddThemeFontSizeOverride("font_size", 20);
        goldIcon.Position = new Vector2(14, 10);
        AddChild(goldIcon);

        _goldLabel = new Label();
        _goldLabel.Text = $"💰 {_gameManager.Gold}";
        _goldLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.3f));
        _goldLabel.AddThemeFontSizeOverride("font_size", 18);
        _goldLabel.Position = new Vector2(10, 10);
        _goldLabel.Size = new Vector2(100, 28);
        AddChild(_goldLabel);

        // 波次
        _waveLabel = new Label();
        _waveLabel.Text = $"波次 {_gameManager.Wave}/{_gameManager.MaxWave}";
        _waveLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 1f));
        _waveLabel.AddThemeFontSizeOverride("font_size", 16);
        _waveLabel.Position = new Vector2(390, 12);
        _waveLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _waveLabel.Size = new Vector2(120, 24);
        AddChild(_waveLabel);

        // 生命值
        _lifeLabel = new Label();
        _lifeLabel.Text = $"❤ {_gameManager.Life}";
        _lifeLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.4f));
        _lifeLabel.AddThemeFontSizeOverride("font_size", 18);
        _lifeLabel.Position = new Vector2(800, 10);
        _lifeLabel.Size = new Vector2(80, 28);
        AddChild(_lifeLabel);

        // 开始战斗按钮
        _startBtn = new Button();
        _startBtn.Text = "开始战斗 ▶";
        _startBtn.AddThemeColorOverride("font_color", Colors.White);
        _startBtn.AddThemeFontSizeOverride("font_size", 14);
        _startBtn.Size = new Vector2(130, 36);
        _startBtn.Position = new Vector2(620, 6);
        var btnStyle = new StyleBoxFlat();
        btnStyle.BgColor = new Color(0.2f, 0.6f, 0.25f);
        btnStyle.CornerRadiusTopLeft = 6;
        btnStyle.CornerRadiusTopRight = 6;
        btnStyle.CornerRadiusBottomLeft = 6;
        btnStyle.CornerRadiusBottomRight = 6;
        _startBtn.AddThemeStyleboxOverride("normal", btnStyle);
        var hoverStyle = btnStyle.Duplicate() as StyleBoxFlat;
        hoverStyle.BgColor = new Color(0.3f, 0.75f, 0.35f);
        _startBtn.AddThemeStyleboxOverride("hover", hoverStyle);
        _startBtn.Pressed += () => _gameManager.StartBattle();
        AddChild(_startBtn);

        // 状态标签
        _stateLabel = new Label();
        _stateLabel.Text = "准备阶段";
        _stateLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1f, 0.5f));
        _stateLabel.AddThemeFontSizeOverride("font_size", 13);
        _stateLabel.Position = new Vector2(200, 13);
        AddChild(_stateLabel);
    }

    private void UpdateLife(int life)
    {
        _lifeLabel.Text = $"❤ {life}";
        _lifeLabel.AddThemeColorOverride("font_color",
            life > 10 ? new Color(1f, 0.3f, 0.4f) :
            life > 5 ? new Color(1f, 0.6f, 0.1f) :
            new Color(1f, 0.1f, 0.1f));
    }

    private void OnStateChanged(int state)
    {
        var gs = (GameManager.GameState)state;
        switch (gs)
        {
            case GameManager.GameState.Prepare:
                _startBtn.Visible = true;
                _startBtn.Disabled = false;
                _stateLabel.Text = "✅ 准备阶段";
                _stateLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1f, 0.5f));
                break;
            case GameManager.GameState.Battle:
                _startBtn.Visible = false;
                _stateLabel.Text = "⚔ 战斗中...";
                _stateLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.3f));
                break;
            case GameManager.GameState.GameOver:
                _startBtn.Visible = false;
                break;
        }
    }

    // ─────────────────────── Toast 提示 ───────────────────────
    private Label _toastLabel;
    private float _toastTimer = 0f;

    public void ShowToast(string message)
    {
        if (_toastLabel == null)
        {
            _toastLabel = new Label();
            _toastLabel.AddThemeFontSizeOverride("font_size", 14);
            _toastLabel.AddThemeColorOverride("font_color", Colors.White);
            _toastLabel.Position = new Vector2(300, 60);
            _toastLabel.Size = new Vector2(300, 30);
            _toastLabel.HorizontalAlignment = HorizontalAlignment.Center;
            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(0f, 0f, 0f, 0.75f);
            bg.CornerRadiusTopLeft = 5;
            bg.CornerRadiusTopRight = 5;
            bg.CornerRadiusBottomLeft = 5;
            bg.CornerRadiusBottomRight = 5;
            GetParent().AddChild(_toastLabel);
        }

        _toastLabel.Text = message;
        _toastLabel.Modulate = Colors.White;
        _toastTimer = 2.5f;
    }

    public override void _Process(double delta)
    {
        if (_toastTimer > 0 && _toastLabel != null)
        {
            _toastTimer -= (float)delta;
            if (_toastTimer <= 0.5f)
            {
                float alpha = _toastTimer / 0.5f;
                _toastLabel.Modulate = new Color(1, 1, 1, alpha);
            }
        }
    }

    private void OnMergeAvailable(string heroName)
    {
        ShowToast($"✨ {heroName} 可以升星！(点击合成)");
    }

    private void OnGameOver(bool win)
    {
        var overlay = new ColorRect();
        overlay.Color = new Color(0f, 0f, 0f, 0.7f);
        overlay.Size = new Vector2(900, 700);
        overlay.ZIndex = 100;
        GetParent().AddChild(overlay);

        var msg = new Label();
        msg.Text = win ? "🎉 胜 利！\n成功守住了所有波次！" : "💀 失 败！\n城池已被攻破...";
        msg.AddThemeFontSizeOverride("font_size", 36);
        msg.AddThemeColorOverride("font_color", win ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.3f, 0.3f));
        msg.Position = new Vector2(280, 280);
        msg.Size = new Vector2(340, 120);
        msg.HorizontalAlignment = HorizontalAlignment.Center;
        msg.ZIndex = 101;
        GetParent().AddChild(msg);
    }
}
