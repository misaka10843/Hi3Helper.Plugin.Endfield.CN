using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Endfield.Management.Api;
using Hi3Helper.Plugin.Endfield.Utils;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Endfield.Management;

internal class EndfieldGameRepairer
{
    private readonly HttpClient _httpClient;
    private readonly string _installPath;
    private readonly EndfieldGameManager _manager;

    public EndfieldGameRepairer(HttpClient httpClient, EndfieldGameManager manager, string installPath)
    {
        _httpClient = httpClient;
        _manager = manager;
        _installPath = installPath;
    }

    public async Task StartRepairAsync(InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
    {
        var baseUrl = _manager.GameResourceBaseUrl;
        if (string.IsNullOrEmpty(baseUrl))
        {
            SharedStatic.InstanceLogger.LogWarning(
                "[EndfieldRepairer] GameResourceBaseUrl is missing. Skipping repair.");
            return;
        }

        InstallProgress progress = default;

        void Report(InstallProgressState state)
        {
            progressDelegate?.Invoke(in progress);
            progressStateDelegate?.Invoke(state);
        }

        Report(InstallProgressState.Preparing);

        var localManifestPath = Path.Combine(_installPath, "game_files");
        byte[]? encryptedManifest = null;

        if (File.Exists(localManifestPath))
        {
            SharedStatic.InstanceLogger.LogInformation(
                "[EndfieldRepairer] Found local game_files. Reading from disk...");
            try
            {
                encryptedManifest = await File.ReadAllBytesAsync(localManifestPath, token);
            }
            catch (Exception ex)
            {
                SharedStatic.InstanceLogger.LogWarning(
                    $"[EndfieldRepairer] Failed to read local game_files: {ex.Message}. Falling back to CDN...");
            }
        }

        if (encryptedManifest == null || encryptedManifest.Length == 0)
        {
            var manifestUrl = $"{baseUrl}/game_files";
            SharedStatic.InstanceLogger.LogInformation(
                $"[EndfieldRepairer] Downloading game_files manifest from CDN: {manifestUrl}");
            try
            {
                encryptedManifest = await _httpClient.GetByteArrayAsync(manifestUrl, token);
                await File.WriteAllBytesAsync(localManifestPath, encryptedManifest, token);
            }
            catch (Exception ex)
            {
                SharedStatic.InstanceLogger.LogError($"[EndfieldRepairer] Failed to download game_files: {ex.Message}");
                return;
            }
        }

        var decryptedManifest = EndfieldCrypto.DecryptBytesToString(encryptedManifest);
        if (string.IsNullOrEmpty(decryptedManifest))
        {
            SharedStatic.InstanceLogger.LogError("[EndfieldRepairer] Failed to decrypt game_files. Wrong AES Key?");
            return;
        }

        var manifestNodes = new List<EndfieldManifestNode>();
        using (var reader = new StringReader(decryptedManifest))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var node = JsonSerializer.Deserialize(line, EndfieldApiContext.Default.EndfieldManifestNode);
                    if (node != null && !string.IsNullOrEmpty(node.Path)) manifestNodes.Add(node);
                }
                catch (Exception ex)
                {
                    SharedStatic.InstanceLogger.LogDebug($"[EndfieldRepairer] JSON line parse skipped: {ex.Message}");
                }
            }
        }

        if (manifestNodes.Count == 0) return;

        SharedStatic.InstanceLogger.LogInformation(
            $"[EndfieldRepairer] Verifying {manifestNodes.Count} local files...");
        progress.TotalCountToDownload = manifestNodes.Count;
        progress.DownloadedCount = 0;
        Report(InstallProgressState.Verify);

        var brokenFiles = new ConcurrentBag<EndfieldManifestNode>();
        long brokenSize = 0;

        await Parallel.ForEachAsync(manifestNodes,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = token }, async (node, innerToken) =>
            {
                var isOk = false;
                var localPath = Path.Combine(_installPath, node.Path!.Replace("/", "\\"));

                if (File.Exists(localPath))
                {
                    var fi = new FileInfo(localPath);
                    if (fi.Length == node.Size) isOk = await CheckMd5Async(localPath, node.Md5!, innerToken);
                }

                if (!isOk)
                {
                    brokenFiles.Add(node);
                    Interlocked.Add(ref brokenSize, node.Size);
                }

                Interlocked.Increment(ref progress.DownloadedCount);
                Report(InstallProgressState.Verify);
            });

        var downloadList = brokenFiles.ToList();
        if (downloadList.Count == 0)
        {
            SharedStatic.InstanceLogger.LogInformation(
                "[EndfieldRepairer] Verification Passed: All files are perfect.");
            return;
        }


        SharedStatic.InstanceLogger.LogInformation(
            $"[EndfieldRepairer] Found {downloadList.Count} missing/corrupted files. Initiating repair...");
        progress.TotalCountToDownload = downloadList.Count;
        progress.TotalBytesToDownload = brokenSize;
        progress.DownloadedBytes = 0;
        progress.DownloadedCount = 0;
        progress.TotalStateToComplete = downloadList.Count;
        progress.StateCount = 0;
        Report(InstallProgressState.Download);

        await Parallel.ForEachAsync(downloadList,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token }, async (node, innerToken) =>
            {
                var downloadUrl = $"{baseUrl}/{node.Path}";
                var targetPath = Path.Combine(_installPath, node.Path!.Replace("/", "\\"));
                var tempPath = targetPath + ".tmp";

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                await DownloadFileAsync(downloadUrl, tempPath, node.Size, innerToken, delta =>
                {
                    Interlocked.Add(ref progress.DownloadedBytes, delta);
                    Report(InstallProgressState.Download);
                });

                var md5Match = await CheckMd5Async(tempPath, node.Md5!, innerToken);
                if (!md5Match)
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        SharedStatic.InstanceLogger.LogDebug(
                            $"[EndfieldRepairer] Temp file delete failed: {ex.Message}");
                    }

                    throw new Exception($"[EndfieldRepairer] MD5 mismatch after downloading {node.Path}");
                }

                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(tempPath, targetPath);

                Interlocked.Increment(ref progress.DownloadedCount);
                Interlocked.Increment(ref progress.StateCount);
                Report(InstallProgressState.Download);
            });

        SharedStatic.InstanceLogger.LogInformation("[EndfieldRepairer] Integrity restored completely!");
    }

    private async Task DownloadFileAsync(string url, string tempPath, long expectedSize, CancellationToken token,
        Action<long> onProgress)
    {
        long existingLength = 0;
        if (File.Exists(tempPath))
        {
            existingLength = new FileInfo(tempPath).Length;
            if (existingLength > expectedSize)
            {
                File.Delete(tempPath);
                existingLength = 0;
            }

            if (existingLength == expectedSize)
            {
                onProgress(existingLength);
                return;
            }
        }

        if (existingLength > 0) onProgress(existingLength);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingLength > 0) request.Headers.Range = new RangeHeaderValue(existingLength, null);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

        if (existingLength > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            existingLength = 0;
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        await using var fs = new FileStream(tempPath, existingLength > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write);

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await fs.WriteAsync(buffer, 0, read, token);
                onProgress(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (new FileInfo(tempPath).Length != expectedSize)
            throw new Exception($"Download incomplete for {url}");
    }

    private async Task<bool> CheckMd5Async(string filePath, string expectedMd5, CancellationToken token)
    {
        if (!File.Exists(filePath)) return false;
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = await md5.ComputeHashAsync(stream, token);
        return BitConverter.ToString(hashBytes).Replace("-", "")
            .Equals(expectedMd5, StringComparison.OrdinalIgnoreCase);
    }
}