namespace downScaleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Disposed of at the end of the program.
            Logger? logger = null;
            AudioPlayer? audioPlayer = null;

            try
            {
                string ffmpegDir = Path.Combine(AppContext.BaseDirectory, "FFmpeg");

                AudioPlayer? GetAudioPlayer()
                {
                    var audioFile = Directory.GetFiles(AppContext.BaseDirectory, "*.wav", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (audioFile != null && File.Exists(audioFile))
                    {
                        return new AudioPlayer(audioFile);
                    }
                    return null;
                }

                logger = new Logger(Path.Combine(AppContext.BaseDirectory, "downScale.log"));
                var console = new ConsoleService();
                audioPlayer = GetAudioPlayer();
                var videoConverter = new VideoConverter(ffmpegDir);
                var cts = new CancellationTokenSource();

                if (args.Length == 0)
                {
                    Console.WriteLine("Usage: downScaleApp <video file paths>");
                    return;
                }

                var fileInfos = await Task.WhenAll(args.Select(videoConverter.ProbeAsync));

                var invalids = fileInfos.Where(f => !f.IsVideo).OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList();
                if (invalids.Any())
                {
                    foreach (var file in invalids)
                    {
                        var msg = $"Not a video file: {Path.GetFileName(file.Path)}";
                        logger.Log(msg);
                        console.WriteError(msg);
                    }
                    return;
                }

                // Pixel format check (yuv420p compatibility)
                //
                // Most consumer-grade smartphones and cameras record video using the standard pixel format `yuv420p`,
                // which is supported by virtually all playback devices and editing tools.
                //
                // While non-standard formats like `yuv422p`, `yuv444p`, or 10-bit variants (e.g., `yuv420p10le`) do exist,
                // they are rare in personal/family videos and often only used in professional workflows.
                //
                // For now, this app does not automatically convert pixel formats.
                // Instead, we log a warning if the pixel format is not `yuv420p`, allowing users to be aware of potential compatibility issues
                // without forcing unnecessary processing.
                //
                // This keeps the implementation simpler and avoids over-engineering for cases that are unlikely to occur in our usage context.

                // HDR video detection and handling
                //
                // Some modern smartphones and cameras record in HDR (High Dynamic Range), typically using:
                //   - Color primaries: bt2020
                //   - Transfer characteristics: smpte2084 (PQ) or arib-std-b67 (HLG)
                //   - Pixel format: yuv420p10le or similar (10-bit color depth)
                //
                // These indicators can be extracted via ffprobe as:
                //   - VideoStream.ColorPrimaries → "bt2020"
                //   - VideoStream.TransferCharacteristics → "smpte2084" or "arib-std-b67"
                //   - VideoStream.PixelFormat → often a 10-bit format like "yuv420p10le"
                //
                // However, in our use case—personal/family videos captured on phones or cameras—
                // HDR content is uncommon or at least non-critical for viewing.
                // Therefore, we currently do not automatically detect or convert HDR to SDR.
                //
                // Instead, we log key metadata such as pixel format, which may suggest HDR usage.
                // If playback after conversion looks washed out or unnatural, users can:
                //   - Review the console/logs to see if the input was HDR-encoded
                //   - Re-process the file manually with tone mapping if necessary
                //
                // This approach avoids complexity for the majority of files,
                // while still offering transparency and a path forward when issues arise.

                var valids = fileInfos.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList();
                Console.WriteLine("Input video files:");
                foreach (var file in valids)
                {
                    // videoStream should not be null here due to previous checks.
                    var videoStream = file.MediaInfo?.VideoStreams.FirstOrDefault();

                    var width = videoStream!.Width;
                    var height = videoStream!.Height;

                    var pixFmt = videoStream!.PixelFormat;
                    if (string.IsNullOrWhiteSpace(pixFmt))
                    {
                        pixFmt = "unknown";
                    }

                    var part = $"{Path.GetFileName(file.Path)} ({width}x{height}, {pixFmt}, {file.Duration:hh\\:mm\\:ss})";
                    logger.Log($"Input video file: {part}");
                    Console.WriteLine($"    {part}");

                    if (width <= 1920 && height <= 1920)
                    {
                        console.WriteInfo($"        Video is already small enough.");
                    }

                    if (!pixFmt.Equals("yuv420p", StringComparison.OrdinalIgnoreCase))
                    {
                        console.WriteWarning($"        Pixel format is not yuv420p.");
                    }
                }

                string defaultOutDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"downScale-{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");

                Console.WriteLine($"Default output directory: {defaultOutDir}");
                Console.Write($"Enter output directory or just press Enter for default: ");
                string? outputDir = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(outputDir))
                    outputDir = defaultOutDir;
                if (!Path.IsPathFullyQualified(outputDir))
                {
                    console.WriteError("Output path must be fully qualified.");
                    return;
                }
                logger.Log($"Output directory: {outputDir}");
                Directory.CreateDirectory(outputDir);

                // Ask user to select a preset
                Console.WriteLine("Select a video conversion preset:");
                foreach (var (preset, idx) in Enum.GetValues(typeof(VideoConvertPreset)).Cast<VideoConvertPreset>().Select((p, i) => (p, i + 1)))
                {
                    Console.WriteLine($"    {idx}. {preset} (codec: {preset.GetCodecName()}, crf: {preset.GetCrf()}, audio: {preset.GetAudioBitrate()})");
                }
                int presetChoice = 0;
                while (true)
                {
                    Console.Write($"Enter preset number (1-{Enum.GetValues(typeof(VideoConvertPreset)).Length}): ");
                    var input = Console.ReadLine();
                    if (int.TryParse(input, out presetChoice) && presetChoice >= 1 && presetChoice <= Enum.GetValues(typeof(VideoConvertPreset)).Length)
                    {
                        break;
                    }
                }
                var chosenPreset = (VideoConvertPreset)(presetChoice - 1);

                if (audioPlayer != null)
                {
                    Console.WriteLine($"Audio file: {Path.GetFileName(audioPlayer.FilePath)}");
                    Console.Write("Press Space to test-play the audio file or Enter to start conversion: ");
                }
                else
                {
                    Console.WriteLine("No audio file found in the application directory.");
                    Console.Write("Press Enter to start conversion: ");
                }

                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (audioPlayer != null && key.Key == ConsoleKey.Spacebar)
                    {
                        audioPlayer.Play();
                    }
                    if (key.Key == ConsoleKey.Enter)
                        break;
                }

                Console.WriteLine();

                foreach (var file in valids)
                {
                    if (cts.Token.IsCancellationRequested) break;

                    try
                    {
                        Console.WriteLine($"Converting {Path.GetFileName(file.Path)}...");
                        await videoConverter.ConvertAsync(file, outputDir, logger, console, chosenPreset, cts.Token);
                        var msg = $"Converted {Path.GetFileName(file.Path)}";
                        logger.Log(msg);
                        console.WriteInfo($"\r{msg}");
                        if (audioPlayer != null)
                        {
                            audioPlayer.Play();
                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Error converting {Path.GetFileName(file.Path)}: {ex}";
                        logger.Log(msg);
                        console.WriteError($"\r{msg}");
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error: {ex}";
                logger?.Log(msg);
                Console.WriteLine(msg);
            }
            finally
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                logger?.Dispose();
                audioPlayer?.Dispose();
            }
        }
    }
}
