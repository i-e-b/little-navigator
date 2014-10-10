namespace lnav
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Forms;

    static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hwnd, uint Msg, IntPtr wParam, IntPtr lParam);
        public const int GoToMessage = 0xA123;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // if we get a file path arg, connect to an existing instance and pass that path, then exit
            if (args.Length > 0)
            {
                if (SendArgumentToOtherInstances(args[0])) return;

                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        static bool SendArgumentToOtherInstances(string target)
        {
            if (!File.Exists(target))
            {
                return true;
            }

            var me = Process.GetCurrentProcess().Id;
            var others = Process.GetProcessesByName("lnav").Where(p => p.Id != me);

            foreach (var proc in others)
            {
                foreach (char c in target)
                {
                    SendMessage(proc.MainWindowHandle, GoToMessage, IntPtr.Zero, new IntPtr(c));
                }
                SendMessage(proc.MainWindowHandle, GoToMessage, IntPtr.Zero, IntPtr.Zero);
            }
            return false;
        }
    }
}
