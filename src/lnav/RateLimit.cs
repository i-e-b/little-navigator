namespace lnav
{
    using System;
    using System.Threading;

    public class RateLimit
    {
        readonly Action _act;
        readonly ManualResetEventSlim _gate;

        public RateLimit(TimeSpan rate, Action act)
        {
            _act = act;
            _gate = new ManualResetEventSlim(false);
            new Thread(() => {
                for (; ; )
                {
                    _gate.Wait();
                    Thread.Sleep(rate);
                    _act();
                    _gate.Reset();
                }
                // ReSharper disable once FunctionNeverReturns
            }) { IsBackground = true }.Start();
        }

        public void Trigger()
        {
            _gate.Set();
        }

        public void Immediate()
        {
            _act();
        }
    }
}