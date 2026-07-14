using System.Windows.Forms;
using SolidWorksTester.UI.Controls;

namespace SolidWorksTester.UI.Views
{
    /// <summary>Strongly typed references to all interactive controls on the main form.</summary>
    internal sealed class MainFormView
    {
        public required TextBox TemplateTextBox { get; init; }
        public required ListBox PartsListBox { get; init; }
        public required Label PartsCountLabel { get; init; }
        public required ThemedButton RunButton { get; init; }
        public required ThemedButton CancelButton { get; init; }
        public required ThemedProgressBar ProgressBar { get; init; }
        public required Label StatusLabel { get; init; }
        public required TextBox LogTextBox { get; init; }
        public required Label FooterAuthorLabel { get; init; }
        public required LinkLabel FooterVersionLink { get; init; }
    }
}
