namespace lnav
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Attach to a parent console process, allowing reading STDIN and writing to STDOUT
    /// </summary>
    public class GUIConsoleWriter
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private const int AttachParentProcess = -1;

        readonly StreamWriter _stdOutWriter;
        readonly StreamReader _stdInReader;

        // this must be called early in the program
        public GUIConsoleWriter(): this(AttachParentProcess) {} 
        public GUIConsoleWriter(int processId)
        {
            var stdout = Console.OpenStandardOutput();
            var stdin = Console.OpenStandardInput();

            _stdInReader = new StreamReader(stdin);
            _stdOutWriter = new StreamWriter(stdout) { AutoFlush = true };

            AttachConsole(AttachParentProcess);
        }

        public void WriteLine(string line)
        {
            _stdOutWriter.WriteLine(line);
            Console.WriteLine(line);
        }

        public bool WaitingData()
        {
            return _stdInReader.Peek() >= 0;
        }

        public string ReadLine()
        {
            return _stdInReader.ReadLine();
        }
    }
}