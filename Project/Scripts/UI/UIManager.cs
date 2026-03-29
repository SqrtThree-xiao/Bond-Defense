using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// UI 管理器 —— 根据配置表动态加载和管理 UI 界面
/// 支持通过 ui_layer 字段自动创建/复用 CanvasLayer
///
/// 用法：
///   UIManager.Instance.LoadUI(1, parent)     → 根据界面ID加载，自动处理层级
///   UIManager.Instance.GetUI&lt;TopBarUI&gt;(1)    → 获取已加载的 UI 实例
///   UIManager.Instance.UnloadUI(1)           → 卸载并释放 UI
/// </summary>
public partial class UIManager : Node
{
    public static UIManager Instance { get; private set; }

    /// <summary>已加载的 UI 实例缓存：uiId → Control 实例</summary>
    private readonly Dictionary<int, Control> _loadedUIs = new();

    /// <summary>按层级管理的 CanvasLayer 缓存：layer → CanvasLayer 实例</summary>
    private readonly Dictionary<int, CanvasLayer> _canvasLayers = new();

    /// <summary>脚本名称到 C# 类型的映射表（注册新 UI 时需在此添加）</summary>
    private readonly Dictionary<string, Type> _scriptTypeMap = new()
    {
        { "TopBarUI",     typeof(TopBarUI) },
        { "SynergyPanel", typeof(SynergyPanel) },
        { "BenchUI",      typeof(BenchUI) },
        { "ShopUI",       typeof(ShopUI) },
    };

    public override void _Ready()
    {
        Instance = this;
        Name = "UIManager";
        GD.Print("[UIManager] Initialized");
    }

    /// <summary>
    /// 根据界面 ID 加载 UI 控件
    /// 当 ui_layer > 0 时自动创建或复用对应层级的 CanvasLayer 作为父节点
    /// 当 ui_layer = 0 时直接添加到传入的 parent 节点
    /// 优先从 PackedScene（.tscn）加载，失败时降级为反射创建
    /// </summary>
    /// <param name="uiId">UI 配置表中的界面 ID</param>
    /// <param name="parent">默认父节点（ui_layer=0 时使用）</param>
    /// <returns>加载成功的 UI Control 实例，失败返回 null</returns>
    public Control LoadUI(int uiId, Node parent)
    {
        // 已加载则直接返回
        if (_loadedUIs.TryGetValue(uiId, out var existing) && IsInstanceValid(existing))
        {
            GD.Print($"[UIManager] UI {uiId} already loaded, skipping");
            return existing;
        }

        // 查询配置
        var config = ConfigLoader.Instance.GetUI(uiId);
        if (config == null)
        {
            GD.PrintErr($"[UIManager] UI config not found for id={uiId}");
            return null;
        }

        Control ui = null;

        // 优先从 PackedScene（.tscn 预制场景）加载
        if (!string.IsNullOrEmpty(config.ResourcePath))
        {
            var scene = GD.Load<PackedScene>(config.ResourcePath);
            if (scene != null)
            {
                ui = scene.Instantiate<Control>();
                ui.Name = config.Name;
                GD.Print($"[UIManager] Loaded '{config.Name}' from scene: {config.ResourcePath}");
            }
            else
            {
                GD.PrintErr($"[UIManager] Scene not found: {config.ResourcePath}, falling back to code creation");
            }
        }

        // 降级：根据脚本名通过反射创建
        if (ui == null)
        {
            ui = CreateInstanceByScript(config.Script);
            if (ui != null)
            {
                ui.Name = config.Name;
                GD.Print($"[UIManager] Loaded '{config.Name}' via reflection: {config.Script}");
            }
        }

        if (ui == null)
        {
            GD.PrintErr($"[UIManager] Failed to create UI '{config.Name}' (id={uiId})");
            return null;
        }

        // 根据 ui_layer 决定父节点
        Node actualParent = parent;
        if (config.UILayer > 0)
        {
            var layer = GetOrCreateCanvasLayer(config.UILayer, parent);
            actualParent = layer;
        }

        // 添加到父节点
        actualParent.AddChild(ui);
        _loadedUIs[uiId] = ui;

        GD.Print($"[UIManager] Added '{config.Name}' to {(config.UILayer > 0 ? $"CanvasLayer(layer={config.UILayer})" : "parent")}");
        return ui;
    }

    /// <summary>
    /// 获取或创建指定层级的 CanvasLayer
    /// CanvasLayer 会添加到 fallbackParent 下面，同名复用
    /// </summary>
    private CanvasLayer GetOrCreateCanvasLayer(int layer, Node fallbackParent)
    {
        if (_canvasLayers.TryGetValue(layer, out var existing) && IsInstanceValid(existing))
            return existing;

        var cl = new CanvasLayer();
        cl.Name = $"UILayer_{layer}";
        cl.Layer = layer;
        fallbackParent.AddChild(cl);
        _canvasLayers[layer] = cl;

        GD.Print($"[UIManager] Created CanvasLayer '{cl.Name}' (layer={layer})");
        return cl;
    }

    /// <summary>
    /// 根据界面 ID 卸载并释放 UI 实例
    /// </summary>
    public bool UnloadUI(int uiId)
    {
        if (!_loadedUIs.TryGetValue(uiId, out var ui) || !IsInstanceValid(ui))
        {
            _loadedUIs.Remove(uiId);
            return false;
        }

        ui.GetParent()?.RemoveChild(ui);
        ui.QueueFree();
        _loadedUIs.Remove(uiId);

        GD.Print($"[UIManager] Unloaded UI id={uiId}");
        return true;
    }

    /// <summary>
    /// 获取已加载的 UI 实例（带类型转换）
    /// </summary>
    public T GetUI<T>(int uiId) where T : Control
    {
        if (_loadedUIs.TryGetValue(uiId, out var ui) && IsInstanceValid(ui))
            return ui as T;
        return null;
    }

    /// <summary>
    /// 获取已加载的 UI 实例（不带类型转换）
    /// </summary>
    public Control GetUI(int uiId)
    {
        if (_loadedUIs.TryGetValue(uiId, out var ui) && IsInstanceValid(ui))
            return ui;
        return null;
    }

    /// <summary>
    /// 获取指定层级的 CanvasLayer（用于布局定位等）
    /// </summary>
    public CanvasLayer GetCanvasLayer(int layer)
    {
        if (_canvasLayers.TryGetValue(layer, out var cl) && IsInstanceValid(cl))
            return cl;
        return null;
    }

    /// <summary>
    /// 检查指定 ID 的 UI 是否已加载
    /// </summary>
    public bool IsLoaded(int uiId)
    {
        return _loadedUIs.TryGetValue(uiId, out var ui) && IsInstanceValid(ui);
    }

    /// <summary>
    /// 卸载所有已加载的 UI 和自动创建的 CanvasLayer
    /// </summary>
    public void UnloadAll()
    {
        var ids = new List<int>(_loadedUIs.Keys);
        foreach (var id in ids)
            UnloadUI(id);

        // 清理自动创建的 CanvasLayer
        foreach (var cl in _canvasLayers.Values)
        {
            if (IsInstanceValid(cl))
                cl.QueueFree();
        }
        _canvasLayers.Clear();
    }

    /// <summary>
    /// 根据脚本名称通过反射创建 Control 实例
    /// </summary>
    private Control CreateInstanceByScript(string scriptName)
    {
        if (!_scriptTypeMap.TryGetValue(scriptName, out var type))
        {
            GD.PrintErr($"[UIManager] Unknown script '{scriptName}', register it in _scriptTypeMap");
            return null;
        }

        try
        {
            return Activator.CreateInstance(type) as Control;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[UIManager] Failed to instantiate '{scriptName}': {e.Message}");
            return null;
        }
    }
}
