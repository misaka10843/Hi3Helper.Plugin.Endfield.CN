using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Endfield.Management.Api;
using Hi3Helper.Plugin.Endfield.Utils;
using Microsoft.Extensions.Logging;
using SevenZipExtractor;
using SevenZipExtractor.Event;
using SharpHDiffPatch.Core;

namespace Hi3Helper.Plugin.Endfield.Management;

internal partial class EndfieldGameInstaller
{
    private sealed class Install
    {
        private readonly EndfieldGameInstaller _owner;

        public Install(EndfieldGameInstaller owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public async Task RunAsync(GameInstallerKind kind, InstallProgressDelegate? progressDelegate,
            InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
        {
            await _owner.InitAsync(token).ConfigureAwait(false);

            if (_owner.GameManager is not EndfieldGameManager manager || manager.GamePacks == null ||
                manager.GamePacks.Count == 0)
                throw new InvalidOperationException("No download packs found in API response.");

            _owner.GameManager.GetGamePath(out var installPath);
            if (string.IsNullOrEmpty(installPath)) throw new InvalidOperationException("Install path is missing.");

            var downloadDir = Path.Combine(installPath, "Diffs");
            Directory.CreateDirectory(downloadDir);

            InstallProgress progress = default;

            void Report(InstallProgressState state)
            {
                progressDelegate?.Invoke(in progress);
                progressStateDelegate?.Invoke(state);
            }

            // 并发文件预校验
            Report(InstallProgressState.Preparing);
            SharedStatic.InstanceLogger.LogInformation("[EndfieldInstaller] Verifying existing packages...");

            var packsToDownload = new ConcurrentBag<EndfieldPack>();
            long totalBytesToDownload = 0;
            long alreadyDownloadedBytes = 0;

            foreach (var pack in manager.GamePacks)
                if (long.TryParse(pack.PackageSize, out var s))
                    totalBytesToDownload += s;

            await Parallel.ForEachAsync(manager.GamePacks,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token },
                async (pack, innerToken) =>
                {
                    if (string.IsNullOrEmpty(pack.Url)) return;
                    long.TryParse(pack.PackageSize, out var size);

                    var fileName = Path.GetFileName(new Uri(pack.Url).LocalPath);
                    var filePath = Path.Combine(downloadDir, fileName);
                    var tempPath = filePath + ".tmp";

                    var needsDownload = true;

                    if (File.Exists(filePath))
                    {
                        var fi = new FileInfo(filePath);
                        if (fi.Length == size)
                        {
                            var isMatch = string.IsNullOrEmpty(pack.Md5) ||
                                          await CheckMd5Async(filePath, pack.Md5, innerToken);
                            if (isMatch)
                            {
                                Interlocked.Add(ref alreadyDownloadedBytes, size);
                                needsDownload = false;
                            }
                            else
                            {
                                File.Delete(filePath);
                            }
                        }
                        else
                        {
                            File.Delete(filePath);
                        }
                    }

                    if (needsDownload)
                    {
                        if (File.Exists(tempPath))
                            if (new FileInfo(tempPath).Length > size)
                                File.Delete(tempPath);

                        packsToDownload.Add(pack);
                    }
                });

            var downloadTasks = packsToDownload.ToList();
            progress.TotalCountToDownload = downloadTasks.Count;
            progress.TotalBytesToDownload = totalBytesToDownload;
            progress.DownloadedBytes = alreadyDownloadedBytes;
            progress.TotalStateToComplete = downloadTasks.Count + 1;

            //断点续传下载
            if (downloadTasks.Count > 0)
            {
                progressStateDelegate?.Invoke(InstallProgressState.Download);
                SharedStatic.InstanceLogger.LogInformation(
                    $"[EndfieldInstaller] Downloading {downloadTasks.Count} files...");

                await Parallel.ForEachAsync(downloadTasks, new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = token
                }, async (pack, innerToken) =>
                {
                    var fileName = Path.GetFileName(new Uri(pack.Url).LocalPath);
                    var filePath = Path.Combine(downloadDir, fileName);
                    var tempPath = filePath + ".tmp";
                    var expectedSize = long.Parse(pack.PackageSize ?? "0");

                    await DownloadFileAsync(pack.Url!, tempPath, expectedSize, innerToken, delta =>
                    {
                        Interlocked.Add(ref progress.DownloadedBytes, delta);
                        Report(InstallProgressState.Download);
                    }).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(pack.Md5))
                    {
                        var isMatch = await CheckMd5Async(tempPath, pack.Md5, innerToken);
                        if (!isMatch)
                        {
                            try
                            {
                                File.Delete(tempPath);
                            }
                            catch
                            {
                            }

                            throw new Exception($"MD5 Mismatch after downloading {fileName}");
                        }
                    }

                    if (File.Exists(filePath)) File.Delete(filePath);
                    File.Move(tempPath, filePath);

                    Interlocked.Increment(ref progress.DownloadedCount);
                    Interlocked.Increment(ref progress.StateCount);
                });
            }

            // 分发IO执行流
            progressStateDelegate?.Invoke(InstallProgressState.Install);

            if (manager.IsDeltaUpdate)
            {
                SharedStatic.InstanceLogger.LogInformation("[EndfieldInstaller] Delta update mechanism confirmed.");
                var tempExtractDir = Path.Combine(installPath, "_Endfield_DeltaTemp");

                var skipExtractForDebug = false;
#if DEBUG
                skipExtractForDebug = true;
# endif


                if (!skipExtractForDebug)
                {
                    if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
                    Directory.CreateDirectory(tempExtractDir);

                    await ExtractPackagesAsync(downloadDir, tempExtractDir, token, (extractedBytes, totalBytes) =>
                    {
                        progress.DownloadedBytes = extractedBytes;
                        progress.TotalBytesToDownload = totalBytes;
                        Report(InstallProgressState.Install);
                    });
                }
                else
                {
                    SharedStatic.InstanceLogger.LogWarning(
                        "[EndfieldInstaller] Debug mode enabled: Skipping extraction. Using existing _Endfield_DeltaTemp directory for patching.");
                }

                await ApplyDeltaPatchAsync(tempExtractDir, installPath, manager.PatchManifestUrl, token,
                    (patchedBytes, totalBytes) =>
                    {
                        progress.DownloadedBytes = patchedBytes;
                        progress.TotalBytesToDownload = totalBytes;
                        Report(InstallProgressState.Install);
                    });

                // 调试模式下保留沙盒目录，非调试模式下执行清理
                if (!skipExtractForDebug)
                    try
                    {
                        Directory.Delete(tempExtractDir, true);
                    }
                    catch
                    {
                    }
                else
                    SharedStatic.InstanceLogger.LogWarning("[EndfieldInstaller] [DEBUG] 调试模式已开启：跳过沙盒清理逻辑。");
            }
            else
            {
                SharedStatic.InstanceLogger.LogInformation("[EndfieldInstaller] Full update mechanism confirmed.");
                await ExtractPackagesAsync(downloadDir, installPath, token, (extractedBytes, totalBytes) =>
                {
                    progress.DownloadedBytes = extractedBytes;
                    progress.TotalBytesToDownload = totalBytes;
                    Report(InstallProgressState.Install);
                });
            }

            try
            {
                Directory.Delete(downloadDir, true);
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(manager.TargetVersion))
            {
                var configPath = Path.Combine(installPath, "config.ini");
                if (File.Exists(configPath))
                    try
                    {
                        var lines = File.ReadAllLines(configPath);
                        for (var i = 0; i < lines.Length; i++)
                            if (lines[i].StartsWith("version=", StringComparison.OrdinalIgnoreCase))
                                lines[i] = $"version={manager.TargetVersion}";

                        File.WriteAllLines(configPath, lines);
                        SharedStatic.InstanceLogger.LogInformation(
                            $"[EndfieldInstaller] Successfully updated config.ini version to {manager.TargetVersion}");
                    }
                    catch (Exception ex)
                    {
                        SharedStatic.InstanceLogger.LogError(
                            $"[EndfieldInstaller] Failed to write config.ini: {ex.Message}");
                    }
            }

            progressStateDelegate?.Invoke(InstallProgressState.Completed);
        }

        private async Task ApplyDeltaPatchAsync(string tempExtractDir, string targetGameRoot, string? patchJsonUrl,
            CancellationToken token, Action<long, long>? progressCallback = null)
        {
            if (string.IsNullOrEmpty(patchJsonUrl))
                throw new InvalidDataException("[EndfieldInstaller] Patch configuration URL is missing.");

            SharedStatic.InstanceLogger.LogInformation(
                $"[EndfieldInstaller] Fetching delta patch manifest: {patchJsonUrl}");

            using var httpClient = new HttpClient();
            using var response =
                await httpClient.GetAsync(patchJsonUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var manifest =
                await response.Content.ReadFromJsonAsync(EndfieldApiContext.Default.EndfieldPatchManifest, token)
                ?? throw new InvalidDataException("[EndfieldInstaller] Failed to deserialize EndfieldPatchManifest.");

            var vfsBasePath = Path.Combine(targetGameRoot,
                (manifest.VfsBasePath ?? "Endfield_Data/StreamingAssets/VFS").Replace("/", "\\"));

            var totalFiles = manifest.Files?.Count ?? 0;
            var currentProcessed = 0;
            var totalPatchSize = manifest.Files?.Sum(f => f.Size) ?? 0;
            long currentPatchedSize = 0;

            SharedStatic.InstanceLogger.LogInformation("[EndfieldInstaller] Building temporary extraction file map...");
            var allExtractedFiles = Directory.GetFiles(tempExtractDir, "*", SearchOption.AllDirectories);
            var extractFileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in allExtractedFiles) extractFileMap[Path.GetFileName(file)] = file;

            foreach (var fileNode in manifest.Files!)
            {
                token.ThrowIfCancellationRequested();
                currentProcessed++;

                var targetFilePath = Path.Combine(vfsBasePath, fileNode.Name!.Replace("/", "\\"));
                var targetDir = Path.GetDirectoryName(targetFilePath)!;
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                if (!string.IsNullOrEmpty(fileNode.LocalPath))
                {
                    var sourceExtractedFile = Path.Combine(tempExtractDir, fileNode.LocalPath.Replace("/", "\\"));

                    if (!File.Exists(sourceExtractedFile) &&
                        extractFileMap.TryGetValue(Path.GetFileName(fileNode.LocalPath), out var foundPath))
                        sourceExtractedFile = foundPath;

                    if (File.Exists(sourceExtractedFile))
                    {
                        File.Copy(sourceExtractedFile, targetFilePath, true);
                        Interlocked.Add(ref currentPatchedSize, fileNode.Size);
                        progressCallback?.Invoke(currentPatchedSize, totalPatchSize);
                    }
                }
                else if (fileNode.Patches != null && fileNode.Patches.Count > 0)
                {
                    var patchInfo = fileNode.Patches[0];
                    var baseFilePath = Path.Combine(vfsBasePath, patchInfo.BaseFile!.Replace("/", "\\"));
                    var diffFilePath = Path.Combine(tempExtractDir, patchInfo.PatchPath!.Replace("/", "\\"));
                    if (!File.Exists(diffFilePath) && extractFileMap.TryGetValue(Path.GetFileName(patchInfo.PatchPath!),
                            out var foundDiffPath))
                        diffFilePath = foundDiffPath;

                    if (File.Exists(baseFilePath) && File.Exists(diffFilePath))
                    {
                        var tempOutPath = targetFilePath + ".tmp";
                        try
                        {
                            var hdiffPatcher = new HDiffPatch();
                            hdiffPatcher.Initialize(diffFilePath);

                            Action<long> onPatchProgress = deltaBytes =>
                            {
                                Interlocked.Add(ref currentPatchedSize, deltaBytes);
                                progressCallback?.Invoke(currentPatchedSize, totalPatchSize);
                            };

                            hdiffPatcher.Patch(baseFilePath, tempOutPath, true, onPatchProgress, token);
                            File.Move(tempOutPath, targetFilePath, true);
                        }
                        catch (Exception ex)
                        {
                            SharedStatic.InstanceLogger.LogError(
                                $"[EndfieldInstaller] Delta patch failed for {fileNode.Name}. Error: {ex.Message}");
                            if (File.Exists(tempOutPath)) File.Delete(tempOutPath);
                            throw;
                        }
                    }
                    else
                    {
                        SharedStatic.InstanceLogger.LogWarning(
                            $"[EndfieldInstaller] VFS node lookup failed for delta target. Base exists: {File.Exists(baseFilePath)}, Diff exists: {File.Exists(diffFilePath)}. Skipped {fileNode.Name}");
                    }
                }
            }

            SharedStatic.InstanceLogger.LogInformation(
                "[EndfieldInstaller] VFS delta patch pipeline executed successfully.");
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
            if (existingLength > 0)
                request.Headers.Range = new RangeHeaderValue(existingLength, null);

            using var response =
                await _owner._downloadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

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

            var finalInfo = new FileInfo(tempPath);
            if (finalInfo.Length != expectedSize)
                throw new Exception($"Download incomplete. Expected {expectedSize}, got {finalInfo.Length}");
        }

        private async Task<bool> CheckMd5Async(string filePath, string expectedMd5, CancellationToken token)
        {
            if (!File.Exists(filePath)) return false;

            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);

            var hashBytes = await md5.ComputeHashAsync(stream, token);
            var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            return hashStr.Equals(expectedMd5, StringComparison.OrdinalIgnoreCase);
        }

        private async Task ExtractPackagesAsync(string sourceDir, string destDir, CancellationToken token,
            Action<long, long>? progressCallback)
        {
            SharedStatic.InstanceLogger.LogInformation(
                $"[EndfieldInstaller] Preparing decompression (Virtual merge stream): {sourceDir} -> {destDir}");

            var partFiles = Directory.GetFiles(sourceDir)
                .Where(f => f.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase) ||
                            (Path.GetExtension(f).Length == 4 && char.IsDigit(Path.GetExtension(f)[1]) &&
                             f.Contains(".zip.")))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (partFiles.Count == 0)
            {
                var singleZip = Directory.GetFiles(sourceDir, "*.zip").FirstOrDefault();
                if (singleZip != null) partFiles.Add(singleZip);
            }

            if (partFiles.Count == 0)
                throw new FileNotFoundException("No archive found in Downloads folder");

            await Task.Run(async () =>
            {
                try
                {
                    using var multiStream = new MultiVolumeStream(partFiles);
                    using var archiveFile = new ArchiveFile(multiStream);

                    var totalSize = archiveFile.Entries.Sum(x => (long)x.Size);
                    long currentRead = 0;

                    void ZipProgressAdapter(object? sender, ExtractProgressProp e)
                    {
                        if (token.IsCancellationRequested) return;
                        Interlocked.Add(ref currentRead, (long)e.Read);
                        progressCallback?.Invoke(Math.Min(currentRead, totalSize), totalSize);
                    }

                    archiveFile.ExtractProgress += ZipProgressAdapter;
                    try
                    {
                        await archiveFile.ExtractAsync(entry =>
                        {
                            var safeName = (entry.FileName ?? string.Empty).TrimStart('/', '\\');
                            return Path.Combine(destDir, safeName);
                        }, true, 1 << 20, token);
                    }
                    finally
                    {
                        archiveFile.ExtractProgress -= ZipProgressAdapter;
                    }

                    SharedStatic.InstanceLogger.LogInformation("[EndfieldInstaller] Decompression complete!");
                }
                catch (Exception ex)
                {
                    SharedStatic.InstanceLogger.LogError($"[EndfieldInstaller] Decompression failed: {ex}");
                    throw;
                }
            }, token);
        }
    }
}