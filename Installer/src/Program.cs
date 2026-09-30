using System;
using System.Net;
using System.Windows.Forms;
using SMTInstaller.UI;

namespace SMTInstaller
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // .NET Framework defaults to old TLS versions that GitHub refuses
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            SelfUpdater.CleanupOldVersion(args);
            Theme.InitScale();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
