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

        public async Task ConvertAsync(VideoFileInfo file, string outputDir, Logger logger, ConsoleService console, VideoConvertPreset preset, CancellationToken token)
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
            sb.Append($" -map 0");
            sb.Append($" -map_metadata 0");
            sb.Append($" -map_chapters 0");
            sb.Append($" -c:v {preset.GetCodecName()}");
            sb.Append($" -crf {preset.GetCrf()}");
            sb.Append($" -preset slow");
            sb.Append($" -vf scale=1920:1920:force_original_aspect_ratio=decrease");
            sb.Append($" -c:a aac");
            sb.Append($" -b:a {preset.GetAudioBitrate()}");
            sb.Append($" -ac 2");
            sb.Append($" -movflags +faststart");
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

    // Video/audio encoding profiles used to control compression, quality, and size.
    // Each profile is a combination of codec (H.264 or H.265), CRF (Constant Rate Factor),
    // and audio bitrate (AAC 128k or 192k). These values were selected based on widely
    // accepted practices in FFmpeg and encoder documentation.
    //
    // H.264 CRF scale (libx264):
    //   - CRF 23: Default for good balance between quality and size
    //   - CRF 18: Near visually lossless quality
    //
    // H.265 CRF scale (libx265):
    //   - CRF 28 ≈ H.264 CRF 23 (equivalent visual quality with 30–50% smaller file)
    //   - CRF 23 ≈ H.264 CRF 18 (near lossless at much smaller size)
    //
    // Audio bitrate:
    //   - 128k AAC: Standard quality for streaming, speech, general video
    //   - 192k AAC: Higher quality, suitable for music, preservation, or editing
    //
    // References:
    //   - FFmpeg H.265 encoding guide: https://trac.ffmpeg.org/wiki/Encode/H.265
    //   - HandBrake CRF comparison: https://handbrake.fr/docs/en/latest/technical/video-quality.html
    //     → Suggests H.265 CRF 27 ≈ H.264 CRF 22
    //   - Reddit, VideoHelp, Doom9 user benchmarks and PSNR/SSIM comparisons support these mappings
    //
    // Final profiles:
    //
    //   H264_Standard:
    //     Codec:     libx264
    //     CRF:       23
    //     Audio:     AAC 128k
    //     Use case:  Standard streaming/distribution with acceptable quality and moderate size
    //
    //   H264_High:
    //     Codec:     libx264
    //     CRF:       18
    //     Audio:     AAC 192k
    //     Use case:  Near-lossless output for editing, archiving, or high-quality streaming
    //
    //   H265_Standard:
    //     Codec:     libx265
    //     CRF:       28 (≈ x264 CRF 23)
    //     Audio:     AAC 128k
    //     Use case:  Modern web distribution where size matters but visual quality is preserved
    //
    //   H265_High:
    //     Codec:     libx265
    //     CRF:       23 (≈ x264 CRF 18)
    //     Audio:     AAC 192k
    //     Use case:  Visually near-lossless high-efficiency encoding for archival or delivery

    // Presets for video conversion modes
    // Lower CRF means higher quality (for both H.264 and H.265)
    public enum VideoConvertPreset
    {
        H264_Standard, // H.264: Standard quality, high compatibility, moderate compression
        H264_High,     // H.264: High quality, high compatibility, less compression
        H265_Standard, // H.265: Standard quality, higher compression efficiency, lower compatibility
        H265_High      // H.265: High quality, higher compression efficiency, lower compatibility
    }

    public static class VideoConvertPresetExtensions
    {
        // Returns the codec name for the given preset
        public static string GetCodecName(this VideoConvertPreset preset)
        {
            return preset switch
            {
                VideoConvertPreset.H264_Standard => "libx264",
                VideoConvertPreset.H264_High => "libx264",
                VideoConvertPreset.H265_Standard => "libx265",
                VideoConvertPreset.H265_High => "libx265",
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown preset value")
            };
        }

        // Returns the CRF value for the given preset
        public static int GetCrf(this VideoConvertPreset preset)
        {
            return preset switch
            {
                VideoConvertPreset.H264_Standard => 23,
                VideoConvertPreset.H264_High => 18,
                VideoConvertPreset.H265_Standard => 28,
                VideoConvertPreset.H265_High => 23,
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown preset value")
            };
        }

        // Returns the audio bitrate string for the given preset
        public static string GetAudioBitrate(this VideoConvertPreset preset)
        {
            return preset switch
            {
                VideoConvertPreset.H264_Standard => "128k",
                VideoConvertPreset.H264_High => "192k",
                VideoConvertPreset.H265_Standard => "128k",
                VideoConvertPreset.H265_High => "192k",
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown preset value")
            };
        }
    }
}
