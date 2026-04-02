using Godot;

/// <summary>
/// 主场景入口脚本 - 以代码方式构建整个游戏场景树
/// 支持窗口自适应布局，通过 UIManager 管理所有 UI 界面
/// 
/// 布局结构：
/// ┌──────────────────────────────┐
/// │          TopBar (48px)       │
/// ├──────────────────┬───────────┤
/// │                  │           │
/// │   Battlefield    │  Synergy  │
/// │   (9×6 网格)     │  Panel    │
/// │                  │  (右侧)   │
/// ├──────────────────┤           │
/// │   Bench (待部署区)│           │
/// ├──────────────────┴───────────┤
/// │          Shop (110px)        │
/// └──────────────────────────────┘
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
	private const int UI_TOP_BAR      = 1;
	private const int UI_SYNERGY_PANEL = 2;
	private const int UI_SHOP         = 4;

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

		// 4. BenchUI（场景层 Node2D，位于战场下方）
		_benchUI = new BenchUI();
		_benchUI.Name = "BenchUI";
		AddChild(_benchUI);

		// 5. 背景（放在最底层）
		BuildBackground();

		// 6. UIManager（依赖 ConfigLoader，必须在 ConfigLoader 之后创建）
		_uiManager = new UIManager();
		_uiManager.Name = "UIManager";
		AddChild(_uiManager);

		// 7. 通过 UIManager 加载 UI 界面（不再加载 BenchUI，它已作为场景节点）
		_topBar = (TopBarUI)_uiManager.LoadUI(UI_TOP_BAR, this);
		_synergyPanel = (SynergyPanel)_uiManager.LoadUI(UI_SYNERGY_PANEL, this);
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
	/// 战场占左侧主体区域，待部署区和羁绊面板在右侧
	/// 商店在底部
	/// </summary>
	public void UpdateLayout()
	{
		var viewportSize = GetViewportRect().Size;
		float w = viewportSize.X;
		float h = viewportSize.Y;

		int topBarHeight = GameConst.Layout.TopBarHeight;
		int shopHeight = GameConst.Layout.ShopHeight;
		int rightPanelWidth = Mathf.Clamp((int)(w * GameConst.Layout.RightPanelWidthRatio),
			GameConst.Layout.RightPanelMinWidth, GameConst.Layout.RightPanelMaxWidth);

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
			_topBar.Size = new Vector2(w, topBarHeight);
		}

		// 右侧区域宽度（羁绊面板，待部署区已移到棋盘下方）
		float mainAreaWidth = w - rightPanelWidth;
		float benchHeight = GameConst.Layout.BenchHeight;
		float gameAreaHeight = h - topBarHeight - shopHeight - benchHeight;
		gameAreaHeight = Mathf.Max(gameAreaHeight, 100); // 最小高度保护

		// 战场：左侧主区域（从顶栏下方到待部署区上方）
		if (_battlefield != null)
		{
			_battlefield.Position = new Vector2(0, topBarHeight);
			_battlefield.UpdateLayout(new Vector2(mainAreaWidth, gameAreaHeight));
		}

		// 待部署区：棋盘正下方，与棋盘同宽
		if (_benchUI != null)
		{
			_benchUI.UpdateLayout(
				new Vector2(0, topBarHeight + gameAreaHeight),
				new Vector2(mainAreaWidth, benchHeight)
			);
		}

		// 羁绊面板：右侧，与战场+待部署区同高
		if (_synergyPanel != null)
		{
			float rightHeight = gameAreaHeight + benchHeight;
			_synergyPanel.Position = new Vector2(mainAreaWidth, topBarHeight);
			_synergyPanel.Size = new Vector2(rightPanelWidth, rightHeight);
		}

		// 商店：底部全宽
		if (_shopUI != null)
		{
			_shopUI.Position = new Vector2(0, h - shopHeight);
			_shopUI.Size = new Vector2(w, shopHeight);
		}
	}
}
