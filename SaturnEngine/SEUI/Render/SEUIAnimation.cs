using SaturnEngine.Asset;
using SaturnEngine.SEMath;
using System;
using System.Collections.Generic;

namespace SaturnEngine.SEUI.Render
{
    /// <summary>播放模式。</summary>
    public enum SEUIPlayMode
    {
        /// <summary>播放一次后停在末帧。</summary>
        Once = 0,
        /// <summary>循环播放。</summary>
        Loop = 1,
        /// <summary>往返播放。</summary>
        PingPong = 2,
    }

    /// <summary>缓动类型。</summary>
    public enum SEUIEasing
    {
        Linear = 0,
        QuadIn = 1,
        QuadOut = 2,
        QuadInOut = 3,
        CubicIn = 4,
        CubicOut = 5,
        CubicInOut = 6,
        SineInOut = 7,
        BackOut = 8,
        ElasticOut = 9,
    }

    internal static class SEUIEase
    {
        public static double Apply(SEUIEasing easing, double t)
        {
            t = Math.Clamp(t, 0d, 1d);
            return easing switch
            {
                SEUIEasing.QuadIn => t * t,
                SEUIEasing.QuadOut => t * (2d - t),
                SEUIEasing.QuadInOut => t < 0.5d ? 2d * t * t : -1d + (4d - 2d * t) * t,
                SEUIEasing.CubicIn => t * t * t,
                SEUIEasing.CubicOut => 1d + Math.Pow(t - 1d, 3d),
                SEUIEasing.CubicInOut => t < 0.5d ? 4d * t * t * t : 1d + 4d * Math.Pow(t - 1d, 3d),
                SEUIEasing.SineInOut => -(Math.Cos(Math.PI * t) - 1d) / 2d,
                SEUIEasing.BackOut => 1d + 2.70158d * Math.Pow(t - 1d, 3d) + 1.70158d * Math.Pow(t - 1d, 2d),
                SEUIEasing.ElasticOut => t == 0d || t == 1d
                    ? t
                    : Math.Pow(2d, -10d * t) * Math.Sin((t * 10d - 0.75d) * (2d * Math.PI / 3d)) + 1d,
                _ => t,
            };
        }
    }

    /// <summary>
    /// UI 动画基类。所有动画都以秒为单位推进，并作用于一个目标控件。
    /// </summary>
    public abstract class SEUIAnimation
    {
        protected double Elapsed;

        public SEControl Target { get; }
        public double Duration { get; }
        public SEUIPlayMode Mode { get; }
        public bool IsFinished { get; private set; }
        public bool IsPaused { get; set; }

        /// <summary>动画自然结束时触发（循环模式不会触发）。</summary>
        public event Action<SEUIAnimation>? Completed;

        protected SEUIAnimation(SEControl target, double duration, SEUIPlayMode mode)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Duration = duration <= 0d ? 0.0001d : duration;
            Mode = mode;
        }

        /// <summary>推进动画，返回是否仍在播放。</summary>
        public bool Update(double deltaTime)
        {
            if (IsFinished || IsPaused)
                return !IsFinished;

            Elapsed += deltaTime;

            double progress;
            switch (Mode)
            {
                case SEUIPlayMode.Loop:
                    progress = (Elapsed % Duration) / Duration;
                    break;

                case SEUIPlayMode.PingPong:
                    progress = ComputePingPong(Elapsed);
                    break;

                default:
                    progress = Elapsed / Duration;
                    if (progress >= 1d)
                    {
                        progress = 1d;
                        IsFinished = true;
                    }
                    break;
            }

            Apply(progress);

            if (IsFinished)
                Completed?.Invoke(this);

            return !IsFinished;
        }

        /// <summary>从头开始播放。</summary>
        public void Reset()
        {
            Elapsed = 0d;
            IsFinished = false;
        }

        /// <summary>立即结束动画并跳到终态。</summary>
        public void Stop(bool applyFinalState = true)
        {
            if (applyFinalState)
                Apply(1d);
            IsFinished = true;
        }

        /// <summary>跳转到指定时间点（秒）并立即应用该处的状态。</summary>
        public void Seek(double time)
        {
            Elapsed = Math.Max(0d, time);
            IsFinished = false;

            double progress = Mode switch
            {
                SEUIPlayMode.Loop => (Elapsed % Duration) / Duration,
                SEUIPlayMode.PingPong => ComputePingPong(Elapsed),
                _ => Math.Min(1d, Elapsed / Duration),
            };

            if (Mode == SEUIPlayMode.Once && Elapsed >= Duration)
                IsFinished = true;

            Apply(progress);
        }

        private double ComputePingPong(double elapsed)
        {
            double cycle = (elapsed % (Duration * 2d)) / Duration;
            return cycle <= 1d ? cycle : 2d - cycle;
        }

        /// <summary>按归一化进度 [0,1] 应用动画效果。</summary>
        protected abstract void Apply(double progress);
    }

    /// <summary>
    /// 序列帧动画：输入一组连续贴图与每帧时长，按时间轴切换控件的 Spirit。
    /// </summary>
    public sealed class SEUISpriteAnimation : SEUIAnimation
    {
        private readonly SESpirit[] _frames;

        public SEUISpriteAnimation(SEControl target, SESpirit[] frames, double frameDuration,
            SEUIPlayMode mode = SEUIPlayMode.Loop)
            : base(target, frameDuration * Math.Max(1, frames?.Length ?? 1), mode)
        {
            ArgumentNullException.ThrowIfNull(frames);
            if (frames.Length == 0)
                throw new ArgumentException("Sprite animation requires at least one frame.", nameof(frames));
            _frames = frames;
        }

        protected override void Apply(double progress)
        {
            int index = (int)(progress * _frames.Length);
            if (index >= _frames.Length)
                index = _frames.Length - 1;
            Target.Spirit = _frames[index];
        }
    }

    /// <summary>透明度渐变动画。</summary>
    public sealed class SEUIFadeAnimation : SEUIAnimation
    {
        private readonly double _from;
        private readonly double _to;
        private readonly SEUIEasing _easing;

        public SEUIFadeAnimation(SEControl target, double from, double to, double duration,
            SEUIEasing easing = SEUIEasing.Linear, SEUIPlayMode mode = SEUIPlayMode.Once)
            : base(target, duration, mode)
        {
            _from = from;
            _to = to;
            _easing = easing;
        }

        protected override void Apply(double progress)
        {
            double t = SEUIEase.Apply(_easing, progress);
            Target.Opacity = Math.Clamp(_from + (_to - _from) * t, 0d, 1d);
        }
    }

    /// <summary>尺寸缩放动画，按倍率作用于控件尺寸。</summary>
    public sealed class SEUIScaleAnimation : SEUIAnimation
    {
        private readonly Vector2D _baseSize;
        private readonly double _from;
        private readonly double _to;
        private readonly SEUIEasing _easing;

        public SEUIScaleAnimation(SEControl target, double fromScale, double toScale, double duration,
            SEUIEasing easing = SEUIEasing.Linear, SEUIPlayMode mode = SEUIPlayMode.Once)
            : base(target, duration, mode)
        {
            _baseSize = target.Size;
            _from = fromScale;
            _to = toScale;
            _easing = easing;
        }

        protected override void Apply(double progress)
        {
            double t = SEUIEase.Apply(_easing, progress);
            double scale = _from + (_to - _from) * t;
            Target.Size = new Vector2D(_baseSize.X * scale, _baseSize.Y * scale);
        }
    }

    /// <summary>旋转动画，作用于控件的 Angle（弧度）。</summary>
    public sealed class SEUIRotateAnimation : SEUIAnimation
    {
        private readonly double _from;
        private readonly double _to;
        private readonly SEUIEasing _easing;

        public SEUIRotateAnimation(SEControl target, double fromRadians, double toRadians, double duration,
            SEUIEasing easing = SEUIEasing.Linear, SEUIPlayMode mode = SEUIPlayMode.Once)
            : base(target, duration, mode)
        {
            _from = fromRadians;
            _to = toRadians;
            _easing = easing;
        }

        protected override void Apply(double progress)
        {
            double t = SEUIEase.Apply(_easing, progress);
            Target.Angle = _from + (_to - _from) * t;
        }
    }

    /// <summary>着色渐变动画。</summary>
    public sealed class SEUITintAnimation : SEUIAnimation
    {
        private readonly SEColor _from;
        private readonly SEColor _to;
        private readonly SEUIEasing _easing;

        public SEUITintAnimation(SEControl target, SEColor from, SEColor to, double duration,
            SEUIEasing easing = SEUIEasing.Linear, SEUIPlayMode mode = SEUIPlayMode.Once)
            : base(target, duration, mode)
        {
            _from = from;
            _to = to;
            _easing = easing;
        }

        protected override void Apply(double progress)
        {
            double t = SEUIEase.Apply(_easing, progress);
            var tint = Target.Tint;
            tint.R = _from.R + (_to.R - _from.R) * t;
            tint.G = _from.G + (_to.G - _from.G) * t;
            tint.B = _from.B + (_to.B - _from.B) * t;
            tint.A = (float)(_from.A + (_to.A - _from.A) * t);
            Target.Tint = tint;
        }
    }

    /// <summary>
    /// 全局默认动画管理器。SEUILL 生成的布局代码会把 &lt;Animation&gt; 标签
    /// 声明的动画注册到这里，宿主只需每帧推进它即可。
    /// </summary>
    public static class SEUIAnimations
    {
        public static SEUIAnimator Default { get; } = new();
    }

    /// <summary>
    /// UI 动画管理器：统一推进所有动画，自动移除已完成项。
    /// </summary>
    public sealed class SEUIAnimator
    {
        private readonly List<SEUIAnimation> _animations = new();
        private readonly List<SEUIAnimation> _pending = new();
        private bool _updating;

        public int Count => _animations.Count;

        public void Play(SEUIAnimation animation)
        {
            ArgumentNullException.ThrowIfNull(animation);

            // Update 过程中新增的动画延迟到下一帧加入，避免修改正在遍历的集合
            if (_updating)
                _pending.Add(animation);
            else
                _animations.Add(animation);
        }

        /// <summary>移除某个控件上的全部动画。</summary>
        public void StopAll(SEControl target, bool applyFinalState = false)
        {
            for (int i = _animations.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_animations[i].Target, target))
                {
                    _animations[i].Stop(applyFinalState);
                    _animations.RemoveAt(i);
                }
            }
        }

        /// <summary>暂停或恢复某个控件上的全部动画。</summary>
        public void SetPaused(SEControl target, bool paused)
        {
            foreach (var animation in _animations)
            {
                if (ReferenceEquals(animation.Target, target))
                    animation.IsPaused = paused;
            }
        }

        /// <summary>暂停或恢复全部动画。</summary>
        public void SetPausedAll(bool paused)
        {
            foreach (var animation in _animations)
                animation.IsPaused = paused;
        }

        public void Clear() => _animations.Clear();

        /// <summary>推进所有动画。</summary>
        public void Update(double deltaTime)
        {
            if (_animations.Count == 0 && _pending.Count == 0)
                return;

            _updating = true;
            for (int i = _animations.Count - 1; i >= 0; i--)
            {
                if (!_animations[i].Update(deltaTime))
                    _animations.RemoveAt(i);
            }
            _updating = false;

            if (_pending.Count > 0)
            {
                _animations.AddRange(_pending);
                _pending.Clear();
            }
        }
    }
}
