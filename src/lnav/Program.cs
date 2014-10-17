namespace lnav
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    static class Program
    {
        public const int GoToMessage = 0xA123;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // if we get a file path arg, connect to an existing instance and pass that path, then exit
            if (args.Length > 0)
            {
                if (SendArgumentToOtherInstances(string.Join(" ", args))) return;

                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        static bool SendArgumentToOtherInstances(string target)
        {
            //var cons = new GUIConsoleWriter();
            //cons.WriteLine("Sending search message to other instances");
            var me = Process.GetCurrentProcess().Id;
            var others = Process.GetProcessesByName("lnav").Where(p => p.Id != me);

            foreach (var proc in others)
            {
                //cons.WriteLine("Sending to PID " + proc.Id);
                foreach (var c in target)
                {
                    SendMessage(proc.MainWindowHandle, GoToMessage, IntPtr.Zero, new IntPtr(c));
                }
                SendMessage(proc.MainWindowHandle, GoToMessage, IntPtr.Zero, IntPtr.Zero);
                //cons.WriteLine("- done.");
            }
            return false;
        }
    }
}
