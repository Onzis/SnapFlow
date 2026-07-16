using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Screenshoter
{
    public partial class OverlayWindow : Window
    {
        private enum Mode { Idle, Drawing, Moving }
        private Mode _mode = Mode.Idle;

        private Point _startPoint;           // начало рисования/перемещения
        private Rect _selection;             // текущее выделение (в DIP)
        private Rect _selectionAtDragStart;  // снапшот при старте move
        private bool _hasSelection;

        private readonly BitmapSource _fullShot; // полный кадр экрана
        private const double Handle = 8;
        private const double Pad = 4;

        public OverlayWindow(BitmapSource fullShot, double left, double top,
                             double width, double height)
        {
            InitializeComponent();

            _fullShot = fullShot;
            BackgroundImage.Source = fullShot;

            Left = left; Top = top; Width = width; Height = height;

            Loaded += (_, __) =>
            {
                FullRectGeo.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
                HookThumbs();

                // плавное появление затемнения
                DimPath.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 0.4, TimeSpan.FromSeconds(0.18)));
            };

            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            KeyDown += OnKeyDown;

            BtnClose.Click += (_, __) => _ = CloseAsync();
            BtnCopy.Click += (_, __) => _ = CopyAsync();
            BtnSave.Click += (_, __) => _ = SaveAsync();
        }

        // ---------- Горячие клавиши ----------
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { _ = CloseAsync(); return; }
            if (!_hasSelection) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl && e.Key == Key.C) { _ = CopyAsync(); e.Handled = true; }
            else if (ctrl && e.Key == Key.S) { _ = SaveAsync(); e.Handled = true; }
        }

        // ---------- Рисование / перемещение области ----------
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var p = e.GetPosition(this);

            if (_hasSelection && _selection.Contains(p))
            {
                _mode = Mode.Moving;
                _startPoint = p;
                _selectionAtDragStart = _selection;
            }
            else
            {
                // клик вне области -> сброс и новое рисование
                _mode = Mode.Drawing;
                _startPoint = p;
                _hasSelection = false;
                HideChrome();
            }
            CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_mode == Mode.Idle) return;
            var p = e.GetPosition(this);

            if (_mode == Mode.Drawing)
            {
                double x = Math.Min(p.X, _startPoint.X);
                double y = Math.Min(p.Y, _startPoint.Y);
                _selection = new Rect(x, y, Math.Abs(p.X - _startPoint.X),
                                            Math.Abs(p.Y - _startPoint.Y));
                UpdateSelection(false);
            }
            else if (_mode == Mode.Moving)
            {
                double dx = p.X - _startPoint.X;
                double dy = p.Y - _startPoint.Y;
                double nx = Clamp(_selectionAtDragStart.X + dx, 0, ActualWidth - _selection.Width);
                double ny = Clamp(_selectionAtDragStart.Y + dy, 0, ActualHeight - _selection.Height);
                _selection = new Rect(nx, ny, _selection.Width, _selection.Height);
                UpdateSelection(true);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
            if (_mode == Mode.Drawing && _selection.Width > 4 && _selection.Height > 4)
                _hasSelection = true;

            _mode = Mode.Idle;

            if (_hasSelection) UpdateSelection(true);
            else HideChrome();
        }

        // ---------- Отрисовка выреза, маркеров и панели ----------
        private void UpdateSelection(bool showChrome)
        {
            SelectionGeo.Rect = _selection;
            SelectionBorder.Visibility = Visibility.Visible;
            SelectionBorder.Width = _selection.Width;
            SelectionBorder.Height = _selection.Height;
            SelectionBorder.Margin = new Thickness(_selection.X, _selection.Y, 0, 0);

            if (showChrome) { LayoutThumbs(); LayoutToolbar(); ShowChrome(); }
            else HideThumbsAndToolbar();
        }

        private void LayoutThumbs()
        {
            double l = _selection.Left, t = _selection.Top;
            double r = _selection.Right, b = _selection.Bottom;
            double cx = l + _selection.Width / 2, cy = t + _selection.Height / 2;
            double h = Handle / 2;

            Place(ThumbTL, l - h, t - h); Place(ThumbT, cx - h, t - h); Place(ThumbTR, r - h, t - h);
            Place(ThumbR, r - h, cy - h); Place(ThumbBR, r - h, b - h); Place(ThumbB, cx - h, b - h);
            Place(ThumbBL, l - h, b - h); Place(ThumbL, l - h, cy - h);
        }

        private static void Place(Thumb t, double x, double y)
        {
            Canvas.SetLeft(t, x); Canvas.SetTop(t, y); t.Visibility = Visibility.Visible;
        }

        // Панель ПОД областью выделения, выровнена по правому краю, отступ 4px
        private void LayoutToolbar()
        {
            Toolbar.Visibility = Visibility.Visible;
            Toolbar.Measure(new Size(double.PositiveInfinity, 32));
            double w = Toolbar.DesiredSize.Width;

            double x = _selection.Right - w;
            double y = _selection.Bottom + Pad;

            // если снизу не помещается — размещаем над областью
            if (y + 32 > ActualHeight) y = _selection.Top - 32 - Pad;

            // удержание в пределах экрана по горизонтали
            x = Clamp(x, 0, ActualWidth - w);
            Canvas.SetLeft(Toolbar, x);
            Canvas.SetTop(Toolbar, y);
        }

        private bool _chromeShown;

        private void ShowChrome()
        {
            if (_chromeShown) { Toolbar.Opacity = 0.85; return; }
            _chromeShown = true;

            Toolbar.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0.85, TimeSpan.FromSeconds(0.14)));

            var pop = new DoubleAnimation(0.85, 1.0, TimeSpan.FromSeconds(0.2))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            };
            ToolbarScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            ToolbarScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        private void HideThumbsAndToolbar()
        {
            foreach (var t in Thumbs()) t.Visibility = Visibility.Collapsed;
            Toolbar.Visibility = Visibility.Collapsed;
        }

        private void HideChrome()
        {
            _chromeShown = false;
            Toolbar.BeginAnimation(OpacityProperty, null);
            Toolbar.Opacity = 0;
            SelectionBorder.Visibility = Visibility.Collapsed;
            SelectionGeo.Rect = Rect.Empty;
            HideThumbsAndToolbar();
        }

        private Thumb[] Thumbs() => new[]
            { ThumbTL, ThumbT, ThumbTR, ThumbR, ThumbBR, ThumbB, ThumbBL, ThumbL };

        // ---------- Ресайз через маркеры ----------
        private void HookThumbs()
        {
            ThumbTL.DragDelta += (_, e) => Resize(e, true, true, false, false);
            ThumbT.DragDelta += (_, e) => Resize(e, false, true, false, false);
            ThumbTR.DragDelta += (_, e) => Resize(e, false, true, true, false);
            ThumbR.DragDelta += (_, e) => Resize(e, false, false, true, false);
            ThumbBR.DragDelta += (_, e) => Resize(e, false, false, true, true);
            ThumbB.DragDelta += (_, e) => Resize(e, false, false, false, true);
            ThumbBL.DragDelta += (_, e) => Resize(e, true, false, false, true);
            ThumbL.DragDelta += (_, e) => Resize(e, true, false, false, false);
        }

        private void Resize(DragDeltaEventArgs e, bool left, bool top, bool right, bool bottom)
        {
            double x = _selection.X, y = _selection.Y, w = _selection.Width, h = _selection.Height;

            if (left) { x += e.HorizontalChange; w -= e.HorizontalChange; }
            if (right) { w += e.HorizontalChange; }
            if (top) { y += e.VerticalChange; h -= e.VerticalChange; }
            if (bottom) { h += e.VerticalChange; }

            if (w < 10 || h < 10) return;
            if (x < 0 || y < 0 || x + w > ActualWidth || y + h > ActualHeight) return;

            _selection = new Rect(x, y, w, h);
            UpdateSelection(true);
        }

        private static double Clamp(double v, double min, double max)
            => v < min ? min : (v > max ? max : v);

        // ---------- Асинхронные действия ----------
        private async Task CopyAsync()
        {
            try
            {
                var rect = _selection;
                HideChrome();
                await PlayCaptureFlashAsync(rect);
                await FadeOutAsync();
                var crop = ScreenshotHelper.Crop(_fullShot, rect, ActualWidth, ActualHeight);
                await ScreenshotHelper.CopyToClipboardAsync(crop);
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OverlayWindow.CopyAsync");
                Close();
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                var rect = _selection;
                var crop = ScreenshotHelper.Crop(_fullShot, rect, ActualWidth, ActualHeight);
                HideChrome();
                await PlayCaptureFlashAsync(rect);
                await FadeOutAsync();
                await ScreenshotHelper.SaveAsync(crop);
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OverlayWindow.SaveAsync");
                Close();
            }
        }

        private async Task CloseAsync()
        {
            try
            {
                await FadeOutAsync();
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OverlayWindow.CloseAsync");
                Close();
            }
        }

        // Вспышка по области выделения — эффект «снимка».
        private System.Threading.Tasks.Task PlayCaptureFlashAsync(Rect area)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

            FlashRect.Width = area.Width;
            FlashRect.Height = area.Height;
            FlashRect.Margin = new Thickness(area.X, area.Y, 0, 0);
            FlashRect.Visibility = Visibility.Visible;

            var flash = new DoubleAnimationUsingKeyFrames();
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.05))));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.28))));
            flash.Completed += (_, __) => tcs.SetResult(true);

            FlashRect.BeginAnimation(OpacityProperty, flash);
            return tcs.Task;
        }

        private System.Threading.Tasks.Task FadeOutAsync()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var anim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.1));
            anim.Completed += (_, __) => tcs.SetResult(true);
            RootGrid.BeginAnimation(OpacityProperty, anim);
            return tcs.Task;
        }
    }
}
