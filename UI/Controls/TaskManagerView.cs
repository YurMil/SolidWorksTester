using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SolidWorksTester.UI.Models;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Controls
{
    /// <summary>
    /// Minimalist batch task grid: Task / Status / Attempt / Start / Elapsed(+bar) / Stage.
    /// </summary>
    internal sealed class TaskManagerView : UserControl
    {
        private readonly ListView _list;
        private readonly List<BatchTaskItem> _tasks = new();
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _skipMenu;
        private readonly ToolStripMenuItem _retryMenu;
        private readonly ToolStripMenuItem _copyPathMenu;
        private readonly System.Windows.Forms.Timer _tickTimer;

        public event EventHandler? SelectionChanged;
        public event EventHandler? SkipRequested;
        public event EventHandler? RetryRequested;
        public event EventHandler? TasksChanged;

        public TaskManagerView()
        {
            BackColor = UiTheme.Surface;
            UiControlHelper.EnableDoubleBuffer(this);

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.AppFont,
                ShowItemToolTips = true
            };

            _list.Columns.Add("Task", 280);
            _list.Columns.Add("Status", 100);
            _list.Columns.Add("Attempt", 60);
            _list.Columns.Add("Start", 120);
            _list.Columns.Add("Elapsed", 110);
            _list.Columns.Add("Stage", 140);

            _list.SelectedIndexChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
            _list.DrawColumnHeader += OnDrawColumnHeader;
            _list.DrawItem += (_, e) => e.DrawDefault = false;
            _list.DrawSubItem += OnDrawSubItem;
            _list.Resize += (_, _) => ResizeColumns();

            _skipMenu = new ToolStripMenuItem("Skip", null, (_, _) => SkipRequested?.Invoke(this, EventArgs.Empty));
            _retryMenu = new ToolStripMenuItem("Retry", null, (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty));
            _copyPathMenu = new ToolStripMenuItem("Copy path", null, (_, _) => CopySelectedPaths());
            _menu = new ContextMenuStrip();
            _menu.Items.Add(_skipMenu);
            _menu.Items.Add(_retryMenu);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_copyPathMenu);
            _menu.Opening += OnMenuOpening;
            _list.ContextMenuStrip = _menu;

            _tickTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _tickTimer.Tick += (_, _) => TickRunningRows();

            Controls.Add(_list);
            ResizeColumns();
        }

        public IReadOnlyList<BatchTaskItem> Tasks
        {
            get
            {
                lock (_tasks)
                    return _tasks.ToList();
            }
        }

        public int TaskCount
        {
            get
            {
                lock (_tasks)
                    return _tasks.Count;
            }
        }

        public BatchTaskItem? SelectedTask
        {
            get
            {
                if (_list.SelectedItems.Count == 0)
                    return null;
                return _list.SelectedItems[0].Tag as BatchTaskItem;
            }
        }

        public IReadOnlyList<BatchTaskItem> SelectedTasks =>
            _list.SelectedItems.Cast<ListViewItem>()
                .Select(i => i.Tag as BatchTaskItem)
                .Where(t => t != null)
                .Cast<BatchTaskItem>()
                .ToList();

        public void SetInteractionEnabled(bool enabled)
        {
            // List stays selectable during batch for log browsing; editing is gated by host buttons.
            _list.AllowDrop = enabled;
        }

        public void StartElapsedTicker()
        {
            if (!_tickTimer.Enabled)
                _tickTimer.Start();
        }

        public void StopElapsedTicker()
        {
            _tickTimer.Stop();
        }

        public int AddPaths(IEnumerable<string> paths)
        {
            var existing = new HashSet<string>(
                Tasks.Select(t => t.PartPath),
                StringComparer.OrdinalIgnoreCase);

            int added = 0;
            foreach (string path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (!existing.Add(path))
                    continue;

                var task = new BatchTaskItem(path);
                lock (_tasks)
                    _tasks.Add(task);

                _list.Items.Add(CreateListItem(task));
                added++;
            }

            if (added > 0)
                TasksChanged?.Invoke(this, EventArgs.Empty);

            return added;
        }

        public void RemoveSelected()
        {
            var selected = SelectedTasks;
            if (selected.Count == 0)
                return;

            foreach (BatchTaskItem task in selected)
            {
                lock (_tasks)
                    _tasks.Remove(task);

                for (int i = _list.Items.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(_list.Items[i].Tag, task))
                        _list.Items.RemoveAt(i);
                }
            }

            TasksChanged?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearTasks()
        {
            lock (_tasks)
                _tasks.Clear();
            _list.Items.Clear();
            TasksChanged?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshTask(BatchTaskItem task)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<BatchTaskItem>(RefreshTask), task);
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            foreach (ListViewItem item in _list.Items)
            {
                if (!ReferenceEquals(item.Tag, task))
                    continue;

                ApplyTaskToItem(item, task);
                _list.Invalidate(item.Bounds);
                return;
            }
        }

        public void RefreshAll()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(RefreshAll));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            foreach (ListViewItem item in _list.Items)
            {
                if (item.Tag is BatchTaskItem task)
                    ApplyTaskToItem(item, task);
            }

            _list.Invalidate();
        }

        public void SelectTask(BatchTaskItem task)
        {
            foreach (ListViewItem item in _list.Items)
            {
                item.Selected = ReferenceEquals(item.Tag, task);
                if (item.Selected)
                    item.EnsureVisible();
            }
        }

        public IReadOnlyList<BatchTaskItem> GetRunnableTasks()
        {
            lock (_tasks)
            {
                return _tasks
                    .Where(t => t.Status is BatchTaskStatus.Pending)
                    .ToList();
            }
        }

        public IReadOnlyList<BatchTaskItem> SnapshotAll()
        {
            lock (_tasks)
                return _tasks.ToList();
        }

        private void TickRunningRows()
        {
            bool anyRunning = false;
            foreach (ListViewItem item in _list.Items)
            {
                if (item.Tag is not BatchTaskItem task)
                    continue;
                if (task.Status != BatchTaskStatus.Running)
                    continue;

                anyRunning = true;
                task.TickElapsed();
                ApplyTaskToItem(item, task);
            }

            if (anyRunning)
                _list.Invalidate();
            else
                _tickTimer.Stop();
        }

        private static ListViewItem CreateListItem(BatchTaskItem task)
        {
            var item = new ListViewItem(task.DisplayTask) { Tag = task, ToolTipText = task.PartPath };
            item.SubItems.Add(task.StatusText);
            item.SubItems.Add(task.AttemptText);
            item.SubItems.Add(task.StartText);
            item.SubItems.Add(task.ElapsedText);
            item.SubItems.Add(task.StageText);
            return item;
        }

        private static void ApplyTaskToItem(ListViewItem item, BatchTaskItem task)
        {
            item.Text = task.DisplayTask;
            item.ToolTipText = task.PartPath;
            EnsureSubItem(item, 1).Text = task.StatusText;
            EnsureSubItem(item, 2).Text = task.AttemptText;
            EnsureSubItem(item, 3).Text = task.StartText;
            EnsureSubItem(item, 4).Text = task.ElapsedText;
            EnsureSubItem(item, 5).Text = task.StageText;
        }

        private static ListViewItem.ListViewSubItem EnsureSubItem(ListViewItem item, int index)
        {
            while (item.SubItems.Count <= index)
                item.SubItems.Add(string.Empty);
            return item.SubItems[index];
        }

        private void ResizeColumns()
        {
            if (_list.Columns.Count < 6 || _list.ClientSize.Width <= 0)
                return;

            int width = _list.ClientSize.Width;
            _list.Columns[0].Width = Math.Max(160, (int)(width * 0.34));
            _list.Columns[1].Width = Math.Max(72, (int)(width * 0.12));
            _list.Columns[2].Width = Math.Max(52, (int)(width * 0.08));
            _list.Columns[3].Width = Math.Max(100, (int)(width * 0.14));
            _list.Columns[4].Width = Math.Max(96, (int)(width * 0.14));
            _list.Columns[5].Width = Math.Max(80, width
                - _list.Columns[0].Width
                - _list.Columns[1].Width
                - _list.Columns[2].Width
                - _list.Columns[3].Width
                - _list.Columns[4].Width
                - 4);
        }

        private void OnMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var selected = SelectedTasks;
            bool any = selected.Count > 0;
            _copyPathMenu.Enabled = any;
            _skipMenu.Enabled = selected.Any(t => t.Status == BatchTaskStatus.Pending);
            _retryMenu.Enabled = selected.Any(t =>
                t.Status is BatchTaskStatus.Failed or BatchTaskStatus.Skipped or BatchTaskStatus.Cancelled);
        }

        private void CopySelectedPaths()
        {
            string text = string.Join(Environment.NewLine, SelectedTasks.Select(t => t.PartPath));
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        private void OnDrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using var fill = new SolidBrush(UiTheme.SurfaceMuted);
            e.Graphics.FillRectangle(fill, e.Bounds);
            using var pen = new Pen(UiTheme.Border);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            TextRenderer.DrawText(
                e.Graphics,
                e.Header?.Text ?? string.Empty,
                UiTheme.CaptionFont,
                e.Bounds,
                UiTheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void OnDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item == null || e.SubItem == null)
                return;

            var task = e.Item.Tag as BatchTaskItem;
            bool selected = e.Item.Selected;
            Color back = selected ? Color.FromArgb(232, 232, 245) : UiTheme.Surface;
            using (var fill = new SolidBrush(back))
                e.Graphics.FillRectangle(fill, e.Bounds);

            Color fore = ResolveForeColor(task, e.ColumnIndex);

            if (e.ColumnIndex == 4 && task != null)
            {
                DrawElapsedWithBar(e, task, fore);
                return;
            }

            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                UiTheme.AppFont,
                Rectangle.Inflate(e.Bounds, -4, 0),
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawElapsedWithBar(DrawListViewSubItemEventArgs e, BatchTaskItem task, Color fore)
        {
            string text = task.ElapsedText;
            var textBounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, Math.Max(48, e.Bounds.Width / 2), e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                UiTheme.AppFont,
                textBounds,
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            double p = Math.Clamp(task.Progress01, 0, 1);
            if (p <= 0 && task.Status != BatchTaskStatus.Running)
                return;

            int barWidth = Math.Max(24, e.Bounds.Width / 2 - 10);
            int barHeight = 6;
            int barX = e.Bounds.Right - barWidth - 6;
            int barY = e.Bounds.Y + (e.Bounds.Height - barHeight) / 2;
            var track = new Rectangle(barX, barY, barWidth, barHeight);
            using (var trackBrush = new SolidBrush(UiTheme.Border))
                e.Graphics.FillRectangle(trackBrush, track);

            int fillW = (int)Math.Round(barWidth * p);
            if (fillW > 0)
            {
                using var fillBrush = new SolidBrush(
                    task.Status == BatchTaskStatus.Failed ? Color.FromArgb(196, 90, 80) : UiTheme.Accent);
                e.Graphics.FillRectangle(fillBrush, new Rectangle(barX, barY, fillW, barHeight));
            }
        }

        private static Color ResolveForeColor(BatchTaskItem? task, int columnIndex)
        {
            if (task == null)
                return UiTheme.TextPrimary;

            if (columnIndex == 1)
            {
                return task.Status switch
                {
                    BatchTaskStatus.Running => UiTheme.Accent,
                    BatchTaskStatus.Failed => Color.FromArgb(168, 60, 50),
                    BatchTaskStatus.Succeeded => Color.FromArgb(70, 120, 80),
                    BatchTaskStatus.Skipped or BatchTaskStatus.Cancelled => UiTheme.TextMuted,
                    _ => UiTheme.TextSecondary
                };
            }

            if (task.Status is BatchTaskStatus.Skipped or BatchTaskStatus.Cancelled)
                return UiTheme.TextMuted;

            return UiTheme.TextPrimary;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tickTimer.Stop();
                _tickTimer.Dispose();
                _menu.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
