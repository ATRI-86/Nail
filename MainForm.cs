using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Nail
{
    public partial class MainForm : Form
    {
        private readonly List<PinForm> _pins = new List<PinForm>();
        private readonly HashSet<IntPtr> _ourHandles = new HashSet<IntPtr>();
        private IntPtr _lastForegroundWindow = IntPtr.Zero;
        private System.Windows.Forms.Timer _foregroundTracker;
        private NotifyIcon _notifyIcon;
        private Bitmap _iconBitmap;

        private FlowLayoutPanel _pinListPanel;
        private Label _hintLabel;
        private Button _newPinButton;
        private Button _pinActiveButton;
        private Panel _headerPanel;
        private Label _separatorLabel;

        public MainForm()
        {
            _ourHandles.Add(this.Handle);
            InitializeUI();
            StartForegroundTracker();
            SetupTrayIcon();
        }

        private void InitializeUI()
        {
            this.Text = "Nail - 窗口图钉";
            this.Size = new Size(400, 520);
            this.MinimumSize = new Size(320, 380);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = Color.FromArgb(245, 245, 245);
            this.Font = new Font("Microsoft YaHei UI", 9f);

            // Hint label (bottom)
            _hintLabel = new Label
            {
                Text = "  提示: 拖动图钉到目标窗口上 → 点击图钉 → 选择「启用置顶」",
                Dock = DockStyle.Bottom,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Microsoft YaHei UI", 8f),
                BackColor = Color.FromArgb(235, 235, 235)
            };

            // Pin list panel
            _pinListPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(6, 4, 6, 4)
            };

            // Separator
            _separatorLabel = new Label
            {
                Text = "  ── 已创建的图钉 ──",
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 240, 240)
            };

            // Header panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = Color.FromArgb(250, 250, 250)
            };

            var titleLabel = new Label
            {
                Text = "🔩  Nail - 窗口图钉工具",
                Location = new Point(16, 14),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            _newPinButton = new Button
            {
                Text = "📌  新建图钉",
                Location = new Point(20, 56),
                Size = new Size(130, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(220, 50, 50),
                Cursor = Cursors.Hand
            };
            _newPinButton.FlatAppearance.BorderSize = 0;
            _newPinButton.Click += (s, e) => CreateNewPin();

            _pinActiveButton = new Button
            {
                Text = "🎯  置顶活跃窗口",
                Location = new Point(162, 56),
                Size = new Size(130, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9f),
                ForeColor = Color.FromArgb(60, 60, 60),
                BackColor = Color.FromArgb(224, 224, 224),
                Cursor = Cursors.Hand
            };
            _pinActiveButton.FlatAppearance.BorderSize = 0;
            _pinActiveButton.Click += (s, e) => PinActiveWindow();

            _headerPanel.Controls.Add(titleLabel);
            _headerPanel.Controls.Add(_newPinButton);
            _headerPanel.Controls.Add(_pinActiveButton);

            // Add controls in correct docking order
            this.Controls.Add(_pinListPanel);
            this.Controls.Add(_hintLabel);
            this.Controls.Add(_separatorLabel);
            this.Controls.Add(_headerPanel);

            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;
        }

        private void StartForegroundTracker()
        {
            _foregroundTracker = new System.Windows.Forms.Timer();
            _foregroundTracker.Interval = 400;
            _foregroundTracker.Tick += (s, e) =>
            {
                IntPtr fg = NativeMethods.GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    _lastForegroundWindow = fg;
                }
            };
            _foregroundTracker.Start();
        }

        private void SetupTrayIcon()
        {
            _iconBitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(_iconBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(220, 50, 50)))
                    g.FillEllipse(brush, 4, 2, 24, 24);
                using (var pen = new Pen(Color.FromArgb(180, 50, 50), 2))
                    g.DrawEllipse(pen, 4, 2, 24, 24);
                using (var brush = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                    g.FillEllipse(brush, 10, 6, 7, 7);
                using (var pen = new Pen(Color.FromArgb(130, 130, 130), 3))
                    g.DrawLine(pen, 16, 26, 16, 31);
            }

            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示主窗口", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.Activate(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("退出", null, (s, e) => { ExitApplication(); });

            _notifyIcon = new NotifyIcon
            {
                Icon = Icon.FromHandle(_iconBitmap.GetHicon()),
                Text = "Nail - 窗口图钉",
                Visible = true,
                ContextMenuStrip = trayMenu
            };
            _notifyIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.Activate(); };
        }

        private void CreateNewPin()
        {
            var pin = new PinForm(_ourHandles);
            pin.PinStateChanged += Pin_PinStateChanged;
            pin.PinDeleted += Pin_PinDeleted;
            _pins.Add(pin);
            pin.Show();
            RefreshPinList();
        }

        private void PinActiveWindow()
        {
            if (_lastForegroundWindow == IntPtr.Zero || !NativeMethods.IsWindow(_lastForegroundWindow))
            {
                MessageBox.Show("没有检测到可置顶的窗口。\n请先点击你想要置顶的窗口，再使用此功能。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var p in _pins)
            {
                if (p.TargetWindow == _lastForegroundWindow)
                {
                    if (!p.IsEnabled) p.Enable();
                    MessageBox.Show($"该窗口已被图钉附着，已确保置顶。\n窗口: {p.WindowTitle}",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            var pin = new PinForm(_ourHandles);
            pin.PinStateChanged += Pin_PinStateChanged;
            pin.PinDeleted += Pin_PinDeleted;

            if (NativeMethods.GetWindowRect(_lastForegroundWindow, out var rect))
            {
                pin.Location = new Point(rect.Right - 60, rect.Top + 10);
            }

            _pins.Add(pin);
            pin.Show();
            pin.Enable();
            RefreshPinList();
        }

        private void Pin_PinStateChanged(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => RefreshPinList()));
            }
            else
            {
                RefreshPinList();
            }
        }

        private void Pin_PinDeleted(object sender, EventArgs e)
        {
            if (sender is PinForm pin)
            {
                RemovePin(pin);
            }
        }

        private void RemovePin(PinForm pin)
        {
            _pins.Remove(pin);
            RefreshPinList();
        }

        private void RefreshPinList()
        {
            _pinListPanel.SuspendLayout();
            _pinListPanel.Controls.Clear();

            foreach (var pin in _pins)
            {
                var entry = CreatePinEntry(pin);
                _pinListPanel.Controls.Add(entry);
            }

            if (_pins.Count == 0)
            {
                var emptyLabel = new Label
                {
                    Text = "  还没有图钉，点击「📌 新建图钉」创建一个吧",
                    AutoSize = true,
                    ForeColor = Color.FromArgb(160, 160, 160),
                    Font = new Font("Microsoft YaHei UI", 9f),
                    Padding = new Padding(8, 12, 0, 0)
                };
                _pinListPanel.Controls.Add(emptyLabel);
            }

            _pinListPanel.ResumeLayout();
            UpdateEntryWidths();

            _separatorLabel.Text = _pins.Count > 0
                ? $"  ── 已创建的图钉 ({_pins.Count}) ──"
                : "  ── 已创建的图钉 ──";
        }

        private Panel CreatePinEntry(PinForm pin)
        {
            var panel = new Panel
            {
                Height = 44,
                Margin = new Padding(0, 0, 0, 3),
                BackColor = Color.White,
                Tag = pin,
                Cursor = Cursors.Default
            };

            panel.Paint += (s, e) =>
            {
                var p = (Panel)s;
                using (var pen = new Pen(Color.FromArgb(225, 225, 225)))
                    e.Graphics.DrawLine(pen, 8, p.Height - 1, p.Width - 8, p.Height - 1);
            };

            // Color indicator
            Color indicatorColor = GetIndicatorColor(pin);
            var colorBox = new Panel
            {
                Size = new Size(12, 12),
                Location = new Point(12, 16),
                BackColor = indicatorColor
            };
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, 12, 12);
                colorBox.Region = new Region(path);
            }

            // Window title + status
            string title = pin.WindowTitle;
            string statusText;
            if (pin.TargetWindow == IntPtr.Zero)
                statusText = "(未附着)";
            else if (pin.IsHidden)
                statusText = "(已隐藏)";
            else if (pin.IsEnabled)
                statusText = "(已置顶)";
            else
                statusText = "(已禁用)";

            var titleLabel = new Label
            {
                Text = $"{TruncateText(title, 20)}  {statusText}",
                Location = new Point(32, 0),
                AutoSize = false,
                Size = new Size(200, 44),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 9f),
                ForeColor = (pin.TargetWindow == IntPtr.Zero || pin.IsHidden)
                    ? Color.FromArgb(160, 160, 160)
                    : Color.FromArgb(40, 40, 40)
            };

            // Toggle button
            var toggleBtn = new Button
            {
                Text = pin.IsEnabled ? "禁用" : "启用",
                Size = new Size(52, 28),
                Location = new Point(100, 8),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 8f),
                ForeColor = pin.IsEnabled ? Color.FromArgb(200, 120, 50) : Color.FromArgb(60, 140, 60),
                BackColor = Color.FromArgb(248, 248, 248),
                Cursor = Cursors.Hand,
                Enabled = pin.TargetWindow != IntPtr.Zero
            };
            toggleBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            toggleBtn.Click += (s, e) =>
            {
                if (pin.IsEnabled) pin.Disable(); else pin.Enable();
                RefreshPinList();
            };

            // Hide/Show button
            var hideBtn = new Button
            {
                Text = pin.IsHidden ? "👁" : "🔇",
                Size = new Size(30, 28),
                Location = new Point(100, 8),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 8f),
                ForeColor = Color.FromArgb(100, 100, 100),
                BackColor = Color.FromArgb(248, 248, 248),
                Cursor = Cursors.Hand
            };
            hideBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            hideBtn.Click += (s, e) =>
            {
                pin.ToggleHidden();
                RefreshPinList();
            };

            // Delete button
            var deleteBtn = new Button
            {
                Text = "×",
                Size = new Size(30, 28),
                Location = new Point(100, 8),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 60, 60),
                BackColor = Color.FromArgb(248, 248, 248),
                Cursor = Cursors.Hand
            };
            deleteBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            deleteBtn.Click += (s, e) =>
            {
                pin.Close();
            };

            panel.Controls.Add(colorBox);
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(toggleBtn);
            panel.Controls.Add(hideBtn);
            panel.Controls.Add(deleteBtn);

            void LayoutButtons(Panel p)
            {
                int w = p.Width;
                deleteBtn.Left = w - 40;
                hideBtn.Left = w - 78;
                toggleBtn.Left = w - 140;
                titleLabel.Width = Math.Max(60, w - 210);
            }

            panel.Resize += (s, e) => LayoutButtons(panel);
            LayoutButtons(panel);

            return panel;
        }

        private static Color GetIndicatorColor(PinForm pin)
        {
            if (pin.TargetWindow == IntPtr.Zero) return Color.FromArgb(220, 50, 50);
            if (pin.IsHidden) return Color.FromArgb(180, 180, 180);
            if (pin.IsEnabled) return Color.FromArgb(76, 175, 80);
            return Color.FromArgb(232, 145, 58);
        }

        private void UpdateEntryWidths()
        {
            int availableWidth = _pinListPanel.ClientSize.Width - _pinListPanel.Padding.Horizontal - 8;
            foreach (Control c in _pinListPanel.Controls)
            {
                if (c is Panel p && p.Tag is PinForm)
                {
                    p.Width = availableWidth;
                }
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            UpdateEntryWidths();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                _notifyIcon?.ShowBalloonTip(2000, "Nail", "程序已最小化到系统托盘，图钉仍继续工作。",
                    ToolTipIcon.Info);
            }
            else
            {
                ExitApplication();
            }
        }

        private void ExitApplication()
        {
            _foregroundTracker?.Stop();
            foreach (var pin in _pins.ToArray())
            {
                pin.Close();
            }
            _pins.Clear();
            _notifyIcon?.Dispose();
            _iconBitmap?.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _foregroundTracker?.Stop();
                foreach (var pin in _pins.ToArray())
                {
                    pin.Close();
                }
                _notifyIcon?.Dispose();
                _iconBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }

        private static string TruncateText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "(无标题)";
            return text.Length <= maxLen ? text : text.Substring(0, maxLen - 3) + "...";
        }
    }
}
