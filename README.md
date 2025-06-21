# downScale

**downScale** is a user-friendly desktop app for quickly reducing the size of your video files while keeping good visual quality. It is ideal for archiving, sharing, or saving storage space on personal and family videos.

- **Primary platform:** Windows 11 (with audio feedback)
- **Partial support:** macOS/Linux (audio feedback not available)

## What does it do?
- Shrinks large video files by converting them to efficient formats (H.264 or H.265)
- Automatically scales videos to fit within 1920×1920 pixels, preserving the original aspect ratio
- Lets you process multiple videos at once (batch conversion)
- Plays a short audio notification when each conversion finishes (if a .wav file is present, Windows only)
- Keeps a log of your conversions for easy review
- Warns about non-standard pixel formats and logs HDR-related metadata
- Downloads and manages FFmpeg binaries automatically on first run

## How to use
1. **Prepare your videos**: Place the video files you want to compress anywhere on your computer.
2. **Run the app**: Start `downScaleApp.exe` from the app folder.
3. **Select videos**: Drag and drop video files onto the app or run from the command line with file paths as arguments.
4. **Choose output location**: The app suggests a default folder on your Desktop, or you can enter a different full path.
5. **Pick a quality preset**: Choose from several options balancing file size and quality.
6. **(Optional, Windows only) Test audio**: If an audio file is found, you can test-play it before starting.
7. **Start conversion**: The app will process each video, showing progress and playing a sound when done (Windows only).
8. **Find your files**: Converted videos appear in your chosen output folder, ready to use or share.

## Who is it for?
- Anyone who wants to save space on their video collection
- Families archiving home movies
- Content creators preparing videos for upload
- Anyone needing a quick, no-fuss video compressor for Windows, macOS, or Linux (audio feedback is Windows only)

## Requirements
- Windows 11 (primary), or macOS/Linux (partial)
- .NET 9.0 Runtime (free from Microsoft)
- No technical knowledge required—just follow the prompts!

## Acknowledgement

This software uses a wave file distributed by [otosozai.com](https://otosozai.com/). We would like to express our gratitude to otosozai.com for providing high-quality audio materials.