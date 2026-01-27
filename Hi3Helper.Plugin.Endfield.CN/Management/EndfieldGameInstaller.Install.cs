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
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Endfield.CN.Management.Api;
using Hi3Helper.Plugin.Endfield.CN.Utils;
using Microsoft.Extensions.Logging;
using SharpSevenZip;

namespace Hi3Helper.Plugin.Endfield.CN.Management;

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
            {
                throw new InvalidOperationException("No download packs found in API response.");
            }

            _owner.GameManager.GetGamePath(out var installPath);
            if (string.IsNullOrEmpty(installPath)) throw new InvalidOperationException("Install path is missing.");

            var downloadDir = Path.Combine(installPath, "Downloads");
            Directory.CreateDirectory(downloadDir);

            var downloadTasks = new List<EndfieldPack>();
            long totalBytesToDownload = 0;
            long alreadyDownloadedBytes = 0;

            foreach (var pack in manager.GamePacks)
            {
                if (long.TryParse(pack.PackageSize, out var size))
                    totalBytesToDownload += size;

                var fileName = Path.GetFileName(new Uri(pack.Url).LocalPath);
                var filePath = Path.Combine(downloadDir, fileName);

                // 检查已存在的文件
                if (File.Exists(filePath))
                {
                    var fi = new FileInfo(filePath);
                    if (fi.Length == size)
                    {
                        alreadyDownloadedBytes += size;
                        continue;
                    }
                }

                downloadTasks.Add(pack);
            }

            InstallProgress progress = default;
            progress.TotalCountToDownload = downloadTasks.Count;
            progress.TotalBytesToDownload = totalBytesToDownload;
            progress.DownloadedBytes = alreadyDownloadedBytes;
            progress.TotalStateToComplete = downloadTasks.Count + 1;

            void Report(InstallProgressState state)
            {
                progressDelegate?.Invoke(in progress);
                progressStateDelegate?.Invoke(state);
            }

            Report(InstallProgressState.Preparing);

            if (downloadTasks.Count > 0)
            {
                progressStateDelegate?.Invoke(InstallProgressState.Download);

                await Parallel.ForEachAsync(downloadTasks, new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = token
                }, async (pack, innerToken) =>
                {
                    var fileName = Path.GetFileName(new Uri(pack.Url).LocalPath);
                    var filePath = Path.Combine(downloadDir, fileName);
                    var tempPath = filePath + ".tmp";
                    long expectedSize = long.Parse(pack.PackageSize ?? "0");

                    await DownloadFileAsync(pack.Url!, tempPath, expectedSize, innerToken, (delta) =>
                    {
                        Interlocked.Add(ref progress.DownloadedBytes, delta);
                        Report(InstallProgressState.Download);
                    }).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(pack.Md5))
                    {
                        bool isMatch = await CheckMd5Async(tempPath, pack.Md5, innerToken);
                        if (!isMatch)
                        {
                            try
                            {
                                File.Delete(tempPath);
                            }
                            catch
                            {
                            }

                            throw new Exception($"MD5 Mismatch for {fileName}. Expected: {pack.Md5}");
                        }
                    }

                    if (File.Exists(filePath)) File.Delete(filePath);
                    File.Move(tempPath, filePath);

                    Interlocked.Increment(ref progress.DownloadedCount);
                    Interlocked.Increment(ref progress.StateCount);
                });
            }

            progressStateDelegate?.Invoke(InstallProgressState.Install);

            await ExtractPackagesAsync(downloadDir, installPath, token, (extracted, total) =>
            {
                progress.DownloadedBytes = extracted;
                progress.TotalBytesToDownload = total;
                Report(InstallProgressState.Install);
            });
            // Directory.Delete(downloadDir, true); // 删除下载包
            progressStateDelegate?.Invoke(InstallProgressState.Completed);
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
            {
                throw new Exception($"Download incomplete. Expected {expectedSize}, got {finalInfo.Length}");
            }
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
            SharedStatic.InstanceLogger.LogInformation($"[EndfieldInstaller] 开始解压流程: {sourceDir} -> {destDir}");

            var archives = Directory.GetFiles(sourceDir);
            var firstVolume = archives.FirstOrDefault(f => f.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase)
                                                           || f.EndsWith(".001", StringComparison.OrdinalIgnoreCase));

            if (firstVolume == null)
            {
                firstVolume = archives.FirstOrDefault(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                                           || f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase));
            }

            if (firstVolume == null)
            {
                SharedStatic.InstanceLogger.LogError("[EndfieldInstaller] 未在下载目录找到有效的压缩包文件！");
                throw new FileNotFoundException("No archive found in Downloads folder");
            }

            SharedStatic.InstanceLogger.LogInformation($"[EndfieldInstaller] 发现主压缩包: {Path.GetFileName(firstVolume)}");

            await Task.Run(() =>
            {
                try
                {
                    using (var extractor = new SharpSevenZipExtractor(firstVolume))
                    {
                        long totalSize = 0;
                        foreach (var entry in extractor.ArchiveFileData)
                        {
                            if (!entry.IsDirectory) totalSize += (long)entry.Size;
                        }

                        extractor.Extracting += (sender, args) =>
                        {
                            if (token.IsCancellationRequested)
                            {
                                throw new OperationCanceledException();
                            }

                            long downloaded = (long)(totalSize * (args.PercentDone / 100.0));
                            progressCallback?.Invoke(downloaded, totalSize);
                        };

                        extractor.ExtractArchive(destDir);
                    }

                    SharedStatic.InstanceLogger.LogInformation("[EndfieldInstaller] 解压完成！");
                }
                catch (Exception ex)
                {
                    SharedStatic.InstanceLogger.LogError($"[EndfieldInstaller] 解压失败: {ex}");
                    throw;
                }
            }, token);
        }
    }
}