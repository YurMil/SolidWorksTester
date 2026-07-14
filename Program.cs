using System;
using System.Windows.Forms;

namespace SolidWorksTester
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
