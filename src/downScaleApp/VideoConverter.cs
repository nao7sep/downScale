using System.Text;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace downScaleApp
{
    public class VideoFileInfo
    {
        public string Path { get; set; } = null!;
        public IMediaInfo? MediaInfo { get; set; }
        public bool IsVideo => MediaInfo?.VideoStreams.Any() ?? false;
        public TimeSpan? Duration => MediaInfo?.Duration;
    }

    public class VideoConverter
    {
        private readonly string _ffmpegDir;

        public VideoConverter(string ffmpegDir)
        {
            _ffmpegDir = ffmpegDir;
            // Download FFmpeg if not present
            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, _ffmpegDir).Wait();
            FFmpeg.SetExecutablesPath(_ffmpegDir);
        }

        public async Task<VideoFileInfo> ProbeAsync(string path)
        {
            if (!Path.IsPathFullyQualified(path))
                throw new ArgumentException($"Path is not fully qualified: {path}", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException($"File does not exist: {path}", path);

            try
            {
                var info = await FFmpeg.GetMediaInfo(path);
                return new VideoFileInfo
                {
                    Path = path,
                    MediaInfo = info
                };
            }
            catch
            {
                return new VideoFileInfo { Path = path, MediaInfo = null };
            }
        }

        public async Task ConvertAsync(VideoFileInfo file, string outputDir, Logger logger, ConsoleService console, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            string inputFile = file.Path;
            string outputFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputFile) + ".mp4");

            // Logger for FFmpeg output.
            using Logger outputLogger = new Logger(Path.ChangeExtension(outputFile, ".log"));

            var videoStream = file.MediaInfo?.VideoStreams.FirstOrDefault();
            if (videoStream == null)
                throw new InvalidOperationException($"No video stream found in file: {inputFile}");

            var sb = new StringBuilder();
            sb.Append($"-i \"{inputFile}\"");
            sb.Append($" \"{outputFile}\"");
            string ffmpegArgs = sb.ToString();
            logger.Log($"Command: ffmpeg {ffmpegArgs}");

            var conversion = FFmpeg.Conversions.New().AddParameter(ffmpegArgs);
            conversion.OnProgress += (s, e) =>
            {
                Console.Write($"\rProgress: {e.Percent:F1}%");
            };
            conversion.OnDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    outputLogger.Log(e.Data);
            };

            await conversion.Start(token);
        }
    }
}
