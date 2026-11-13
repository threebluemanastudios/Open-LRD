using System;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.Visualizer
{
    internal sealed class FlyCameraController : IDisposable
    {
        private readonly UIElement host;
        private readonly Window parentWindow;
        private readonly VisualizerScene scene;
        private readonly FlyCamera flyCamera;
        private readonly Stopwatch frameStopwatch = new Stopwatch();

        private bool isRunning;
        private bool isMouseLooking;
        private Point lastMousePosition;
        private double speedMultiplier = 1.0;
        private float sceneRadius = 10f;
        private bool isDisposed;

        public event Action? CameraMoved;
        public event Action<double>? SpeedChanged;

        public FlyCameraController(
            UIElement host,
            Window parentWindow,
            VisualizerScene scene)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));

            flyCamera = scene.FlyCamera;
            sceneRadius = Math.Max(scene.Radius, 1f);

            Start();
        }

        public FlyCamera FlyCamera => flyCamera;

        public double SpeedMultiplier
        {
            get => speedMultiplier;
            set
            {
                double clamped = Math.Clamp(value, 0.5, 4.0);
                if (Math.Abs(speedMultiplier - clamped) > 1e-4)
                {
                    speedMultiplier = clamped;
                    SpeedChanged?.Invoke(speedMultiplier);
                }
            }
        }

        public double BaseSpeed => Math.Max(sceneRadius * 0.5, 1.0);

        public double EffectiveSpeed
        {
            get
            {
                bool isBoosted = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                return BaseSpeed * speedMultiplier * (isBoosted ? 4.0 : 1.0);
            }
        }

        public void SetSceneRadius(float radius)
        {
            sceneRadius = Math.Max(radius, 1f);
        }

        public void Start()
        {
            if (isRunning || isDisposed)
                return;

            isRunning = true;
            frameStopwatch.Restart();
            CompositionTarget.Rendering += OnRendering;
        }

        public void Stop()
        {
            if (!isRunning)
                return;

            isRunning = false;
            frameStopwatch.Stop();
            CompositionTarget.Rendering -= OnRendering;

            if (isMouseLooking)
            {
                isMouseLooking = false;
                host.ReleaseMouseCapture();
            }
        }

        public void HandleMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                isMouseLooking = true;
                lastMousePosition = e.GetPosition(host);
                host.CaptureMouse();
                e.Handled = true;
            }
        }

        public void HandleMouseUp(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right && isMouseLooking)
            {
                isMouseLooking = false;
                host.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        public void HandleMouseMove(MouseEventArgs e)
        {
            if (!isMouseLooking)
                return;

            Point currentPosition = e.GetPosition(host);
            double deltaX = currentPosition.X - lastMousePosition.X;
            double deltaY = currentPosition.Y - lastMousePosition.Y;
            lastMousePosition = currentPosition;

            if (Math.Abs(deltaX) > 1e-4 || Math.Abs(deltaY) > 1e-4)
            {
                flyCamera.Look(deltaX, deltaY);
                scene.SyncCameraFromFlyCamera();
                CameraMoved?.Invoke();
            }

            e.Handled = true;
        }

        public void HandleMouseWheel(MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            SpeedMultiplier *= factor;
            e.Handled = true;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!isRunning || isDisposed)
                return;

            double dt = frameStopwatch.Elapsed.TotalSeconds;
            frameStopwatch.Restart();

            // Clamp delta time to 100ms to avoid tunneling/teleporting after hiccups
            if (dt > 0.1)
                dt = 0.1;

            if (dt <= 0.0)
                return;

            // Do not integrate keyboard navigation if the window is inactive
            if (!parentWindow.IsActive)
                return;

            // Suppress movement while typing in a text field
            IInputElement? focused = Keyboard.FocusedElement;
            if (focused is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox)
                return;

            double forward = 0.0;
            double strafe = 0.0;
            double vertical = 0.0;

            if (Keyboard.IsKeyDown(Key.W))
                forward += 1.0;
            if (Keyboard.IsKeyDown(Key.S))
                forward -= 1.0;
            if (Keyboard.IsKeyDown(Key.D))
                strafe += 1.0;
            if (Keyboard.IsKeyDown(Key.A))
                strafe -= 1.0;
            if (Keyboard.IsKeyDown(Key.E))
                vertical += 1.0;
            if (Keyboard.IsKeyDown(Key.Q))
                vertical -= 1.0;

            if (forward == 0.0 && strafe == 0.0 && vertical == 0.0)
                return;

            flyCamera.Move(forward, strafe, vertical, EffectiveSpeed, dt);
            scene.SyncCameraFromFlyCamera();
            CameraMoved?.Invoke();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            Stop();
        }
    }
}
