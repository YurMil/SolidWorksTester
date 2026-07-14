using Markdig;

namespace SolidWorksTester.UI.Documentation
{
    /// <summary>Converts Markdown to a minimal standalone HTML document for in-app viewing.</summary>
    internal static class MarkdownDocumentRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static string ToHtmlDocument(string markdown, string? documentTitle = null)
        {
            string body = Markdown.ToHtml(markdown, Pipeline);
            string title = string.IsNullOrWhiteSpace(documentTitle)
                ? AppMetadata.ApplicationTitle
                : documentTitle;

            return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="utf-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1" />
                  <title>{System.Net.WebUtility.HtmlEncode(title)}</title>
                  <style>{EngineeringStylesheet}</style>
                </head>
                <body>
                  <main class="doc">{body}</main>
                </body>
                </html>
                """;
        }

        private const string EngineeringStylesheet = """
            :root {
              color-scheme: light;
              --text: #1a1a1a;
              --muted: #5a5a5a;
              --border: #c8c8c8;
              --head: #0f3d5c;
              --bg: #fafafa;
              --table-head: #eef3f7;
            }
            * { box-sizing: border-box; }
            body {
              margin: 0;
              padding: 20px 24px 28px;
              font: 14px/1.55 "Segoe UI", Arial, sans-serif;
              color: var(--text);
              background: var(--bg);
            }
            main.doc { max-width: 820px; margin: 0 auto; }
            h1 {
              font-size: 1.35rem;
              font-weight: 600;
              color: var(--head);
              margin: 0 0 1rem;
              padding-bottom: 0.35rem;
              border-bottom: 2px solid var(--head);
            }
            h2 {
              font-size: 1.05rem;
              font-weight: 600;
              color: var(--head);
              margin: 1.6rem 0 0.65rem;
            }
            h3 {
              font-size: 0.95rem;
              font-weight: 600;
              margin: 1.1rem 0 0.45rem;
            }
            p, li { margin: 0.35rem 0; }
            ul { margin: 0.4rem 0 0.8rem 1.2rem; padding: 0; }
            hr {
              border: none;
              border-top: 1px solid var(--border);
              margin: 1.2rem 0;
            }
            table {
              width: 100%;
              border-collapse: collapse;
              margin: 0.6rem 0 1rem;
              font-size: 13px;
            }
            th, td {
              border: 1px solid var(--border);
              padding: 6px 10px;
              text-align: left;
              vertical-align: top;
            }
            th {
              background: var(--table-head);
              font-weight: 600;
            }
            code {
              font-family: Consolas, "Courier New", monospace;
              font-size: 12px;
              background: #eee;
              padding: 1px 4px;
              border-radius: 3px;
            }
            strong { font-weight: 600; }
            """;
    }
}
