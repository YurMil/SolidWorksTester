using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace SolidWorksTester.UI.Documentation
{
    /// <summary>Minimal read-only Markdown viewer (Markdig → HTML → WebView2).</summary>
    internal sealed class MarkdownViewerControl : UserControl
    {
        private readonly WebView2 _webView;
        private bool _initialized;

        public MarkdownViewerControl()
        {
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(250, 250, 250)
            };
            Controls.Add(_webView);
        }

        public async Task DisplayMarkdownAsync(string markdown, string? documentTitle = null)
        {
            await EnsureInitializedAsync();
            string html = MarkdownDocumentRenderer.ToHtmlDocument(markdown, documentTitle);
            _webView.NavigateToString(html);
        }

        private async Task EnsureInitializedAsync()
        {
            if (_initialized)
                return;

            await _webView.EnsureCoreWebView2Async();
            _initialized = true;
        }
    }
}
