# downScale Application Specifications v0.1

## Document Information

- **Document Title**: downScale Application Specifications v0.1
- **Version**: 0.1
- **Date**: 2025-06-21
- **Author**: nao7sep
- **Company**: Purrfect Code
- **License**: GPL-3.0

## Table of Contents

1. [Project Overview](#project-overview)
2. [System Architecture](#system-architecture)
3. [Core Components](#core-components)
4. [Technical Specifications](#technical-specifications)
5. [User Interface](#user-interface)
6. [Video Processing Pipeline](#video-processing-pipeline)
7. [Audio System](#audio-system)
8. [Logging and Error Handling](#logging-and-error-handling)
9. [Dependencies](#dependencies)
10. [Build and Deployment](#build-and-deployment)
11. [Usage Scenarios](#usage-scenarios)
12. [Performance Considerations](#performance-considerations)
13. [Future Enhancements](#future-enhancements)

## Project Overview

### Purpose
downScale is a desktop utility application designed for efficient video downscaling and compression. The application focuses on reducing video file sizes while maintaining acceptable visual quality, making it ideal for archival storage, sharing, and bandwidth optimization.

### Key Features
- **Batch Video Processing**: Process multiple video files simultaneously
- **Multiple Encoding Presets**: Support for H.264 and H.265 codecs with quality options
- **Intelligent Scaling**: Automatic aspect-ratio-preserving downscaling to 1920×1920 maximum dimensions
- **Audio Feedback**: Optional audio notifications for conversion completion (Windows only)
- **Comprehensive Logging**: Detailed logging of all operations and FFmpeg output
- **User-Friendly Console Interface**: Interactive command-line interface with colored output
- **Automatic FFmpeg Download**: FFmpeg binaries are downloaded and managed automatically on first run
- **Pixel Format and HDR Awareness**: Warns about non-standard pixel formats and logs HDR-related metadata
- **Safe Resource Management**: All disposable resources (audio, logger) are properly disposed

### Target Audience
- Content creators managing large video libraries
- Users needing to reduce video file sizes for storage or sharing
- Archival and backup scenarios requiring efficient compression
- Personal and family video management

## System Architecture

### Platform Requirements
- **Operating System**: Windows 11 (primary target), partial support for macOS/Linux (no audio)
- **Framework**: .NET 9.0
- **Architecture**: x64/x86 compatible
- **Memory**: Minimum 4GB RAM (8GB+ recommended for large videos)
- **Storage**: Variable based on input/output video sizes

### Application Structure
```
downScale/
├── src/downScaleApp/
│   ├── Program.cs              # Main application entry point
│   ├── VideoConverter.cs       # Core video processing logic
│   ├── AudioPlayer.cs          # Audio feedback system (Windows only)
│   ├── ConsoleService.cs       # Console output formatting
│   ├── Logger.cs               # Logging infrastructure
│   ├── downScaleApp.csproj     # Project configuration
│   └── loop200104.wav          # Audio notification file
├── specs/                      # Documentation
├── README.md                   # Project documentation
├── LICENSE                     # GPL-3.0 license
└── downScale.sln              # Visual Studio solution
```

## Core Components

### 1. Program.cs - Main Application Controller
**Responsibilities:**
- Command-line argument parsing and validation
- User interaction and input handling
- Orchestration of video processing workflow
- Error handling and resource cleanup
- Output directory selection and validation (must be fully qualified)
- Preset selection and display of preset details
- Audio test-play and notification integration
- Logging of all major actions and errors

**Key Functions:**
- Video file validation and metadata extraction (using VideoConverter.ProbeAsync)
- User preset selection interface (with details for each preset)
- Output directory configuration (default to Desktop with UTC timestamp)
- Batch processing coordination (conversion loop with progress and error handling)
- Pixel format and HDR metadata warning (logs and console warnings for non-yuv420p and HDR indicators)
- Proper disposal of Logger and AudioPlayer resources

### 2. VideoConverter.cs - Video Processing Engine
**Responsibilities:**
- FFmpeg integration and management (via Xabe.FFmpeg and Xabe.FFmpeg.Downloader)
- Video file analysis and metadata extraction
- Video conversion with configurable presets
- Progress monitoring and reporting (real-time percent display)
- Automatic download and setup of FFmpeg binaries if not present
- Handles all stream mapping, metadata, and chapter preservation
- Applies scaling and autorotation using FFmpeg's built-in logic
- Logs all FFmpeg commands and output to per-file logs

**Key Classes:**
- `VideoFileInfo`: Container for video metadata and file information
- `VideoConverter`: Main conversion engine
- `VideoConvertPreset`: Enumeration of encoding presets
- `VideoConvertPresetExtensions`: Preset configuration methods

**Implementation Notes:**
- Uses `-map 0`, `-map_metadata 0`, `-map_chapters 0` to preserve all streams and metadata
- Uses `-vf scale=1920:1920:force_original_aspect_ratio=decrease` for aspect-ratio-preserving scaling
- Relies on FFmpeg's autorotation for correct orientation
- Audio is always encoded as AAC, downmixed to stereo, and original sample rate is preserved
- MP4 faststart is always enabled for web compatibility
- Codec, CRF, and audio bitrate are determined by preset
- All conversion progress and FFmpeg output are logged

### 3. AudioPlayer.cs - Audio Feedback System
**Responsibilities:**
- Audio file playback for user notifications
- NAudio library integration
- Resource management for audio streams
- Exposes Play/Stop methods and FilePath property
- Implements IDisposable for safe cleanup

**Features:**
- Automatic audio file detection in application directory (first .wav file found)
- Play/stop functionality (test-play before conversion, notification after each conversion)
- Proper resource disposal (output and reader)

### 4. ConsoleService.cs - User Interface
**Responsibilities:**
- Colored console output formatting
- Message categorization (Info, Warning, Error)
- Consistent visual feedback
- Uses ConsoleColor for message types (cyan, yellow, red)

### 5. Logger.cs - Logging Infrastructure
**Responsibilities:**
- File-based logging with UTF-8 encoding (with BOM)
- ISO 8601 timestamp formatting (UTC)
- Thread-safe logging operations
- Automatic log file management (main log and per-conversion logs)
- Implements IDisposable for safe cleanup

## Technical Specifications

### Video Processing Capabilities

#### Supported Input Formats
- **Container Formats**: MP4, AVI, MOV, MKV, WMV, FLV, and other FFmpeg-supported formats
- **Video Codecs**: H.264, H.265, VP8, VP9, and other FFmpeg-supported codecs
- **Audio Codecs**: AAC, MP3, AC3, and other FFmpeg-supported audio formats

#### Output Specifications
- **Container Format**: MP4 (with faststart optimization)
- **Video Codecs**: H.264 (libx264) or H.265 (libx265)
- **Audio Codec**: AAC
- **Maximum Resolution**: 1920×1920 pixels (aspect-ratio preserved)
- **Audio Channels**: Stereo (2.0) with automatic downmixing from surround
- **Pixel Format**: Warns if not yuv420p (no forced conversion)
- **HDR Awareness**: Logs HDR-related metadata (bt2020, smpte2084, arib-std-b67, yuv420p10le)

#### Encoding Presets

| Preset | Codec | CRF | Audio Bitrate | Use Case |
|--------|-------|-----|---------------|----------|
| H264_Standard | libx264 | 23 | 128k AAC | Standard streaming/distribution |
| H264_High | libx264 | 18 | 192k AAC | High-quality archival |
| H265_Standard | libx265 | 28 | 128k AAC | Efficient modern compression |
| H265_High | libx265 | 23 | 192k AAC | High-quality efficient compression |

### Video Processing Pipeline

#### 1. Input Validation
- File existence verification
- Video stream detection
- Metadata extraction using FFprobe (via Xabe.FFmpeg)
- Pixel format compatibility checking (warn if not yuv420p)
- HDR metadata logging (logs if bt2020, smpte2084, arib-std-b67, or 10-bit pixel format detected)

#### 2. Scaling Strategy
- **Method**: `scale=1920:1920:force_original_aspect_ratio=decrease`
- **Rotation Handling**: Automatic FFmpeg autorotation (default behavior)
- **Aspect Ratio**: Preserved during scaling
- **Dimension Limits**: Maximum 1920 pixels on longest edge

#### 3. Audio Processing
- **Codec**: AAC for maximum compatibility
- **Channel Configuration**: Automatic downmix to stereo
- **Sample Rate**: Preserved from source
- **Bitrate**: Configurable (128k/192k based on preset)

#### 4. Container Optimization
- **MP4 Faststart**: Enabled for progressive playback
- **Metadata Preservation**: Global metadata, chapters, and stream metadata copied
- **Stream Mapping**: All input streams preserved unless explicitly excluded

### FFmpeg Integration

#### Command Structure
```bash
ffmpeg -i "input.mp4" -map 0 -map_metadata 0 -map_chapters 0 \
       -c:v libx264 -crf 23 -preset slow \
       -vf scale=1920:1920:force_original_aspect_ratio=decrease \
       -c:a aac -b:a 128k -ac 2 \
       -movflags +faststart "output.mp4"
```

#### Key Parameters Explained
- **`-map 0`**: Include all streams from input
- **`-map_metadata 0`**: Copy global metadata
- **`-map_chapters 0`**: Preserve chapter markers
- **`-preset slow`**: Balance between encoding speed and compression efficiency
- **`-movflags +faststart`**: Enable progressive playback
- **`-vf scale=1920:1920:force_original_aspect_ratio=decrease`**: Aspect-ratio-preserving scaling
- **`-ac 2`**: Downmix to stereo
- **`-c:a aac`**: Always encode audio as AAC
- **`-b:a`**: Audio bitrate set by preset

## User Interface

### Command-Line Interface
The application provides an interactive console-based interface with the following workflow:

#### 1. Input Validation Phase
```
Usage: downScaleApp <video file paths>
Input video files:
    video1.mp4 (1920x1080, yuv420p, 00:05:30)
        Video is already small enough.
    video2.mov (3840x2160, yuv420p, 00:10:15)
        Pixel format is not yuv420p.
```

#### 2. Configuration Phase
```
Default output directory: C:\Users\User\Desktop\downScale-20250619T042300Z
Enter output directory or just press Enter for default:

Select a video conversion preset:
    1. H264_Standard (codec: libx264, crf: 23, audio: 128k)
    2. H264_High (codec: libx264, crf: 18, audio: 192k)
    3. H265_Standard (codec: libx265, crf: 28, audio: 128k)
    4. H265_High (codec: libx265, crf: 23, audio: 192k)
Enter preset number (1-4):
```

#### 3. Audio Test-Play and Notification
- If an audio file is found, user is prompted to press Space to test-play before conversion
- After each conversion, audio notification is played (if available)
- If no audio file is found, user is notified

#### 4. Processing Phase
```
Converting video1.mp4...
Progress: 45.2%
Converted video1.mp4

Converting video2.mov...
Progress: 78.9%
Converted video2.mov
```

### Console Output Formatting
- **Info Messages**: Cyan color for general information
- **Warning Messages**: Yellow color for non-critical issues
- **Error Messages**: Red color for errors and failures
- **Progress Updates**: Real-time percentage display during conversion

## Audio System

### Audio Feedback Features
- **Completion Notifications**: Audio plays when each video conversion completes
- **Test Playback**: Users can test audio before starting conversion
- **Automatic Detection**: Searches for `.wav` files in application directory

### Audio File Requirements
- **Format**: WAV (Windows Audio Format)
- **Location**: Application root directory
- **Naming**: Any `.wav` file (first found is used)
- **Included File**: `loop200104.wav` (courtesy of otosozai.com)

### NAudio Integration
- **Library**: NAudio 2.2.1
- **Components**: `AudioFileReader`, `WaveOutEvent`
- **Resource Management**: Proper disposal pattern implementation

## Logging and Error Handling

### Logging System
- **Main Log**: `downScale.log` in application directory
- **Conversion Logs**: Individual `.log` files for each conversion
- **Format**: ISO 8601 timestamps with UTF-8 encoding
- **Content**: User actions, FFmpeg commands, conversion progress, errors

### Error Handling Strategy
- **Input Validation**: Comprehensive file and format checking
- **Graceful Degradation**: Continue processing remaining files if one fails
- **User Feedback**: Clear error messages with suggested actions
- **Resource Cleanup**: Proper disposal of all resources in finally blocks

### Log File Examples
```
[2025-06-19T04:23:00Z] Input video file: video1.mp4 (1920x1080, yuv420p, 00:05:30)
[2025-06-19T04:23:01Z] Output directory: C:\Users\User\Desktop\downScale-20250619T042300Z
[2025-06-19T04:23:02Z] Command: ffmpeg -i "video1.mp4" -map 0 -map_metadata 0...
[2025-06-19T04:23:45Z] Converted video1.mp4
```

## Dependencies

### NuGet Packages
| Package | Version | Purpose |
|---------|---------|---------|
| NAudio | 2.2.1 | Audio playback functionality |
| Xabe.FFmpeg | 6.0.1 | FFmpeg integration and video processing |
| Xabe.FFmpeg.Downloader | 6.0.1 | Automatic FFmpeg binary management |

### External Dependencies
- **FFmpeg**: Automatically downloaded and managed by Xabe.FFmpeg.Downloader
- **FFmpeg Location**: `{ApplicationDirectory}/FFmpeg/`
- **Version**: Latest official FFmpeg release

### System Dependencies
- **.NET 9.0 Runtime**: Required for application execution
- **Windows Media Foundation**: For audio playback (typically pre-installed)

## Build and Deployment

### Build Configuration
```xml
<PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

### Project Metadata
- **Assembly Title**: downScaleApp
- **Product Name**: downScale
- **Version**: 0.1
- **Copyright**: Copyright © 2025
- **License**: GPL-3.0
- **Repository**: https://github.com/nao7sep/downScale

### Deployment Requirements
1. **Application Executable**: `downScaleApp.exe`
2. **Audio File**: `loop200104.wav` (copied to output directory)
3. **FFmpeg Binaries**: Automatically downloaded on first run
4. **.NET 9.0 Runtime**: Must be installed on target system

## Usage Scenarios

### Scenario 1: Family Video Archive
**Context**: User has large collection of family videos from various devices
**Input**: Mixed resolution videos (1080p, 4K, various formats)
**Process**: Batch conversion using H265_Standard preset
**Output**: Consistent MP4 files optimized for long-term storage

### Scenario 2: Content Creator Workflow
**Context**: YouTuber needs to reduce file sizes for faster uploads
**Input**: High-resolution screen recordings and camera footage
**Process**: H264_Standard preset for maximum compatibility
**Output**: Smaller files suitable for upload platforms

### Scenario 3: Corporate Training Materials
**Context**: Company needs to compress training videos for internal distribution
**Input**: Professional video content with high quality requirements
**Process**: H264_High preset to maintain visual quality
**Output**: High-quality compressed videos for corporate network distribution

## Performance Considerations

### Processing Speed Factors
- **Preset Selection**: `slow` preset balances quality and speed
- **Input Resolution**: Higher resolution inputs require more processing time
- **Codec Choice**: H.265 encoding is slower than H.264 but produces smaller files
- **Hardware**: CPU performance directly impacts encoding speed

### Memory Usage
- **FFmpeg Buffers**: Managed automatically by FFmpeg
- **Application Memory**: Minimal footprint for application logic
- **Large Files**: Processing handled by FFmpeg streaming, not loaded into memory

### Storage Requirements
- **Temporary Space**: FFmpeg may create temporary files during processing
- **Output Size**: Typically 30-70% smaller than input files
- **Log Files**: Minimal storage impact (few KB per conversion)

## Future Enhancements

### Planned Features (Post v0.1)
1. **GPU Acceleration**: NVENC/QuickSync support for faster encoding
2. **Batch Preset Configuration**: Save and load custom preset configurations
3. **Progress Persistence**: Resume interrupted conversions
4. **Advanced Filtering**: Custom video filters and effects
5. **GUI Version**: Windows Forms or WPF interface option
6. **Network Processing**: Distributed processing across multiple machines

### Technical Improvements
1. **HDR Support**: Automatic HDR to SDR tone mapping
2. **Subtitle Preservation**: Enhanced subtitle stream handling
3. **Metadata Enhancement**: Custom metadata injection and editing
4. **Quality Metrics**: PSNR/SSIM quality analysis
5. **Preview Generation**: Thumbnail and preview clip creation

### User Experience Enhancements
1. **Drag-and-Drop Interface**: File selection via drag-and-drop
2. **Real-time Preview**: Before/after quality comparison
3. **Scheduling**: Batch processing with time-based scheduling
4. **Cloud Integration**: Direct upload to cloud storage services
5. **Mobile Companion**: Mobile app for remote monitoring

## Conclusion

downScale v0.1 provides a solid foundation for video compression and downscaling tasks. The application successfully balances ease of use with powerful video processing capabilities, making it suitable for both casual users and content professionals. The modular architecture and comprehensive logging system provide a strong base for future enhancements and feature additions.

The choice of established technologies (FFmpeg, .NET, NAudio) ensures reliability and maintainability, while the GPL-3.0 license promotes open-source collaboration and community contribution.