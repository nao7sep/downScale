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
                        logger.Log($"ERROR: Not a video file: {Path.GetFileName(file.Path)}");
                        console.WriteError($"ERROR: Not a video file: {Path.GetFileName(file.Path)}");
                    }
                    return;
                }

                var valids = fileInfos.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList();
                Console.WriteLine("Input video files:");
                foreach (var file in valids)
                {
                    logger.Log($"Input video file: {Path.GetFileName(file.Path)} ({file.Duration:hh\\:mm\\:ss})");
                    Console.WriteLine($"    {Path.GetFileName(file.Path)} ({file.Duration:hh\\:mm\\:ss})");
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
                    console.WriteError("ERROR: Output path must be fully qualified.");
                    return;
                }
                logger.Log($"Output directory: {outputDir}");
                Directory.CreateDirectory(outputDir);

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
                        await videoConverter.ConvertAsync(file, outputDir, logger, cts.Token);
                        logger.Log($"Converted {Path.GetFileName(file.Path)}");
                        console.WriteInfo($"\rConverted {Path.GetFileName(file.Path)}");
                        if (audioPlayer != null)
                        {
                            audioPlayer.Play();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"ERROR converting {Path.GetFileName(file.Path)}: {ex}");
                        console.WriteError($"\rERROR converting {Path.GetFileName(file.Path)}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Log($"ERROR: {ex}");
                Console.WriteLine($"ERROR: {ex}");
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
