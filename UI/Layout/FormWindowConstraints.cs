using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolidWorksTester.UI.Theme;

namespace SolidWorksTester.UI.Layout
{
    /// <summary>
    /// Enforces a minimum client-area size so controls cannot overlap or clip when resizing.
    /// The minimum is measured from the live layout rather than hardcoded, so it stays correct
    /// at any DPI, font scale, or future layout change.
    /// </summary>
    internal static class FormWindowConstraints
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct PointNative
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public PointNative ptReserved;
            public PointNative ptMaxSize;
            public PointNative ptMaxPosition;
            public PointNative ptMinTrackSize;
            public PointNative ptMaxTrackSize;
        }

        private static Size _minClient = Size.Empty;

        /// <summary>
        /// Measures the layout's own minimum and applies it. Call once the form's controls exist
        /// and have been scaled (i.e. from OnLoad), and again whenever DPI changes.
        /// </summary>
        public static void Apply(Form form)
        {
            _minClient = MeasureMinClient(form);
            form.MinimumSize = ToOuterSize(form, _minClient);
        }

        public static void ApplyMinMaxInfo(Form form, ref Message m)
        {
            if (_minClient.IsEmpty)
                return;

            Size chrome = GetNonClientChrome(form);
            var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
            info.ptMinTrackSize.X = _minClient.Width + chrome.Width;
            info.ptMinTrackSize.Y = _minClient.Height + chrome.Height;
            Marshal.StructureToPtr(info, m.LParam, false);
        }

        /// <summary>
        /// Asks the live layout what it actually needs, so the answer is right at any DPI and
        /// survives future layout edits. Width and height are two separate questions: the width
        /// floor comes from the cards' own minimums, and the height must then be measured *at
        /// that width*, because the banner wraps and is tallest when the window is narrowest.
        /// </summary>
        private static Size MeasureMinClient(Form form)
        {
            if (form.Controls.Count == 0)
                return form.ClientSize;

            Control root = form.Controls[0];

            // Proposing a 1px width makes every child report its own minimum rather than its
            // current size. These are DPI-scaled by WinForms, so no manual conversion is needed.
            int contentWidth = root.GetPreferredSize(new Size(1, 0)).Width;
            int minWidth = Math.Max(contentWidth, form.LogicalToDeviceUnits(UiTheme.MinContentWidth));
            int minHeight = root.GetPreferredSize(new Size(minWidth, 0)).Height;

            return new Size(minWidth, minHeight);
        }

        private static Size ToOuterSize(Form form, Size clientSize)
        {
            Size chrome = GetNonClientChrome(form);
            return new Size(clientSize.Width + chrome.Width, clientSize.Height + chrome.Height);
        }

        private static Size GetNonClientChrome(Form form)
        {
            Size delta = form.Size - form.ClientSize;
            if (delta.Width > 0 && delta.Height > 0)
                return delta;

            return new Size(
                SystemInformation.FrameBorderSize.Width * 2,
                SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height * 2);
        }
    }
}
