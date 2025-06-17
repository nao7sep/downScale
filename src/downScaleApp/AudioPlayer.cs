using NAudio.Wave;

namespace downScaleApp
{
    public class AudioPlayer : IDisposable
    {
        private readonly AudioFileReader reader;
        private readonly IWavePlayer output;
        private bool disposed = false;

        public AudioPlayer(string filePath)
        {
            reader = new AudioFileReader(filePath);
            output = new WaveOutEvent();
            output.Init(reader);
        }

        public string FilePath => reader.FileName;

        public void Play()
        {
            reader.Position = 0;
            output.Play();
        }

        public void Stop()
        {
            output.Stop();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    output.Dispose();
                    reader.Dispose();
                }
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AudioPlayer()
        {
            Dispose(false);
        }
    }
}
