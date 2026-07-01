using Industrial.Bootstrap;
using Industrial.DI.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Industrial.UI.WinForms
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var container = new Container();
            Bootstrapper.Initialize(container);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var mainWin = container.Resolve<MainForm>();

            Application.Run(mainWin);
        }
    }
}
