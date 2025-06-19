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

            // FFmpeg mapping options used to control stream and metadata inclusion.
            // These three -map* options explicitly define what parts of the input are carried into the output.
            // This avoids relying on FFmpeg's default stream selection behavior (which may exclude subtitles, secondary audio, etc).
            //
            // -map 0
            //   → Includes all streams from the input file (video, audio, subtitles, etc).
            //     Without this, FFmpeg may only copy the "best" video/audio and omit others.
            //     This ensures preservation of all content unless explicitly excluded later.
            //
            // -map_metadata 0
            //   → Copies global metadata (e.g., creation_time, encoder, title) from the first input.
            //     Note: This does NOT remove metadata. If metadata should be stripped, use `-map_metadata -1` instead.
            //
            // -map_chapters 0
            //   → Copies chapter markers (if any) from the input. Important for structured videos or educational content.
            //
            // These three options are the most commonly used -map* directives. Other -map* options exist but are rarely needed:
            //   - `-map_metadata:s:a:0`  → Copy metadata from a specific stream (e.g., audio), usually unnecessary.
            //   - `-map_title`           → Legacy option for DVD/Blu-ray; not used for MP4/MKV workflows.
            //   - `-map_disposition`     → Very rare use for setting default stream flags.
            //
            // Recommendation:
            //   Only use the three map options above (`-map 0`, `-map_metadata 0`, `-map_chapters 0`) unless you have a very specific reason.
            //   Using unnecessary map options can lead to unexpected behavior or missing data in output files.

            // -map_channel
            //   → Allows remapping of individual audio channels from input to output.
            //     For example, it can extract only the center channel (dialogue) or remix surround channels into stereo.
            //     Syntax: -map_channel [input_file.stream.channel] [output_stream.channel]
            //     Example: -map_channel 0.1.0 output.0.0  // Maps input file 0, stream 1 (audio), channel 0 (FL) to output channel 0
            //
            //     This option is powerful but low-level, and is typically only needed when doing custom audio remixing:
            //     - Downmixing 5.1 surround into custom stereo configurations
            //     - Extracting commentary or alternate languages from a multichannel stream
            //
            //     In general, this is NOT needed when simply converting surround to stereo using:
            //       -ac 2
            //     Because FFmpeg will perform a standard, perceptually balanced downmix automatically.
            //
            // Recommendation:
            //   Do not use -map_channel unless you need precise control over individual audio channel routing.
            //   For standard workflows (e.g., converting surround to stereo), `-ac 2` is simpler and safer.

            sb.Append($" -map 0");
            sb.Append($" -map_metadata 0");
            sb.Append($" -map_chapters 0");

            // Codec selection: H.264 vs H.265
            //
            // H.264 (libx264) and H.265 (libx265) are both industry-standard video codecs.
            // H.264 is more widely supported across devices, especially older or embedded ones (e.g., TVs, smartphones, browsers).
            // It is still the safest option when sharing with others or ensuring compatibility in unknown environments.
            //
            // H.265 provides significantly better compression efficiency (about 30–50% smaller files at the same visual quality).
            // It is ideal for long-term personal storage (e.g., family videos), where you can ensure proper playback tools.
            // Unlike public distribution, private archives allow you to control decoding methods even years later.
            //
            // Also consider that:
            //   - Playback devices and CPUs are getting faster and more power-efficient (especially with AI acceleration and hardware decoders).
            //   - H.265 is supported in most modern platforms (Windows 10+, macOS, iOS, Android, VLC, etc.).
            //   - AI-enhanced upscalers and decoders (e.g., Nvidia RTX Video, Topaz AI, ffmpeg + VMAF pipelines)
            //     increasingly favor high-efficiency sources like H.265 due to better compression artifacts.
            //
            // Therefore:
            //
            // → Use H.264 if:
            //      - You are sharing the file externally
            //      - You need guaranteed compatibility across many players/devices
            //
            // → Use H.265 if:
            //      - The video is for personal use, archival, or editing
            //      - You prefer smaller file sizes with better quality
            //      - You expect future-proof performance from modern hardware/software
            //
            // Both codec presets are included to let the user choose based on their target audience, device constraints, and long-term goals.
            // This ensures that users aren't locked into a one-size-fits-all choice but can make informed trade-offs.

            sb.Append($" -c:v {preset.GetCodecName()}");
            sb.Append($" -crf {preset.GetCrf()}");

            // Encoding speed vs compression preset
            //
            // The `-preset` option in FFmpeg (used with libx264 and libx265) controls the encoding speed vs compression efficiency trade-off.
            // It does not affect output quality directly (when CRF is fixed), but determines how much CPU time is spent finding better compression.
            //
            // Available presets (from fastest to slowest):
            //   ultrafast → superfast → veryfast → faster → fast → medium (default) → slow → slower → veryslow → placebo
            //
            // - Faster presets = faster encoding but larger files (less efficient compression)
            // - Slower presets = smaller files with same quality, but much longer encoding times
            //
            // Common comparisons:
            //   • fast     → ~2x faster than slow, but ~10–15% larger file
            //   • veryslow → ~3–4x slower than slow, but only ~3–5% smaller file (diminishing returns)
            //
            // Why we use `-preset slow`:
            //   - It offers a good balance between compression and speed
            //   - Compared to the default `medium`, `slow` typically reduces file size by 5–10% without quality loss
            //   - While encoding takes longer, this is often a one-time archival process, where smaller output is worth the extra time
            //   - It is safe to include by default, especially in batch/offline workflows where time is not critical
            //
            // Recommendation:
            //   Use `-preset slow` unless you're in a hurry or constrained by hardware.
            //   Only use `faster` presets for preview, streaming, or real-time scenarios.
            //   Avoid `veryslow` and `placebo` unless you are experimenting—they offer marginal gains at huge time cost.

            sb.Append($" -preset slow");

            // Video rotation handling and aspect-aware scaling
            //
            // FFmpeg stores rotation metadata in up to 3 different places (container tag, stream tag, side data).
            // In practice, reading and interpreting all three consistently is complex, especially via Xabe.FFmpeg:
            //   - The second source (side_data rotation matrix) is not directly accessible via Xabe’s standard API
            //   - Proper interpretation requires parsing JSON output from ffprobe
            //   - The three values may not always match; if inconsistent, additional logic is required to determine priority
            //
            // For this reason, implementing manual rotation in this app would introduce complexity and risk.
            // Rotation metadata varies widely across devices (e.g., smartphones, drones, action cams),
            // and applying manual rotation based on uncertain or inconsistent metadata could lead to incorrect results.
            //
            // Instead, this app relies on FFmpeg’s built-in autorotation behavior, which is enabled by default.
            // This allows FFmpeg to rotate the video according to the embedded metadata before scaling.
            //
            // To perform aspect-ratio-preserving scaling to fit within a 1920×1920 bounding box (FHD limit),
            // we use the following filter:
            //
            //   scale=1920:1920:force_original_aspect_ratio=decrease
            //
            // This ensures:
            //   - The longer edge is resized to 1920 (if greater)
            //   - The shorter edge is scaled proportionally
            //   - The video maintains its correct orientation (portrait/landscape)
            //     as autorotation is handled internally by FFmpeg
            //
            // This design simplifies the implementation while increasing reliability across varied input sources.
            // It also avoids the need for app-specific rotation logic, which is fragile and hard to test broadly.

            // Rotation handling strategy and scaling design rationale
            //
            // We considered two approaches for handling video orientation:
            //
            // (1) Keep rotation metadata intact:
            //     - Use `-noautorotate` to avoid applying FFmpeg’s default rotation
            //     - Read video orientation from metadata (via ffprobe)
            //     - Determine portrait/landscape by comparing aspect ratio (width vs height)
            //     - Resize the frame but preserve its original orientation and metadata
            //     - Let players/devices rotate the video during playback as before
            //
            //   ➤ Pros:
            //     - Preserves exact behavior of original recording
            //     - Avoids re-encoding the rotation visually
            //
            //   ➤ Cons:
            //     - Some devices/apps still ignore rotation metadata (even in 2020s)
            //     - Metadata-based rotation is inconsistently supported across platforms
            //     - Leaves orientation ambiguity for archival or editing use
            //
            // (2) Rotate explicitly by relying on FFmpeg’s built-in autorotate (default behavior):
            //     - FFmpeg applies rotation metadata automatically during decode
            //     - We apply scaling to the visually-rotated frame
            //     - Output has no residual rotation tags — it’s visually correct everywhere
            //
            //   ➤ Chosen approach: (2)
            //     - "If it doesn't hurt to rotate, it's safer to rotate"
            //     - Guarantees correct orientation on all devices, even legacy players
            //     - Prevents future confusion when revisiting the files
            //
            // Scaling method:
            //   scale=w=1920:h=1920:force_original_aspect_ratio=decrease
            //     - This limits both dimensions to a 1920x1920 box
            //     - Automatically adjusts to portrait or landscape shape
            //     - Keeps aspect ratio intact (no distortion)
            //
            // Alternatively, in other filter styles you might see:
            //   scale=1920:-2  → Set width to 1920, compute height automatically
            //   scale=-2:1920  → Set height to 1920, compute width automatically
            //
            //   The `-2` is a special value in FFmpeg that means:
            //     “calculate this dimension automatically while ensuring it's even”
            //     (some codecs require even dimensions)
            //     So `-2` is safe, preferred over `-1` which might result in odd sizes.
            //
            // Summary:
            //   - We allow FFmpeg to autorotate the video based on metadata
            //   - Then scale the result into a 1920x1920 box while preserving aspect ratio
            //   - This ensures consistent visual results regardless of playback environment
            //   - It simplifies the app and maximizes reliability across device types

            sb.Append($" -vf scale=1920:1920:force_original_aspect_ratio=decrease");

            // Audio codec, bitrate, channels, and sample rate handling
            //
            // Codec: AAC
            //   - AAC (Advanced Audio Coding) is the de facto standard for audio in MP4 containers.
            //   - Widely supported by players, devices, browsers, and streaming platforms.
            //   - Default audio codec in most MP4 files (used by Apple, YouTube, etc.)
            //   - Well-balanced between quality, compression, and compatibility
            //   - Chosen for this app because it avoids compatibility surprises and is easy to decode anywhere
            //
            // Bitrate: 128k (standard) / 192k (high quality)
            //   - 128k AAC is sufficient for most use cases: voice, casual video, general streaming
            //   - 192k is used for higher fidelity audio (music, archival footage, editing)
            //   - These are common bitrate choices that offer good perceptual quality at small file sizes
            //
            // Channels: downmix to stereo (ac=2)
            //   - When input is multichannel (e.g., 5.1), we reduce to stereo for broader compatibility and smaller size
            //   - FFmpeg performs perceptually tuned downmixing (center → L+R, etc.)
            //   - If the input is already stereo, channel count is preserved—no unnecessary conversion occurs
            //
            // Sample rate: unchanged
            //   - No resampling is performed (e.g., 48kHz → 44.1kHz), because:
            //     • It provides no perceptual benefit in this context
            //     • Resampling introduces processing and potential minor artifacts
            //     • Most source files already use standard sample rates (44.1kHz or 48kHz)
            //   - Let FFmpeg keep the original sample rate to preserve audio fidelity
            //
            // Summary:
            //   - Audio is encoded using AAC for maximum compatibility
            //   - Bitrate is selected based on quality preset (128k vs 192k)
            //   - Multichannel audio is downmixed to stereo unless already stereo
            //   - Sampling rate is retained to avoid unnecessary processing

            sb.Append($" -c:a aac");
            sb.Append($" -b:a {preset.GetAudioBitrate()}");
            sb.Append($" -ac 2");

            // MP4 faststart optimization
            //
            // Option: -movflags +faststart
            //   - Moves the MP4 "moov" atom (metadata) to the beginning of the file
            //   - This enables progressive playback in streaming scenarios (e.g., online video starts playing before fully downloaded)
            //   - Without it, the moov atom is at the end of the file, and the player must wait for the entire file to load
            //
            // Benefits:
            //   - Allows immediate playback on the web and in some embedded players
            //   - Especially useful for videos uploaded to streaming platforms or sent via HTTP
            //   - No effect on playback quality or codec behavior
            //
            // Is it necessary outside of streaming?
            //   - Even for local or archive use, it has virtually no downside
            //   - Adds only a few kilobytes to file size in the worst case
            //   - Can improve compatibility with mobile players and certain media libraries
            //
            // Recommendation:
            //   - Include +faststart by default, as it improves compatibility and user experience
            //   - Safe for all use cases, including long-term storage and offline playback

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
