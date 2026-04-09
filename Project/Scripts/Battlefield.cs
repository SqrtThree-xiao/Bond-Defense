using Godot;
using System.Collections.Generic;

/// <summary>
/// 战场管理节点 - 管理格子、英雄放置、敌人生成和路径
/// 使用 TileMapLayer 实现棋盘渲染，支持格子高亮
/// </summary>
public partial class Battlefield : Node2D
{
    // 战场格子配置
    [Export] public int GridCols { get; set; } = GameConst.Grid.Cols;
    [Export] public int GridRows { get; set; } = GameConst.Grid.Rows;

    // 战场尺寸（由外部设置）
    public Vector2 Size { get; set; } = new Vector2(560f, 320f);

    // 当前动态计算的格子大小（由 BuildTileMap 设置）
    private float _currentCellSize = GameConst.Grid.DefaultCellSize;

    // 敌人路径（格子坐标数组）
    private Vector2I[] _enemyPath;

    // 格子状态：null=空, Hero=有英雄
    private Hero[,] _grid;

    // TileMapLayer 引用（由 _Ready() 内部查找，或由外部注入）
    public TileMapLayer GridLayer { get; set; }
    public TileMapLayer HighlightLayer { get; set; }

    // 活跃敌人列表
    public List<Enemy> ActiveEnemies { get; private set; } = new();

    // TileSet source index 常量
    private const int TILE_EMPTY     = 0;
    private const int TILE_OCCUPIED = 1;
    private const int TILE_VALID   = 2;
    private const int TILE_SWAP     = 3;
    private const int TILE_INVALID  = 4;

    [Signal]
    public delegate void HeroPlacedEventHandler(Hero hero, int col, int row);
    [Signal]
    public delegate void HeroRemovedEventHandler(Hero hero);
    [Signal]
    public delegate void EnemyReachedEndEventHandler();
    [Signal]
    public delegate void EnemyKilledEventHandler(int reward);

    public override void _Ready()
    {
        AddToGroup("battlefield");
        _grid = new Hero[GridCols, GridRows];

        // 优先从场景子节点中查找 TileMapLayer
        GridLayer = GetNodeOrNull<TileMapLayer>("GridLayer");
        HighlightLayer = GetNodeOrNull<TileMapLayer>("HighlightLayer");

        // 找不到时才代码创建
        if (GridLayer == null)
        {
            GridLayer = new TileMapLayer();
            GridLayer.Name = "GridLayer";
            AddChild(GridLayer);
        }
        if (HighlightLayer == null)
        {
            HighlightLayer = new TileMapLayer();
            HighlightLayer.Name = "HighlightLayer";
            HighlightLayer.ZIndex = 1;
            AddChild(HighlightLayer);
        }

        // 初始化 TileSet
        _InitializeTileSet();
    }

    /// <summary>
    /// 初始化 TileSet：创建 5 种 tile（AtlasTile 风格）
    /// tile_size = 80×80，颜色直接画在 atlas texture 上
    /// </summary>
    private void _InitializeTileSet()
    {
        var ts = new TileSet();
        int tileSize = (int)GameConst.Grid.DefaultCellSize; // 80
        ts.TileSize = new Vector2I(tileSize, tileSize);

        _AddSolidTile(ts, TILE_EMPTY,     new Color(0.15f, 0.25f, 0.35f, 0.7f),  tileSize);
        _AddSolidTile(ts, TILE_OCCUPIED, new Color(0.2f,  0.3f,  0.5f,  0.85f), tileSize);
        _AddSolidTile(ts, TILE_VALID,    new Color(0.2f,  0.7f,  0.3f,  0.85f), tileSize);
        _AddSolidTile(ts, TILE_SWAP,     new Color(0.8f,  0.75f, 0.2f,  0.85f), tileSize);
        _AddSolidTile(ts, TILE_INVALID,   new Color(0.8f,  0.2f,  0.2f,  0.85f), tileSize);

        GridLayer.TileSet = ts;
        HighlightLayer.TileSet = ts;
    }

    /// <summary>
    /// 向 TileSet 添加一个纯色 AtlasTile（用 ImageTexture 填充纯色）
    /// </summary>
    private void _AddSolidTile(TileSet ts, int sourceId, Color color, int size)
    {
        // Image.CreateEmpty 是推荐 API
        var img = Image.CreateEmpty(size, size, true, Image.Format.Rgba8);
        img.Fill(color);
        var tex = ImageTexture.CreateFromImage(img);

        var source = new TileSetAtlasSource();
        source.Texture = tex;
        source.Margins = new Vector2I(0, 0);
        source.Separation = new Vector2I(0, 0);
        source.TextureRegionSize = new Vector2I(size, size);
        source.UseTexturePadding = false;

        // 在 atlas 中创建 1×1 的 tile（位置 0,0）
        Vector2I tilePos = new Vector2I(0, 0);
        source.CreateTile(tilePos);

        // 该 tile 会在 atlas 位置 (0,0) 处显示整张纯色纹理
        // TileMapLayer.SetCell 时指定 atlas_coord=(0,0) 即可

        ts.AddSource(source, sourceId);
    }

    // ═══════════════════════════════════════════════════════
    //  布局构建
    // ═══════════════════════════════════════════════════════

    private void BuildPath()
    {
        // 敌人路径：基于格子坐标
        // 中间行 row = GridRows / 2（向下取整）
        int midRow = GridRows / 2;
        var pts = new List<Vector2I>();

        // 起始：棋盘左侧边界外一格
        pts.Add(new Vector2I(-1, midRow));
        // 穿越棋盘的 3 个均匀分布点
        pts.Add(new Vector2I(GridCols / 3,     midRow));
        pts.Add(new Vector2I(GridCols * 2 / 3, midRow));
        // 终点：棋盘右侧边界外一格
        pts.Add(new Vector2I(GridCols, midRow));

        _enemyPath = pts.ToArray();

        // 路径可视化（用 Line2D，格子坐标转世界坐标）
        _UpdatePathVisual();
    }

    private void _UpdatePathVisual()
    {
        // 延迟到 UpdateLayout 后由 GridLayer 坐标系统确定
    }

    /// <summary>
    /// 构建 TileMap 格子背景
    /// </summary>
    public void BuildTileMap()
    {
        GridLayer.Clear();
        // 计算 CellSize（动态适配可用空间）
        float cellW = Size.X / GridCols;
        float cellH = Size.Y / GridRows;
        float cellSize = Mathf.Min(cellW, cellH);
        _currentCellSize = cellSize; // 记录当前格子大小
        // 更新 TileSet tile_size
        if (GridLayer.TileSet != null)
            GridLayer.TileSet.TileSize = new Vector2I((int)cellSize, (int)cellSize);
        if (HighlightLayer.TileSet != null)
            HighlightLayer.TileSet.TileSize = new Vector2I((int)cellSize, (int)cellSize);

        // 遍历所有格子，设置背景 tile
        for (int col = 0; col < GridCols; col++)
        {
            for (int row = 0; row < GridRows; row++)
            {
                GridLayer.SetCell(
                    new Vector2I(col, row),
                    0, // source_id = TILE_EMPTY
                    new Vector2I(0, 0)
                );
            }
        }
    }

    /// <summary>
    /// 更新战场布局（窗口大小变化时由 Main 调用）
    /// </summary>
    public void UpdateLayout(Vector2 newSize)
    {
        Size = newSize;
        BuildTileMap();
        BuildPath();

        // 重新定位已有英雄
        for (int c = 0; c < GridCols; c++)
        {
            for (int r = 0; r < GridRows; r++)
            {
                if (_grid[c, r] != null && IsInstanceValid(_grid[c, r]))
                {
                    _grid[c, r].GlobalPosition = GridToWorld(c, r) + new Vector2(0f, HeroPlacementYOffset);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  坐标工具
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 格子坐标 → 世界坐标（中心点）
    /// 直接基于 _currentCellSize 计算，不依赖 MapToLocal
    /// </summary>
    public Vector2 GridToWorld(int col, int row)
    {
        // 从战场 Position(0,0) 开始，每个格子 _currentCellSize 像素
        // 格子中心 = 左上角 + 半格
        return new Vector2(
            col * _currentCellSize + _currentCellSize / 2f,
            row * _currentCellSize + _currentCellSize / 2f
        );
    }

    /// <summary>
    /// 世界坐标 → 格子坐标
    /// </summary>
    public Vector2I WorldToGrid(Vector2 world)
    {
        return GridLayer.LocalToMap(GridLayer.ToLocal(world));
    }

    public bool IsValidCell(int col, int row)
    {
        return col >= 0 && col < GridCols && row >= 0 && row < GridRows;
    }

    public bool IsCellEmpty(int col, int row)
    {
        return IsValidCell(col, row) && _grid[col, row] == null;
    }

    /// <summary>
    /// 根据全局坐标获取格子上的英雄（用于拖拽启动检测）
    /// </summary>
    public Hero GetHeroAtWorldPos(Vector2 globalPos)
    {
        var cell = WorldToGrid(globalPos);
        if (!IsValidCell(cell.X, cell.Y)) return null;
        return _grid[cell.X, cell.Y];
    }

    /// <summary>
    /// 根据英雄查找其所在格子坐标
    /// </summary>
    public Vector2I? FindHeroCell(Hero hero)
    {
        for (int c = 0; c < GridCols; c++)
            for (int r = 0; r < GridRows; r++)
                if (_grid[c, r] == hero)
                    return new Vector2I(c, r);
        return null;
    }

    // ═══════════════════════════════════════════════════════
    //  英雄放置
    // ═══════════════════════════════════════════════════════

    /// <summary>英雄放置到格子时的垂直微调（像素），使视觉居中</summary>
    private const float HeroPlacementYOffset = 6f;

    public bool PlaceHero(Hero hero, int col, int row)
    {
        if (!IsCellEmpty(col, row)) return false;

        _grid[col, row] = hero;
        hero.GetParent()?.RemoveChild(hero);
        AddChild(hero);

        // 重置缩放（可能从待部署区继承了 BenchHeroScale）
        hero.Scale = Vector2.One;
        hero.Position = Vector2.Zero;

        var cellCenter = GridToWorld(col, row);
        hero.GlobalPosition = cellCenter + new Vector2(0f, HeroPlacementYOffset);

        // 更新格子 tile 为 occupied
        GridLayer.SetCell(new Vector2I(col, row), TILE_OCCUPIED, new Vector2I(0, 0));
        ClearHighlight(col, row);
        EmitSignal(SignalName.HeroPlaced, hero, col, row);
        return true;
    }

    /// <summary>
    /// 从格子移除英雄（返回英雄节点）
    /// </summary>
    public Hero RemoveHeroFromCell(int col, int row)
    {
        if (!IsValidCell(col, row) || _grid[col, row] == null) return null;
        var hero = _grid[col, row];
        _grid[col, row] = null;

        // 恢复格子 tile 为 empty
        GridLayer.SetCell(new Vector2I(col, row), TILE_EMPTY, new Vector2I(0, 0));
        ClearHighlight(col, row);
        EmitSignal(SignalName.HeroRemoved, hero);
        return hero;
    }

    /// <summary>
    /// 交换两个格子的英雄
    /// </summary>
    public void SwapHeroes(int col1, int row1, int col2, int row2)
    {
        if (!IsValidCell(col1, row1) || !IsValidCell(col2, row2)) return;
        var h1 = _grid[col1, row1];
        var h2 = _grid[col2, row2];
        _grid[col1, row1] = h2;
        _grid[col2, row2] = h1;
        if (h1 != null) h1.GlobalPosition = GridToWorld(col2, row2) + new Vector2(0f, HeroPlacementYOffset);
        if (h2 != null) h2.GlobalPosition = GridToWorld(col1, row1) + new Vector2(0f, HeroPlacementYOffset);

        // 更新 tile 状态
        GridLayer.SetCell(new Vector2I(col1, row1), h2 != null ? TILE_OCCUPIED : TILE_EMPTY, new Vector2I(0, 0));
        GridLayer.SetCell(new Vector2I(col2, row2), h1 != null ? TILE_OCCUPIED : TILE_EMPTY, new Vector2I(0, 0));
    }

    /// <summary>
    /// 获取战场所有英雄列表
    /// </summary>
    public List<Hero> GetAllHeroes()
    {
        var list = new List<Hero>();
        for (int c = 0; c < GridCols; c++)
            for (int r = 0; r < GridRows; r++)
                if (_grid[c, r] != null)
                    list.Add(_grid[c, r]);
        return list;
    }

    // ═══════════════════════════════════════════════════════
    //  格子高亮
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 高亮类型
    /// </summary>
    public enum HighlightType
    {
        None,
        Valid,
        Swap,
        Invalid
    }

    /// <summary>
    /// 高亮单个格子（使用 HighlightLayer 覆盖）
    /// </summary>
    public void HighlightCell(int col, int row, HighlightType type)
    {
        if (!IsValidCell(col, row)) return;
        var coord = new Vector2I(col, row);

        if (type == HighlightType.None)
        {
            HighlightLayer.EraseCell(coord);
        }
        else
        {
            int tileId = type switch
            {
                HighlightType.Valid   => TILE_VALID,
                HighlightType.Swap    => TILE_SWAP,
                HighlightType.Invalid => TILE_INVALID,
                _ => -1
            };
            if (tileId >= 0)
                HighlightLayer.SetCell(coord, tileId, new Vector2I(0, 0));
        }
    }

    /// <summary>
    /// 高亮单个格子（兼容旧接口）
    /// </summary>
    public void HighlightCell(int col, int row, bool highlighted)
    {
        HighlightCell(col, row, highlighted ? HighlightType.Valid : HighlightType.None);
    }

    /// <summary>
    /// 清除单个格子高亮
    /// </summary>
    public void ClearHighlight(int col, int row)
    {
        if (!IsValidCell(col, row)) return;
        HighlightLayer.EraseCell(new Vector2I(col, row));
    }

    public void ClearAllHighlights()
    {
        HighlightLayer.Clear();
    }

    // ═══════════════════════════════════════════════════════
    //  敌人生成
    // ═══════════════════════════════════════════════════════

    public void SpawnEnemy(float hp, float speed, int reward)
    {
        var enemy = new Enemy();
        enemy.MaxHp = hp;
        enemy.Speed = speed;
        enemy.Reward = reward;
        enemy.EnemyDied += OnEnemyDied;
        AddChild(enemy);
        enemy.SetGridPath(_enemyPath, GridLayer);
        ActiveEnemies.Add(enemy);
    }

    private void OnEnemyDied(Enemy enemy, bool reachedEnd)
    {
        ActiveEnemies.Remove(enemy);
        if (reachedEnd)
            EmitSignal(SignalName.EnemyReachedEnd);
        else
            EmitSignal(SignalName.EnemyKilled, enemy.Reward);
    }

    /// <summary>
    /// 获取敌人路径（格子坐标）
    /// </summary>
    public Vector2I[] GetEnemyGridPath() => _enemyPath;

    /// <summary>
    /// 获取敌人路径（像素坐标，用于兼容旧逻辑）
    /// </summary>
    public Vector2[] GetEnemyPath()
    {
        if (_enemyPath == null) return null;
        var pts = new Vector2[_enemyPath.Length];
        for (int i = 0; i < _enemyPath.Length; i++)
        {
            // 直接基于格子坐标和 _currentCellSize 计算世界坐标
            pts[i] = new Vector2(
                _enemyPath[i].X * _currentCellSize + _currentCellSize / 2f,
                _enemyPath[i].Y * _currentCellSize + _currentCellSize / 2f
            );
        }
        return pts;
    }

    /// <summary>
    /// 格子坐标 → 世界坐标（供外部使用，如 Enemy.SetGridPath）
    /// </summary>
    public Vector2 GridCellToWorld(int col, int row)
    {
        return new Vector2(
            col * _currentCellSize + _currentCellSize / 2f,
            row * _currentCellSize + _currentCellSize / 2f
        );
    }
}
