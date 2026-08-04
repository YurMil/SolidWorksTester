using System.Windows.Forms;
using SolidWorksTester.UI.Controls;

namespace SolidWorksTester.UI.Views
{
    /// <summary>Strongly typed references to all interactive controls on the main form.</summary>
    internal sealed class MainFormView
    {
        public required TextBox TemplateTextBox { get; init; }
        public required TaskManagerView TaskManager { get; init; }
        public required Label TasksCountLabel { get; init; }
        public required ThemedButton AddFilesButton { get; init; }
        public required ThemedButton AddFolderButton { get; init; }
        public required ThemedButton RemoveButton { get; init; }
        public required ThemedButton ClearButton { get; init; }
        public required ThemedButton SkipButton { get; init; }
        public required ThemedButton RetryButton { get; init; }
        public required ThemedButton RunButton { get; init; }
        public required ThemedButton CancelButton { get; init; }
        public required ThemedProgressBar ProgressBar { get; init; }
        public required Label StatusLabel { get; init; }
        public required Label EventLogHeader { get; init; }
        public required TextBox LogTextBox { get; init; }
        public required Label FooterAuthorLabel { get; init; }
        public required LinkLabel FooterVersionLink { get; init; }
    }
}
