using System.Text;

namespace downScaleApp
{
    public class Logger : IDisposable
    {
        private readonly StreamWriter writer;
        private bool disposed = false;
        public Logger(string logPath)
        {
            writer = new StreamWriter(logPath, append: true, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)) { AutoFlush = true };
        }
        public void Log(string message)
        {
            writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    writer.Dispose();
                }
                disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~Logger()
        {
            Dispose(false);
        }
    }
}
