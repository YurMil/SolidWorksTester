using System;
using System.Windows.Forms;
using SolidWorksTester.Services.SolidWorks;

namespace SolidWorksTester
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            SolidWorksBootstrap.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
