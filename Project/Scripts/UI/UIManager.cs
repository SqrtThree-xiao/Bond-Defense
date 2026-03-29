using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// UI 管理器 —— 根据配置表动态加载和管理 UI 界面
///
/// 用法：
///   UIManager.Instance.LoadUI(1, parent)     → 根据界面ID加载并添加到父节点
///   UIManager.Instance.GetUI<TopBarUI>(1)    → 获取已加载的 UI 实例
///   UIManager.Instance.UnloadUI(1)           → 卸载并释放 UI
/// </summary>
public partial class UIManager : Node
{
    public static UIManager Instance { get; private set; }

    /// <summary>已加载的 UI 实例缓存：uiId → Control 实例</summary>
    private readonly Dictionary<int, Control> _loadedUIs = new();

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
    /// 根据界面 ID 加载 UI 控件并添加到指定父节点
    /// </summary>
    /// <param name="uiId">UI 配置表中的界面 ID</param>
    /// <param name="parent">UI 要添加到的父节点</param>
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

        // 根据脚本名创建实例
        Control ui = CreateInstanceByScript(config.Script);
        if (ui == null)
        {
            GD.PrintErr($"[UIManager] Failed to create UI instance for script '{config.Script}' (id={uiId})");
            return null;
        }

        ui.Name = config.Name;

        // 如果配置了场景资源路径，优先用场景加载（预留扩展）
        if (!string.IsNullOrEmpty(config.ResourcePath))
        {
            var scene = GD.Load<PackedScene>(config.ResourcePath);
            if (scene != null)
            {
                ui = scene.Instantiate<Control>();
                ui.Name = config.Name;
            }
            else
            {
                GD.Print($"[UIManager] Resource path '{config.ResourcePath}' not found, using code-created instance");
            }
        }

        // 添加到父节点
        parent.AddChild(ui);
        _loadedUIs[uiId] = ui;

        GD.Print($"[UIManager] Loaded UI '{config.Name}' (id={uiId}, script={config.Script})");
        return ui;
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
    /// 检查指定 ID 的 UI 是否已加载
    /// </summary>
    public bool IsLoaded(int uiId)
    {
        return _loadedUIs.TryGetValue(uiId, out var ui) && IsInstanceValid(ui);
    }

    /// <summary>
    /// 卸载所有已加载的 UI
    /// </summary>
    public void UnloadAll()
    {
        var ids = new List<int>(_loadedUIs.Keys);
        foreach (var id in ids)
            UnloadUI(id);
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
