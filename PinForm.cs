using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Nail
{
    public partial class PinForm : Form
    {
        private IntPtr _targetWindow = IntPtr.Zero;
        private bool _isEnabled = false;
        private bool _isHidden = false;
        private bool _isDragging = false;
        private Point _dragOffset;
        private Point _mouseDownPoint;
        private Point _offsetFromWindow;
        private Color _pinColor = Color.FromArgb(220, 50, 50);
        private readonly HashSet<IntPtr> _ourHandles;
        private readonly System.Windows.Forms.Timer _checkTimer;
        private ContextMenuStrip _contextMenu;
        private ToolStripMenuItem _enableItem;
        private ToolStripMenuItem _disableItem;
        private ToolStripMenuItem _hideShowItem;
        private ToolStripSeparator _hideShowSep;
        private MenuCloseFilter _menuFilter;

        private static readonly Color[] AvailableColors = new[]
        {
            Color.FromArgb(220, 50, 50),   // Red
            Color.FromArgb(232, 145, 58),  // Orange
            Color.FromArgb(240, 200, 50),  // Yellow
            Color.FromArgb(76, 175, 80),   // Green
            Color.FromArgb(66, 133, 244),  // Blue
            Color.FromArgb(156, 39, 176),  // Purple
        };

        public IntPtr TargetWindow => _targetWindow;
        public bool IsEnabled => _isEnabled;
        public bool IsHidden => _isHidden;
        public string WindowTitle => NativeMethods.GetWindowTitle(_targetWindow);

        public event EventHandler PinStateChanged;
        public event EventHandler PinDeleted;

        public PinForm(HashSet<IntPtr> ourHandles)
        {
            _ourHandles = ourHandles;
            _ourHandles.Add(this.Handle);

            this.Size = new Size(52, 72);
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Fuchsia;
            this.TransparencyKey = Color.Fuchsia;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Cursor = Cursors.SizeAll;

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.SupportsTransparentBackColor, true);

            BuildContextMenu();

            _checkTimer = new System.Windows.Forms.Timer();
            _checkTimer.Interval = 200;
            _checkTimer.Tick += CheckTargetWindow;
            _checkTimer.Start();

            this.MouseDown += PinForm_MouseDown;
            this.MouseMove += PinForm_MouseMove;
            this.MouseUp += PinForm_MouseUp;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            _enableItem = new ToolStripMenuItem("📌 启用置顶", null, OnEnableClick);
            _disableItem = new ToolStripMenuItem("📍 取消置顶", null, OnDisableClick);
            _hideShowItem = new ToolStripMenuItem("", null, OnHideShowClick);
            _hideShowSep = new ToolStripSeparator();

            var colorMenu = new ToolStripMenuItem("🎨 更换颜色");
            var colors = new[] { "红色", "橙色", "黄色", "绿色", "蓝色", "紫色" };
            for (int i = 0; i < colors.Length; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(colors[i], null, (s, e) => OnChangeColor(idx));
                item.ForeColor = AvailableColors[i];
                colorMenu.DropDownItems.Add(item);
            }

            var deleteItem = new ToolStripMenuItem("❌ 删除图钉", null, OnDeleteClick);
            var infoItem = new ToolStripMenuItem("目标窗口: (未附着)");
            infoItem.Enabled = false;

            _contextMenu.Items.Add(_enableItem);
            _contextMenu.Items.Add(_disableItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(_hideShowItem);
            _contextMenu.Items.Add(_hideShowSep);
            _contextMenu.Items.Add(colorMenu);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(deleteItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(infoItem);

            _contextMenu.Opening += (s, e) =>
            {
                bool attached = _targetWindow != IntPtr.Zero;
                _enableItem.Enabled = attached && !_isEnabled;
                _disableItem.Enabled = attached && _isEnabled;
                _hideShowItem.Text = _isHidden ? "👁 显示图钉" : "🔇 隐藏图钉";
                infoItem.Text = attached
                    ? $"目标窗口: {TruncateText(WindowTitle, 40)}"
                    : "目标窗口: (未附着)";
            };
        }

        private void CheckTargetWindow(object sender, EventArgs e)
        {
            if (_targetWindow == IntPtr.Zero) return;

            if (!NativeMethods.IsWindow(_targetWindow))
            {
                _targetWindow = IntPtr.Zero;
                _isEnabled = false;
                this.Visible = !_isHidden;
                this.Invalidate();
                PinStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            bool isMinimized = NativeMethods.IsIconic(_targetWindow);

            if (_isEnabled && isMinimized)
            {
                // Target was minimized — hide pin alongside it
                if (this.Visible)
                    this.Visible = false;
                return;
            }

            if (_isEnabled && !_isDragging && !isMinimized
                && NativeMethods.GetWindowRect(_targetWindow, out var rect))
            {
                // Restore pin visibility if user hasn't manually hidden it
                if (!this.Visible && !_isHidden)
                    this.Visible = true;

                int newX = rect.Left + _offsetFromWindow.X;
                int newY = rect.Top + _offsetFromWindow.Y;
                if (this.Location.X != newX || this.Location.Y != newY)
                {
                    NativeMethods.SetWindowPos(this.Handle, NativeMethods.HWND_TOPMOST,
                        newX, newY, 0, 0,
                        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                }
            }
        }

        private void PinForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _mouseDownPoint = e.Location;
                _dragOffset = e.Location;
                _isDragging = false;
            }
        }

        private void PinForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_isDragging)
            {
                int dx = e.X - _mouseDownPoint.X;
                int dy = e.Y - _mouseDownPoint.Y;
                if (dx * dx + dy * dy > 16)
                {
                    _isDragging = true;
                }
            }

            if (_isDragging)
            {
                Point screen = this.PointToScreen(e.Location);
                this.Location = new Point(screen.X - _dragOffset.X, screen.Y - _dragOffset.Y);
            }
        }

        private void PinForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (_isDragging)
            {
                DetectAndAttachWindow();
            }
            else
            {
                ShowContextMenu(e.Location);
            }

            _isDragging = false;
        }

        private void ShowContextMenu(Point location)
        {
            // Always clean up any previous filter first
            CleanupMenuFilter();
            // Close any lingering menu
            if (_contextMenu.Visible)
                _contextMenu.Close();

            _contextMenu.Show(this, location);

            // Install a fresh message filter to close menu when clicking outside
            _menuFilter = new MenuCloseFilter(_contextMenu);
            _contextMenu.Closed += OnContextMenuClosed;
            Application.AddMessageFilter(_menuFilter);
        }

        private void CleanupMenuFilter()
        {
            if (_menuFilter != null)
            {
                Application.RemoveMessageFilter(_menuFilter);
                _contextMenu.Closed -= OnContextMenuClosed;
                _menuFilter = null;
            }
        }

        private void OnContextMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            CleanupMenuFilter();
        }

        private void DetectAndAttachWindow()
        {
            Point tip = GetTipScreenPoint();
            IntPtr hWnd = GetWindowAtScreenPoint(tip);
            if (hWnd != IntPtr.Zero && hWnd != this.Handle)
            {
                if (_isEnabled && _targetWindow != IntPtr.Zero && _targetWindow != hWnd)
                {
                    NativeMethods.SetWindowPos(_targetWindow, NativeMethods.HWND_NOTOPMOST,
                        0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
                _targetWindow = hWnd;
                _isEnabled = false;

                if (NativeMethods.GetWindowRect(hWnd, out var rect))
                {
                    _offsetFromWindow = new Point(
                        this.Location.X - rect.Left,
                        this.Location.Y - rect.Top
                    );
                }

                this.Invalidate();
                PinStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private Point GetTipScreenPoint()
        {
            return this.PointToScreen(new Point(this.Width / 2, this.Height - 10));
        }

        private IntPtr GetWindowAtScreenPoint(Point screenPoint)
        {
            bool wasVisible = this.Visible;
            this.Visible = false;
            Application.DoEvents();
            System.Threading.Thread.Sleep(30);

            IntPtr hWnd = NativeMethods.WindowFromPoint(screenPoint);
            if (hWnd != IntPtr.Zero)
            {
                hWnd = NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOT);
            }

            this.Visible = wasVisible;

            // Only exclude this pin's own handle; allow other Nail windows
            if (hWnd == this.Handle)
                return IntPtr.Zero;

            if (hWnd != IntPtr.Zero && !NativeMethods.IsWindowVisible(hWnd))
                return IntPtr.Zero;

            return hWnd;
        }

        public void Enable()
        {
            if (_targetWindow == IntPtr.Zero) return;

            NativeMethods.SetWindowPos(_targetWindow, NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);

            NativeMethods.SetWindowPos(this.Handle, NativeMethods.HWND_TOPMOST,
                this.Left, this.Top, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            _isEnabled = true;
            // Only show pin if target is NOT minimized and user hasn't manually hidden it
            bool targetMinimized = NativeMethods.IsIconic(_targetWindow);
            if (!targetMinimized)
            {
                _isHidden = false;
                this.Visible = true;
            }
            else
            {
                _isHidden = false;
                this.Visible = false;
            }
            this.Invalidate();
            PinStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Disable()
        {
            if (_targetWindow == IntPtr.Zero) return;
            NativeMethods.SetWindowPos(_targetWindow, NativeMethods.HWND_NOTOPMOST,
                0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            _isEnabled = false;
            this.Invalidate();
            PinStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void HidePin()
        {
            _isHidden = true;
            this.Visible = false;
            PinStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ShowPin()
        {
            _isHidden = false;
            this.Visible = true;
            PinStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleHidden()
        {
            if (_isHidden) ShowPin(); else HidePin();
        }

        public void Cleanup()
        {
            CleanupMenuFilter();
            if (_isEnabled && _targetWindow != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(_targetWindow, NativeMethods.HWND_NOTOPMOST,
                    0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
            _checkTimer.Stop();
            _ourHandles.Remove(this.Handle);
        }

        private void OnEnableClick(object sender, EventArgs e) => Enable();
        private void OnDisableClick(object sender, EventArgs e) => Disable();
        private void OnHideShowClick(object sender, EventArgs e) => ToggleHidden();

        private void OnChangeColor(int index)
        {
            if (index >= 0 && index < AvailableColors.Length)
            {
                _pinColor = AvailableColors[index];
                this.Invalidate();
            }
        }

        private void OnDeleteClick(object sender, EventArgs e)
        {
            this.Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Cleanup();
            PinDeleted?.Invoke(this, EventArgs.Empty);
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color color = _pinColor;
            if (_targetWindow != IntPtr.Zero && _isEnabled)
                color = Color.FromArgb(76, 175, 80);

            Color darkColor = ControlPaint.Dark(color, 0.25f);
            int cx = this.Width / 2;
            int headRadius = 14;
            int headTop = 4;

            // Needle shaft
            using (var needlePen = new Pen(Color.FromArgb(160, 160, 160), 4))
            {
                needlePen.StartCap = LineCap.Round;
                needlePen.EndCap = LineCap.Round;
                g.DrawLine(needlePen, cx, headTop + headRadius * 2 - 6, cx, headTop + headRadius * 2 + 18);
            }

            // Needle point
            using (var pointBrush = new SolidBrush(Color.FromArgb(120, 120, 120)))
            {
                Point[] pointPts =
                {
                    new Point(cx - 5, headTop + headRadius * 2 + 14),
                    new Point(cx + 5, headTop + headRadius * 2 + 14),
                    new Point(cx, headTop + headRadius * 2 + 28)
                };
                g.FillPolygon(pointBrush, pointPts);
            }

            // Head shadow
            var headRect = new Rectangle(cx - headRadius, headTop, headRadius * 2, headRadius * 2);
            using (var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, headRect.X + 2, headRect.Y + 2, headRect.Width, headRect.Height);
            }

            // Head
            using (var headBrush = new SolidBrush(color))
            {
                g.FillEllipse(headBrush, headRect);
            }

            // Head border
            using (var borderPen = new Pen(darkColor, 1.5f))
            {
                g.DrawEllipse(borderPen, headRect);
            }

            // Head highlight (glass reflection)
            var highlightRect = new Rectangle(cx - 7, headTop + 5, 9, 11);
            using (var highlightBrush = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
            {
                g.FillEllipse(highlightBrush, highlightRect);
            }

            // Inner circle detail
            var innerRect = new Rectangle(cx - 3, headTop + headRadius - 2, 6, 6);
            using (var innerBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            {
                g.FillEllipse(innerBrush, innerRect);
            }
        }

        private static string TruncateText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "(无标题)";
            return text.Length <= maxLen ? text : text.Substring(0, maxLen - 3) + "...";
        }
    }

    internal class MenuCloseFilter : IMessageFilter
    {
        private readonly ContextMenuStrip _menu;

        public MenuCloseFilter(ContextMenuStrip menu)
        {
            _menu = menu;
        }

        public bool PreFilterMessage(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_RBUTTONDOWN = 0x0204;
            const int WM_MBUTTONDOWN = 0x0207;
            const int WM_NCLBUTTONDOWN = 0x00A1;

            if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_RBUTTONDOWN ||
                m.Msg == WM_MBUTTONDOWN || m.Msg == WM_NCLBUTTONDOWN)
            {
                if (_menu == null || !_menu.Visible) return false;

                Point pt = Control.MousePosition;
                if (!_menu.Bounds.Contains(pt))
                {
                    _menu.Close();
                }
            }
            return false;
        }
    }
}
