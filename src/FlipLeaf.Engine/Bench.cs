using System.Diagnostics;

namespace FlipLeaf
{
    internal class Bench : IDisposable
    {
        private static int _level = 0;

        private static readonly string[] _indents;

        private readonly string _operation;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        static Bench()
        {
            _indents = new string[20];
            _indents[0] = "";
            for (int i = 1; i < 20; i++)
            {
                _indents[i] = new string(' ', i);
            }
        }

        public Bench(string operation)
        {
            _operation = operation;
            Console.WriteLine($"{_indents[_level]}>> {operation}");
            Interlocked.Increment(ref _level);
        }

        public void Dispose()
        {
            Interlocked.Decrement(ref _level);
            Console.WriteLine($"{_indents[_level]}<< {_operation} {_stopwatch.ElapsedMilliseconds}ms");
        }

        public static IDisposable Start(string operation) => new Bench(operation);


    }
}