using Godot;

/// <summary>
/// 主场景入口脚本 - 以代码方式构建整个游戏场景树
/// </summary>
public partial class Main : Node2D
{
    private SynergyManager _synergyManager;
    private GameManager _gameManager;
    private Battlefield _battlefield;

    public override void _Ready()
    {
        Name = "Main";
        BuildSceneTree();
    }

    private void BuildSceneTree()
    {
        // 0. ConfigLoader（必须最先加载，其他系统的 _Ready 会依赖配置数据）
        var configLoader = new ConfigLoader();
        configLoader.Name = "ConfigLoader";
        AddChild(configLoader);

        // 1. SynergyManager（必须先于GameManager）
        _synergyManager = new SynergyManager();
        _synergyManager.Name = "SynergyManager";
        AddChild(_synergyManager);

        // 2. Battlefield
        _battlefield = new Battlefield();
        _battlefield.Name = "Battlefield";
        _battlefield.Position = new Vector2(0, 48);
        AddChild(_battlefield);

        // 3. GameManager（依赖 Battlefield 和 SynergyManager）
        _gameManager = new GameManager();
        _gameManager.Name = "GameManager";
        AddChild(_gameManager);

        // 4. UI层 (CanvasLayer确保UI始终在最上层)
        var uiLayer = new CanvasLayer();
        uiLayer.Name = "UILayer";
        uiLayer.Layer = 10;
        AddChild(uiLayer);

        // 4a. 顶栏
        var topBar = new TopBarUI();
        topBar.Name = "TopBarUI";
        topBar.Position = new Vector2(0, 0);
        uiLayer.AddChild(topBar);

        // 4b. 羁绊面板（右侧）
        var synergyPanel = new SynergyPanel();
        synergyPanel.Name = "SynergyPanel";
        synergyPanel.Position = new Vector2(720, 48);
        uiLayer.AddChild(synergyPanel);

        // 4c. 待部署区
        var benchUI = new BenchUI();
        benchUI.Name = "BenchUI";
        benchUI.Position = new Vector2(0, 450);
        uiLayer.AddChild(benchUI);

        // 4d. 商店UI
        var shopUI = new ShopUI();
        shopUI.Name = "ShopUI";
        shopUI.Position = new Vector2(0, 535);
        uiLayer.AddChild(shopUI);

        // 5. 背景（放在最底层）
        BuildBackground();
    }

    private void BuildBackground()
    {
        // 深色背景
        var bg = new ColorRect();
        bg.Color = new Color(0.04f, 0.07f, 0.12f);
        bg.Size = new Vector2(900, 700);
        bg.ZIndex = -10;
        AddChild(bg);

        // 标题文字
        var titleLabel = new Label();
        titleLabel.Text = "⚔ Bond Defense";
        titleLabel.AddThemeFontSizeOverride("font_size", 13);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.5f, 0.7f, 0.6f));
        titleLabel.Position = new Vector2(360, 660);
        titleLabel.ZIndex = -5;
        AddChild(titleLabel);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            GetTree().Quit();
        }
    }
}
