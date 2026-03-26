using System;
using System.Diagnostics;
using System.Threading;

namespace TapSynth.Sequencing
{
    public class Clock : IDisposable
    {
        private Thread _thread;
        private bool _running;
        public int BPM { get; set; } = 120;
        public bool IsPlaying { get; private set; }

        public event Action<int> OnTick; 

        private int _tickCount;
        private CancellationTokenSource _cts;

        public void Start()
        {
            if (IsPlaying) return;
            IsPlaying = true;
            _tickCount = 0;
            _cts = new CancellationTokenSource();
            _running = true;
            
            _thread = new Thread(RunClock)
            {
                Priority = ThreadPriority.Highest,
                IsBackground = true
            };
            _thread.Start(_cts.Token);
        }

        public void Stop()
        {
            IsPlaying = false;
            _running = false;
            _cts?.Cancel();
        }

        private void RunClock(object state)
        {
            var token = (CancellationToken)state;
            Stopwatch sw = Stopwatch.StartNew();
            double nextTickTime = 0;

            while (!token.IsCancellationRequested && _running)
            {
                // 16th note calculation
                double msPerTick = (60000.0 / BPM) / 4.0; 

                if (sw.ElapsedMilliseconds >= nextTickTime)
                {
                    OnTick?.Invoke(_tickCount);
                    _tickCount++;
                    nextTickTime += msPerTick;
                }
                else
                {
                    double diff = nextTickTime - sw.ElapsedMilliseconds;
                    if (diff > 2)
                        Thread.Sleep(1);
                    else
                        Thread.SpinWait(100);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
