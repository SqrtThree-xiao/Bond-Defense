using Godot;

/// <summary>
/// 全局常量 —— 集中管理项目中所有硬编码的数值常量
///
/// 分类：
///   Game     - 游戏核心数值（波次、商店、升星）
///   Layout   - UI 布局尺寸（顶栏、商店、待部署区、右侧面板）
///   Grid     - 战场网格配置
///   UI       - UI 配置表 ID
///   Visual   - 视觉样式参数
/// </summary>
public static class GameConst
{
    // ═══════════════════════════════════════════
    //  Game —— 游戏核心数值
    // ═══════════════════════════════════════════

    public static class Game
    {
        /// <summary>初始金币</summary>
        public const int StartGold = 30;

        /// <summary>初始生命</summary>
        public const int StartLife = 20;

        /// <summary>最大波次</summary>
        public const int MaxWave = 10;

        /// <summary>商店槽位数</summary>
        public const int ShopSlotCount = 5;

        /// <summary>待部署区容量</summary>
        public const int BenchCapacity = 8;

        /// <summary>升星合成所需数量</summary>
        public const int MergeRequireCount = 3;

        /// <summary>英雄最大星级</summary>
        public const int MaxStar = 3;

        /// <summary>升星攻击力倍率（按星级索引，0=未使用）</summary>
        public static readonly float[] StarMultipliers = { 0f, 1.0f, 1.8f, 3.5f };

        /// <summary>商店默认刷新费用</summary>
        public const int DefaultRefreshCost = 2;

        /// <summary>波次回退公式：基础敌人数量</summary>
        public const int WaveBaseEnemyCount = 5;

        /// <summary>波次回退公式：每波额外敌人数量</summary>
        public const int WaveExtraEnemyPerWave = 2;

        /// <summary>波次回退公式：基础 HP</summary>
        public const int WaveBaseHp = 80;

        /// <summary>波次回退公式：每波额外 HP</summary>
        public const int WaveExtraHpPerWave = 30;

        /// <summary>波次回退公式：基础速度</summary>
        public const float WaveBaseSpeed = 70f;

        /// <summary>波次回退公式：每波额外速度</summary>
        public const float WaveExtraSpeedPerWave = 5f;

        /// <summary>波次回退公式：基础生成间隔</summary>
        public const float WaveBaseSpawnInterval = 1.5f;

        /// <summary>波次回退公式：每波生成间隔减少量</summary>
        public const float WaveSpawnIntervalDecay = 0.05f;

        /// <summary>生成间隔下限</summary>
        public const float WaveMinSpawnInterval = 0.3f;

        /// <summary>波次回退公式：基础击杀奖励</summary>
        public const int WaveBaseKillReward = 5;

        /// <summary>波次回退公式：每波额外击杀奖励</summary>
        public const int WaveExtraKillRewardPerWave = 1;

        /// <summary>波次回退公式：基础波次奖励</summary>
        public const int WaveBaseReward = 10;

        /// <summary>波次回退公式：每波额外波次奖励</summary>
        public const int WaveExtraRewardPerWave = 2;
    }

    // ═══════════════════════════════════════════
    //  Layout —— UI 布局尺寸
    // ═══════════════════════════════════════════

    public static class Layout
    {
        /// <summary>顶栏高度</summary>
        public const int TopBarHeight = 48;

        /// <summary>商店区域高度</summary>
        public const int ShopHeight = 110;

        /// <summary>待部署区高度（两行槽位 + 内边距 + 标题）</summary>
        public const int BenchHeight = 140;

        /// <summary>右侧面板最小宽度</summary>
        public const int RightPanelMinWidth = 180;

        /// <summary>右侧面板最大宽度</summary>
        public const int RightPanelMaxWidth = 220;

        /// <summary>右侧面板宽度占窗口百分比</summary>
        public const float RightPanelWidthRatio = 0.22f;

        /// <summary>战场最小高度保护</summary>
        public const int BattlefieldMinHeight = 100;

        /// <summary>CanvasLayer 渲染层级</summary>
        public const int UILayer = 10;
    }

    // ═══════════════════════════════════════════
    //  Grid —— 战场网格
    // ═══════════════════════════════════════════

    public static class Grid
    {
        /// <summary>网格列数</summary>
        public const int Cols = 9;

        /// <summary>网格行数</summary>
        public const int Rows = 6;

        /// <summary>默认格子大小（像素）</summary>
        public const float DefaultCellSize = 80f;

        /// <summary>路径超出战场边缘的距离</summary>
        public const float PathOverflow = 60f;
    }

    // ═══════════════════════════════════════════
    //  UI —— UI 配置表 ID
    // ═══════════════════════════════════════════

    public static class UI
    {
        public const int TopBar = 1;
        public const int SynergyPanel = 2;
        public const int Bench = 3;
        public const int Shop = 4;
    }

    // ═══════════════════════════════════════════
    //  Visual —— 视觉样式参数
    // ═══════════════════════════════════════════

    public static class Visual
    {
        /// <summary>英雄八边形半径</summary>
        public const float HeroBodyRadius = 22f;

        /// <summary>英雄点击区域半径</summary>
        public const float HeroClickRadius = 24f;

        /// <summary>英雄边框线宽</summary>
        public const float HeroBorderWidth = 2f;

        /// <summary>敌人菱形半宽</summary>
        public const float EnemyHalfWidth = 14f;

        /// <summary>敌人菱形半高</summary>
        public const float EnemyHalfHeight = 18f;

        /// <summary>HP 条宽度</summary>
        public const float HpBarWidth = 36f;

        /// <summary>HP 条高度</summary>
        public const float HpBarHeight = 5f;

        /// <summary>敌人路径可视化线宽</summary>
        public const float PathLineWidth = 8f;

        /// <summary>格子间距（像素）</summary>
        public const float CellGap = 4f;

        /// <summary>Toast 显示时长（秒）</summary>
        public const float ToastDuration = 2.5f;

        /// <summary>Toast 淡出时长（秒）</summary>
        public const float ToastFadeDuration = 0.5f;

        /// <summary>攻击闪烁持续时间</summary>
        public const float AttackFlashDuration = 0.05f;

        /// <summary>攻击闪烁恢复时间</summary>
        public const float AttackFlashRecover = 0.1f;

        /// <summary>受击闪白持续时间</summary>
        public const float HitFlashDuration = 0.05f;

        /// <summary>受击闪白恢复时间</summary>
        public const float HitFlashRecover = 0.1f;

        /// <summary>死亡淡出时间（秒）</summary>
        public const float DeathFadeDuration = 0.2f;

        /// <summary>英雄卡片尺寸</summary>
        public static readonly Vector2 HeroCardSize = new(104f, 78f);

        /// <summary>待部署区英雄槽位尺寸</summary>
        public static readonly Vector2 BenchSlotSize = new(76f, 56f);

        /// <summary>待部署区槽位间距</summary>
        public const float BenchSlotGap = 6f;

        /// <summary>待部署区内边距</summary>
        public const float BenchPadding = 6f;

        /// <summary>待部署区标题行高度</summary>
        public const float BenchTitleHeight = 20f;

        /// <summary>待部署区英雄模型缩放比例</summary>
        public const float BenchHeroScale = 0.5f;
    }
}
