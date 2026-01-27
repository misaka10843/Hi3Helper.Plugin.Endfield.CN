using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Endfield.CN.Management.Api;
using Hi3Helper.Plugin.Endfield.CN.Utils;
using Microsoft.Extensions.Logging;

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
            
            if (_owner.GameManager is not EndfieldGameManager manager || manager.GamePacks == null || manager.GamePacks.Count == 0)
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

            // 初始化进度
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
                    MaxDegreeOfParallelism = Environment.ProcessorCount, 
                    CancellationToken = token 
                }, async (pack, innerToken) =>
                {
                    var fileName = Path.GetFileName(new Uri(pack.Url).LocalPath);
                    var filePath = Path.Combine(downloadDir, fileName);
                    var tempPath = filePath + ".tmp";

                    await DownloadFileAsync(pack.Url!, tempPath, filePath, innerToken, (delta) => 
                    {
                        Interlocked.Add(ref progress.DownloadedBytes, delta);
                        Report(InstallProgressState.Download);
                    }).ConfigureAwait(false);

                    if (File.Exists(tempPath)) 
                    {
                        if (File.Exists(filePath)) File.Delete(filePath);
                        File.Move(tempPath, filePath);
                    }

                    Interlocked.Increment(ref progress.DownloadedCount);
                    Interlocked.Increment(ref progress.StateCount);
                });
            }

            Report(InstallProgressState.Verify);
            // 计算本地文件 MD5 是否匹配 pack.Md5
            // 暂时略过严格校验，仅做基础存在性检查

            progressStateDelegate?.Invoke(InstallProgressState.Install);
            await ExtractPackagesAsync(downloadDir, installPath, token);

            // Directory.Delete(downloadDir, true); // 删除下载包
            progressStateDelegate?.Invoke(InstallProgressState.Completed);
        }

        private async Task DownloadFileAsync(string url, string tempPath, string finalPath, CancellationToken token, Action<long> onProgress)
        {
            long existingLength = 0;
            if (File.Exists(tempPath))
                existingLength = new FileInfo(tempPath).Length;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existingLength > 0)
                request.Headers.Range = new RangeHeaderValue(existingLength, null);

            using var response = await _owner._downloadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            
            if (existingLength > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            {
                existingLength = 0;
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            await using var fs = new FileStream(tempPath, existingLength > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write);

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
        }

        private Task ExtractPackagesAsync(string sourceDir, string destDir, CancellationToken token)
        {
            // TODO: 需接入解压逻辑
            
            SharedStatic.InstanceLogger.LogWarning("[EndfieldInstaller] 自动解压尚未实现。请手动解压 'Downloads' 文件夹中的压缩包到游戏根目录。");
            return Task.CompletedTask;
        }
    }
}