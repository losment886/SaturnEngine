using SaturnEngine.Asset;
using SaturnEngine.SEGraphics;
using SaturnEngine.SEUI.Render;
using SaturnEngine.SEUIControls;
using SaturnEngine.SEMath;
using System;
using System.IO;

namespace SaturnEngine.SEUI.Samples
{
    /// <summary>
    /// UI 自检样例：一键构造半透明面板 + 中英文标签 + 三态按钮 + 序列帧动画的控件树，
    /// 用于验证 UI 可见性、alpha 混合、置顶层级、鼠标命中与动画推进。
    /// </summary>
    public static class SEUISelfTest
    {
        /// <summary>
        /// 在指定窗口上装配测试 UI。调用后窗口每帧会自动布局并绘制。
        /// </summary>
        /// <param name="window">目标窗口，必须已完成 OnStart。</param>
        /// <param name="fontPath">TTF 字体路径，为空时标签走 SixLabors 位图回退路径。</param>
        /// <param name="frames">按钮序列帧动画所用的贴图，可为空。</param>
        public static SEControls Build(SEWindowNative window, string? fontPath = null, SESpirit[]? frames = null)
        {
            ArgumentNullException.ThrowIfNull(window);

            if (window.Renderer is not SENativeRender renderer)
                throw new InvalidOperationException("Window renderer is not ready.");

            // 必须先启用 UI 场景，SEUIRenderer 才会被创建
            renderer.SetUIScene(true);
            var uiRenderer = renderer.UIRenderer
                ?? throw new InvalidOperationException("UI renderer was not created.");

            SEUIFont? font = null;
            if (!string.IsNullOrEmpty(fontPath) && File.Exists(fontPath))
            {
                font = new SEUIFont(File.ReadAllBytes(fontPath), 32f, uiRenderer.Atlas);
                uiRenderer.DefaultFont = font;
            }

            var panel = new SEPanel
            {
                Size = new Vector2D(600, 400),
                // 半透明背景，用于确认 alpha 混合与 3D 场景叠加效果
                BackgroundColor = new SEColor(0.1, 0.1, 0.2, 0.6f),
                Opacity = 1.0,
                ZOrder = 0,
            };

            var label = new SELabel
            {
                ZOrder = 1,
                TextColor = SEColor.White,
            };
            if (font is not null)
                label.UIFont = font;
            label.Text = "SaturnEngine UI 自检 / Self Test";

            var button = new SEButton
            {
                ZOrder = 2,
                Animator = window.Animator,
                // 悬停放大、按下缩小，用于确认状态切换与动画推进
                StateAnimationFactory = static (btn, from, to) => to switch
                {
                    SEButtonState.Hover => new SEUIScaleAnimation(btn, 1.0, 1.08, 0.15, SEUIEasing.CubicOut),
                    SEButtonState.Pressed => new SEUIScaleAnimation(btn, 1.08, 0.95, 0.08, SEUIEasing.QuadOut),
                    _ => new SEUIScaleAnimation(btn, 1.08, 1.0, 0.15, SEUIEasing.BackOut),
                },
            };

            if (frames is { Length: > 0 })
            {
                button.NormalImage = frames[0];
                button.Spirit = frames[0];
                window.Animator.Play(new SEUISpriteAnimation(button, frames, 0.1, SEUIPlayMode.Loop));
            }

            panel.Child = [label, button];
            label.Parent = panel;
            button.Parent = panel;

            var controls = new SEControls();
            controls.Controls.Add(panel);
            controls.Init();

            window.Controls = controls;
            return controls;
        }
    }
}
