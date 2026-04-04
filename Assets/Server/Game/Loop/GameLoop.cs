using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MuLike.Server.Game.Loop
{
    public sealed class GameLoop
    {
        public event Action<float> OnTick;

        private readonly int _ticksPerSecond;
        private CancellationTokenSource _cts;

        public GameLoop(int ticksPerSecond = 20)
        {
            _ticksPerSecond = ticksPerSecond;
        }

        public Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            return RunAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task RunAsync(CancellationToken token)
        {
            double msPerTick = 1000.0 / _ticksPerSecond;
            Stopwatch sw = Stopwatch.StartNew();
            long last = sw.ElapsedMilliseconds;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    long now = sw.ElapsedMilliseconds;
                    float delta = (now - last) / 1000f;
                    last = now;

                    OnTick?.Invoke(delta);

                    long elapsed = sw.ElapsedMilliseconds - now;
                    int delay = Math.Max(0, (int)(msPerTick - elapsed));
                    await Task.Delay(delay, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on stop.
            }
        }
    }
}
