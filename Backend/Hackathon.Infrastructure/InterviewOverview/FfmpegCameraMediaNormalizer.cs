using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using Hackathon.Application.InterviewOverview;

namespace Hackathon.Infrastructure.InterviewOverview;

public sealed class FfmpegCameraMediaNormalizer(IHttpClientFactory httpClientFactory)
{
    private const string LinuxX64Url =
        "https://github.com/eugeneware/ffmpeg-static/releases/download/b6.1.1/ffmpeg-linux-x64.gz";

    private const string LinuxX64GzSha256 =
        "bfe8a8fc511530457b528c48d77b5737527b504a3797a9bc4866aeca69c2dffa";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<CameraVideoLink?> ToMp4Async(CameraVideoLink clip, CancellationToken cancellationToken)
    {
        if (clip.Content is not { Length: > 0 })
        {
            return null;
        }

        if (LooksLikeMp4(clip))
        {
            return clip with
            {
                ContentType = "video/mp4",
                FileName = "camera.mp4",
            };
        }

        var ffmpeg = await EnsureFfmpegAsync(cancellationToken);
        if (ffmpeg is null)
        {
            return null;
        }

        var work = Directory.CreateTempSubdirectory("cam-mp4-");
        try
        {
            var input = Path.Combine(work.FullName, "input.webm");
            var output = Path.Combine(work.FullName, "camera.mp4");
            await File.WriteAllBytesAsync(input, clip.Content, cancellationToken);

            if (!await RunAsync(
                    ffmpeg,
                    $"-y -i \"{input}\" -c:v libx264 -preset veryfast -crf 28 -pix_fmt yuv420p -c:a aac -ac 1 -b:a 64k -movflags +faststart \"{output}\"",
                    TimeSpan.FromSeconds(150),
                    cancellationToken)
                && !await RunAsync(
                    ffmpeg,
                    $"-y -i \"{input}\" -c:v libx264 -preset veryfast -crf 28 -pix_fmt yuv420p -an -movflags +faststart \"{output}\"",
                    TimeSpan.FromSeconds(150),
                    cancellationToken))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(output, cancellationToken);
            return bytes.Length == 0
                ? null
                : clip with
                {
                    Content = bytes,
                    ContentType = "video/mp4",
                    FileName = "camera.mp4",
                    Size = bytes.Length,
                };
        }
        finally
        {
            TryDelete(work);
        }
    }

    public async Task<IReadOnlyList<CameraStill>> ExtractStillsAsync(
        CameraVideoLink clip,
        CancellationToken cancellationToken)
    {
        if (clip.Content is not { Length: > 0 })
        {
            return [];
        }

        var ffmpeg = await EnsureFfmpegAsync(cancellationToken);
        if (ffmpeg is null)
        {
            return [];
        }

        var work = Directory.CreateTempSubdirectory("cam-still-");
        try
        {
            var input = Path.Combine(work.FullName, "input.bin");
            await File.WriteAllBytesAsync(input, clip.Content, cancellationToken);
            var pattern = Path.Combine(work.FullName, "frame_%02d.jpg");
            if (!await RunAsync(
                    ffmpeg,
                    $"-y -i \"{input}\" -vf \"fps=1/10,scale='min(720,iw)':-2\" -q:v 5 -frames:v 8 \"{pattern}\"",
                    TimeSpan.FromSeconds(90),
                    cancellationToken))
            {
                return [];
            }

            return Directory.GetFiles(work.FullName, "frame_*.jpg")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new CameraStill(File.ReadAllBytes(path), "image/jpeg"))
                .Where(still => still.Content.Length > 800)
                .ToList();
        }
        finally
        {
            TryDelete(work);
        }
    }

    private async Task<string?> EnsureFfmpegAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var candidate in LocalCandidates())
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var onPath = FindOnPath();
            if (onPath is not null)
            {
                return onPath;
            }

            if (!OperatingSystem.IsLinux())
            {
                return null;
            }

            var dest = Path.Combine(Path.GetTempPath(), "ffmpeg");
            if (File.Exists(dest))
            {
                return dest;
            }

            var client = httpClientFactory.CreateClient("FfmpegDownload");
            using var response = await client.GetAsync(LinuxX64Url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var gz = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(gz));
            if (!hash.Equals(LinuxX64GzSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Downloaded ffmpeg checksum did not match.");
            }

            var staging = dest + ".tmp";
            await using (var input = new MemoryStream(gz, writable: false))
            await using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            await using (var output = File.Create(staging))
            {
                await gzip.CopyToAsync(output, cancellationToken);
            }

            File.SetUnixFileMode(
                staging,
                UnixFileMode.UserRead | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            File.Move(staging, dest, overwrite: true);
            return dest;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static IEnumerable<string> LocalCandidates()
    {
        var env = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(env))
        {
            yield return env;
        }

        yield return Path.Combine(AppContext.BaseDirectory, "ffmpeg");
        yield return "/opt/bin/ffmpeg";
        yield return Path.Combine(Path.GetTempPath(), "ffmpeg");
    }

    private static string? FindOnPath()
    {
        var name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir.Trim(), name))
            .FirstOrDefault(File.Exists);
    }

    private static bool LooksLikeMp4(CameraVideoLink clip)
    {
        if (clip.ContentType.Contains("mp4", StringComparison.OrdinalIgnoreCase) ||
            clip.FileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var bytes = clip.Content;
        return bytes is { Length: >= 12 } &&
               bytes[4] == (byte)'f' &&
               bytes[5] == (byte)'t' &&
               bytes[6] == (byte)'y' &&
               bytes[7] == (byte)'p';
    }

    private static async Task<bool> RunAsync(
        string ffmpeg,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return false;
        }

        await Task.WhenAll(stderr, stdout);
        return process.ExitCode == 0;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static void TryDelete(DirectoryInfo work)
    {
        try
        {
            work.Delete(recursive: true);
        }
        catch
        {
            // /tmp is ephemeral
        }
    }
}
