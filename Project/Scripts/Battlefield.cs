using Godot;
using System.Collections.Generic;

/// <summary>
/// 战场管理节点 - 管理格子、英雄放置、敌人生成和路径
/// </summary>
public partial class Battlefield : Node2D
{
    // 战场格子配置
    [Export] public int GridCols { get; set; } = 7;
    [Export] public int GridRows { get; set; } = 4;
    [Export] public float CellSize { get; set; } = 80f;

    // 战场起始位置（左上角）
    [Export] public Vector2 GridOrigin { get; set; } = new Vector2(60f, 80f);
    
    // 战场尺寸（由外部设置）
    public Vector2 Size { get; set; } = new Vector2(560f, 320f); // 默认值：7*80, 4*80

    // 敌人路径（从左到右穿越战场）
    private Vector2[] _enemyPath;

    // 格子状态：null=空, Hero=有英雄
    private Hero[,] _grid;
    // 格子视觉节点
    private ColorRect[,] _cellVisuals;

    // 活跃敌人列表
    public List<Enemy> ActiveEnemies { get; private set; } = new();

    // 敌人预制体场景
    private PackedScene _enemyScene;

    // 拖拽状态
    private Hero _draggingHero = null;
    private Vector2 _dragOriginalPos;
    private Vector2I _dragOriginalCell;
    private bool _isDraggingFromBench = false; // 是否从待部署区拖拽来
    
    // 路径可视化
    private Line2D _pathLine;

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
        _cellVisuals = new ColorRect[GridCols, GridRows];
        
        // 初始化路径可视化
        _pathLine = new Line2D();
        _pathLine.Width = 8f;
        _pathLine.DefaultColor = new Color(0.8f, 0.3f, 0.1f, 0.4f);
        AddChild(_pathLine);

        // 初始化布局（会在外部调用UpdateLayout）
    }

    private void BuildPath()
    {
        // 敌人路径：从左侧入场，蛇形穿越，从右侧离开
        var pts = new List<Vector2>();
        float startX = GridOrigin.X - 60f;
        float endX = GridOrigin.X + GridCols * CellSize + 60f;
        float midY = GridOrigin.Y + (GridRows / 2f) * CellSize;

        // 简单直线路径（穿越战场中央）
        pts.Add(new Vector2(startX, midY));
        pts.Add(new Vector2(GridOrigin.X + GridCols * CellSize / 3, midY));
        pts.Add(new Vector2(GridOrigin.X + GridCols * CellSize * 2 / 3, midY));
        pts.Add(new Vector2(endX, midY));
        _enemyPath = pts.ToArray();
        
        // 更新路径可视化
        DrawPathVisual();
    }

    private void BuildGridVisuals()
    {
        // 清除旧的格子视觉
        foreach (var cell in _cellVisuals)
        {
            if (cell != null && IsInstanceValid(cell))
                cell.QueueFree();
        }
        
        _cellVisuals = new ColorRect[GridCols, GridRows];

        // 计算格子大小（根据可用空间）
        float availableWidth = Size.X;
        float availableHeight = Size.Y;
        float cellWidth = availableWidth / GridCols;
        float cellHeight = availableHeight / GridRows;
        CellSize = Mathf.Min(cellWidth, cellHeight);
        
        // 计算起始位置（居中）
        float totalWidth = GridCols * CellSize;
        float totalHeight = GridRows * CellSize;
        GridOrigin = new Vector2(
            (availableWidth - totalWidth) / 2,
            (availableHeight - totalHeight) / 2
        );

        for (int col = 0; col < GridCols; col++)
        {
            for (int row = 0; row < GridRows; row++)
            {
                var cell = new ColorRect();
                cell.Size = new Vector2(CellSize - 4, CellSize - 4);
                cell.Position = GridOrigin + new Vector2(col * CellSize + 2, row * CellSize + 2);
                cell.Color = new Color(0.15f, 0.25f, 0.35f, 0.7f);

                AddChild(cell);
                _cellVisuals[col, row] = cell;
            }
        }
    }

    private void DrawPathVisual()
    {
        if (_enemyPath == null || _enemyPath.Length < 2) return;
        
        _pathLine.ClearPoints();
        foreach (var pt in _enemyPath)
        {
            _pathLine.AddPoint(pt);
        }
    }

    /// <summary>
    /// 更新战场布局（窗口大小变化时由 Main 调用）
    /// </summary>
    public void UpdateLayout(Vector2 newSize)
    {
        Size = newSize;
        BuildGridVisuals();
        BuildPath();
        
        // 重新定位已有英雄
        for (int c = 0; c < GridCols; c++)
        {
            for (int r = 0; r < GridRows; r++)
            {
                if (_grid[c, r] != null && IsInstanceValid(_grid[c, r]))
                {
                    _grid[c, r].GlobalPosition = GridToWorld(c, r);
                }
            }
        }
    }

    // ─────────────────────── 坐标工具 ───────────────────────

    public Vector2 GridToWorld(int col, int row)
    {
        return GridOrigin + new Vector2(col * CellSize + CellSize / 2, row * CellSize + CellSize / 2);
    }

    public Vector2I WorldToGrid(Vector2 world)
    {
        var local = world - GridOrigin;
        return new Vector2I((int)(local.X / CellSize), (int)(local.Y / CellSize));
    }

    public bool IsValidCell(int col, int row)
    {
        return col >= 0 && col < GridCols && row >= 0 && row < GridRows;
    }

    public bool IsCellEmpty(int col, int row)
    {
        return IsValidCell(col, row) && _grid[col, row] == null;
    }

    // ─────────────────────── 英雄放置 ───────────────────────

    /// <summary>
    /// 在格子放置英雄
    /// </summary>
    public bool PlaceHero(Hero hero, int col, int row)
    {
        if (!IsCellEmpty(col, row)) return false;

        _grid[col, row] = hero;
        hero.GetParent()?.RemoveChild(hero);
        AddChild(hero);
        hero.GlobalPosition = GridToWorld(col, row);

        HighlightCell(col, row, false);
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
        HighlightCell(col, row, false);
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
        if (h1 != null) h1.GlobalPosition = GridToWorld(col2, row2);
        if (h2 != null) h2.GlobalPosition = GridToWorld(col1, row1);
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

    // ─────────────────────── 格子高亮 ───────────────────────

    public void HighlightCell(int col, int row, bool highlighted)
    {
        if (!IsValidCell(col, row)) return;
        var cell = _cellVisuals[col, row];
        if (highlighted)
            cell.Color = new Color(0.3f, 0.6f, 0.4f, 0.8f);
        else if (_grid[col, row] != null)
            cell.Color = new Color(0.2f, 0.3f, 0.5f, 0.8f);
        else
            cell.Color = new Color(0.15f, 0.25f, 0.35f, 0.7f);
    }

    public void ClearAllHighlights()
    {
        for (int c = 0; c < GridCols; c++)
            for (int r = 0; r < GridRows; r++)
                HighlightCell(c, r, false);
    }

    // ─────────────────────── 鼠标交互（拖拽）───────────────────────

    [Signal]
    public delegate void DragReturnedToBenchEventHandler();

    // HeroStorage 引用（由 GameManager 设置）
    public Node HeroStorage { get; set; }

    public void StartDragFromBench(Hero hero)
    {
        _draggingHero = hero;
        _isDraggingFromBench = true;
        _dragOriginalCell = new Vector2I(-1, -1);

        // 将英雄从 HeroStorage 移入战场，使其可见并可跟随鼠标
        hero.GetParent()?.RemoveChild(hero);
        AddChild(hero);
        hero.ZIndex = 10; // 拖拽时置顶
    }

    public void StartDragFromGrid(int col, int row)
    {
        if (!IsValidCell(col, row) || _grid[col, row] == null) return;
        _draggingHero = _grid[col, row];
        _isDraggingFromBench = false;
        _dragOriginalCell = new Vector2I(col, row);
        _dragOriginalPos = _draggingHero.GlobalPosition;
        _grid[col, row] = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (_draggingHero == null) return;

        if (@event is InputEventMouseMotion motion)
        {
            _draggingHero.GlobalPosition = GetGlobalMousePosition();

            // 高亮悬停格子
            ClearAllHighlights();
            var cell = WorldToGrid(GetGlobalMousePosition());
            if (IsValidCell(cell.X, cell.Y))
                HighlightCell(cell.X, cell.Y, true);
        }
        else if (@event is InputEventMouseButton mb && !mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            DropDragging();
        }
    }

    private void DropDragging()
    {
        if (_draggingHero == null) return;
        var dropCell = WorldToGrid(GetGlobalMousePosition());
        ClearAllHighlights();

        bool placed = false;
        if (IsValidCell(dropCell.X, dropCell.Y))
        {
            if (_grid[dropCell.X, dropCell.Y] == null)
            {
                // 放置到空格
                _grid[dropCell.X, dropCell.Y] = _draggingHero;
                _draggingHero.GlobalPosition = GridToWorld(dropCell.X, dropCell.Y);
                HighlightCell(dropCell.X, dropCell.Y, false);
                placed = true;
                EmitSignal(SignalName.HeroPlaced, _draggingHero, dropCell.X, dropCell.Y);
            }
            else if (!_isDraggingFromBench && _dragOriginalCell.X >= 0)
            {
                // 与目标格子交换（仅战场内部拖拽）
                var other = _grid[dropCell.X, dropCell.Y];
                _grid[dropCell.X, dropCell.Y] = _draggingHero;
                _grid[_dragOriginalCell.X, _dragOriginalCell.Y] = other;
                _draggingHero.GlobalPosition = GridToWorld(dropCell.X, dropCell.Y);
                other.GlobalPosition = GridToWorld(_dragOriginalCell.X, _dragOriginalCell.Y);
                placed = true;
            }
        }

        if (!placed)
        {
            if (_isDraggingFromBench)
            {
                // 从待部署区拖来但放置失败 → 归还 HeroStorage 并通知刷新 bench UI
                ReturnToBench();
                _draggingHero = null;
                _isDraggingFromBench = false;
                return;
            }
            else if (_dragOriginalCell.X >= 0)
            {
                // 战场内部拖拽但放置失败 → 放回原位
                _grid[_dragOriginalCell.X, _dragOriginalCell.Y] = _draggingHero;
                _draggingHero.GlobalPosition = _dragOriginalPos;
            }
        }

        if (_draggingHero != null)
            _draggingHero.ZIndex = 0;
        _draggingHero = null;
        _isDraggingFromBench = false;
    }

    /// <summary>
    /// 将拖拽失败的英雄归还到 HeroStorage（bench 列表不变，英雄节点回到有效父节点）
    /// </summary>
    private void ReturnToBench()
    {
        if (_draggingHero == null) return;
        _draggingHero.ZIndex = 0;
        _draggingHero.Position = Vector2.Zero; // 重置位置，避免停留在拖拽处
        // 从战场移除，放回 HeroStorage 保持节点在场景树中
        RemoveChild(_draggingHero);
        if (HeroStorage != null)
            HeroStorage.AddChild(_draggingHero);
        // 通知 BenchUI 刷新显示（bench 列表未改变，但 UI 需要重建）
        EmitSignal(SignalName.DragReturnedToBench);
    }

    // ─────────────────────── 敌人生成 ───────────────────────

    public void SpawnEnemy(float hp, float speed, int reward)
    {
        var enemy = new Enemy();
        enemy.MaxHp = hp;
        enemy.Speed = speed;
        enemy.Reward = reward;
        enemy.EnemyDied += OnEnemyDied;
        AddChild(enemy);
        enemy.SetPath(_enemyPath);
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

    public Vector2[] GetEnemyPath() => _enemyPath;
}
