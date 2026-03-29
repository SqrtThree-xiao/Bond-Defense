using Godot;

/// <summary>
/// 主场景入口脚本 - 以代码方式构建整个游戏场景树
/// 支持窗口自适应布局，通过 UIManager 管理所有 UI 界面
/// </summary>
public partial class Main : Node2D
{
	private SynergyManager _synergyManager;
	private GameManager _gameManager;
	private Battlefield _battlefield;
	private UIManager _uiManager;

	private TopBarUI _topBar;
	private SynergyPanel _synergyPanel;
	private BenchUI _benchUI;
	private ShopUI _shopUI;
	private ColorRect _background;
	private Label _titleLabel;

	// UI 配置表 ID 常量
	private const int UI_TOP_BAR     = 1;
	private const int UI_SYNERGY_PANEL = 2;
	private const int UI_BENCH       = 3;
	private const int UI_SHOP        = 4;

	// 布局常量
	private const int TOP_BAR_HEIGHT = 48;
	private const int SHOP_HEIGHT = 110;
	private const int BENCH_HEIGHT = 80;
	private const int RIGHT_PANEL_MIN_WIDTH = 180;

	public override void _Ready()
	{
		Name = "Main";
		BuildSceneTree();

		// 延迟初始布局，确保所有子节点 _Ready 已完成
		Callable.From(UpdateLayout).CallDeferred();
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

		// 2. Battlefield（初始位置，UpdateLayout 会更新）
		_battlefield = new Battlefield();
		_battlefield.Name = "Battlefield";
		AddChild(_battlefield);

		// 3. GameManager（依赖 Battlefield 和 SynergyManager）
		_gameManager = new GameManager();
		_gameManager.Name = "GameManager";
		AddChild(_gameManager);

		// 4. 背景（放在最底层）
		BuildBackground();

		// 5. UIManager（依赖 ConfigLoader，必须在 ConfigLoader 之后创建）
		//    UIManager 会根据 UI 配置表的 ui_layer 自动创建 CanvasLayer
		_uiManager = new UIManager();
		_uiManager.Name = "UIManager";
		AddChild(_uiManager);

		// 6. 通过 UIManager 加载所有 UI 界面（层级由配置表 ui_layer 字段控制）
		_topBar = (TopBarUI)_uiManager.LoadUI(UI_TOP_BAR, this);
		_synergyPanel = (SynergyPanel)_uiManager.LoadUI(UI_SYNERGY_PANEL, this);
		_benchUI = (BenchUI)_uiManager.LoadUI(UI_BENCH, this);
		_shopUI = (ShopUI)_uiManager.LoadUI(UI_SHOP, this);
	}

	private void BuildBackground()
	{
		// 深色背景
		_background = new ColorRect();
		_background.Color = new Color(0.04f, 0.07f, 0.12f);
		_background.ZIndex = -10;
		AddChild(_background);

		// 标题文字
		_titleLabel = new Label();
		_titleLabel.Text = "⚔ Bond Defense";
		_titleLabel.AddThemeFontSizeOverride("font_size", 13);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.5f, 0.7f, 0.6f));
		_titleLabel.ZIndex = -5;
		AddChild(_titleLabel);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
		{
			GetTree().Quit();
		}
	}

	/// <summary>
	/// 更新所有元素的布局（根据当前窗口大小）
	/// 布局结构：
	/// ┌──────────────────────────────────┐
	/// │            TopBar (48px)          │
	/// ├────────────────────┬─────────────┤
	/// │                    │  Synergy     │
	/// │    Battlefield      │  Panel       │
	/// │                    │  (右侧)      │
	/// ├────────────────────┤              │
	/// │   Bench (80px)     │              │
	/// ├────────────────────┴─────────────┤
	/// │          Shop (110px)             │
	/// └──────────────────────────────────┘
	/// </summary>
	public void UpdateLayout()
	{
		var viewportSize = GetViewportRect().Size;
		float w = viewportSize.X;
		float h = viewportSize.Y;

		// 计算右侧面板宽度（最小180，最大不超过25%的宽度）
		float rightPanelWidth = Mathf.Clamp(w * 0.22f, RIGHT_PANEL_MIN_WIDTH, 220f);

		// 背景
		if (_background != null)
		{
			_background.Size = viewportSize;
		}

		// 标题（右下角）
		if (_titleLabel != null)
		{
			_titleLabel.Position = new Vector2(w - 200, h - 30);
		}

		// 顶栏：全宽，固定高度
		if (_topBar != null)
		{
			_topBar.Position = new Vector2(0, 0);
			_topBar.Size = new Vector2(w, TOP_BAR_HEIGHT);
		}

		// 羁绊面板：右侧，从顶栏下方到窗口底部
		if (_synergyPanel != null)
		{
			_synergyPanel.Position = new Vector2(w - rightPanelWidth, TOP_BAR_HEIGHT);
			_synergyPanel.Size = new Vector2(rightPanelWidth, h - TOP_BAR_HEIGHT);
		}

		// 战场：左侧区域，从顶栏下方到商店上方
		if (_battlefield != null)
		{
			float bfWidth = w - rightPanelWidth;
			float bfHeight = h - TOP_BAR_HEIGHT - SHOP_HEIGHT - BENCH_HEIGHT;
			bfHeight = Mathf.Max(bfHeight, 100); // 最小高度保护
			_battlefield.Position = new Vector2(0, TOP_BAR_HEIGHT);
			_battlefield.UpdateLayout(new Vector2(bfWidth, bfHeight));
		}

		// 待部署区：左下，商店上方
		if (_benchUI != null)
		{
			float benchWidth = w - rightPanelWidth;
			_benchUI.Position = new Vector2(0, h - SHOP_HEIGHT - BENCH_HEIGHT);
			_benchUI.Size = new Vector2(benchWidth, BENCH_HEIGHT);
		}

		// 商店：底部全宽
		if (_shopUI != null)
		{
			_shopUI.Position = new Vector2(0, h - SHOP_HEIGHT);
			_shopUI.Size = new Vector2(w, SHOP_HEIGHT);
		}
	}
}
