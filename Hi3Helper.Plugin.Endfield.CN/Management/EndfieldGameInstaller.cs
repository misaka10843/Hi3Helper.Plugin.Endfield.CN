using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Endfield.CN.Management;

[GeneratedComClass]
internal partial class EndfieldGameInstaller : GameInstallerBase
{
    private readonly HttpClient _downloadHttpClient;

    internal EndfieldGameInstaller(IGameManager? gameManager) : base(gameManager)
    {
        _downloadHttpClient = new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.GZip)
            .AllowCookies()
            .AllowRedirections()
            .AllowUntrustedCert()
            .Create();
    }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        if (GameManager is not EndfieldGameManager endfieldManager)
            throw new InvalidOperationException("GameManager is not EndfieldGameManager");

        return await endfieldManager.InitAsyncInner(true, token).ConfigureAwait(false);
    }

    protected override async Task<long> GetGameSizeAsyncInner(GameInstallerKind gameInstallerKind,
        CancellationToken token)
    {
        await InitAsync(token).ConfigureAwait(false);

        if (GameManager is not EndfieldGameManager { GamePacks: { } packs }) return 0L;

        long totalSize = 0;
        foreach (var pack in packs)
            if (long.TryParse(pack.PackageSize, out var size))
                totalSize += size;
        return totalSize;
    }

    protected override async Task<long> GetGameDownloadedSizeAsyncInner(GameInstallerKind gameInstallerKind,
        CancellationToken token)
    {
        await InitAsync(token).ConfigureAwait(false);
        if (GameManager is not EndfieldGameManager { GamePacks: { } packs }) return 0L;

        GameManager.GetGamePath(out var installPath);
        if (string.IsNullOrEmpty(installPath)) return 0L;

        var downloadDir = Path.Combine(installPath, "Downloads");
        if (!Directory.Exists(downloadDir)) return 0L;

        long downloadedSize = 0;
        foreach (var pack in packs)
        {
            if (string.IsNullOrEmpty(pack.Url)) continue;

            var fileName = Path.GetFileName(new Uri(pack.Url).LocalPath);
            var filePath = Path.Combine(downloadDir, fileName);

            if (File.Exists(filePath))
            {
                downloadedSize += new FileInfo(filePath).Length;
            }
            else
            {
                var tempPath = filePath + ".tmp";
                if (File.Exists(tempPath))
                    downloadedSize += new FileInfo(tempPath).Length;
            }
        }

        return downloadedSize;
    }

    protected override Task StartInstallAsyncInner(InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
    {
        return StartInstallCoreAsync(GameInstallerKind.Install, progressDelegate, progressStateDelegate, token);
    }

    protected override Task StartUpdateAsyncInner(InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
    {
        return StartInstallCoreAsync(GameInstallerKind.Update, progressDelegate, progressStateDelegate, token);
    }

    protected override Task StartPreloadAsyncInner(InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
    {
        return StartInstallCoreAsync(GameInstallerKind.Preload, progressDelegate, progressStateDelegate, token);
    }

    private Task StartInstallCoreAsync(GameInstallerKind kind, InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
    {
        var installer = new Install(this);
        return installer.RunAsync(kind, progressDelegate, progressStateDelegate, token);
    }

    protected override Task UninstallAsyncInner(CancellationToken token)
    {
        GameManager.IsGameInstalled(out var isInstalled);
        if (!isInstalled) return Task.CompletedTask;

        GameManager.GetGamePath(out var installPath);
        if (string.IsNullOrEmpty(installPath)) return Task.CompletedTask;

        try
        {
            if (Directory.Exists(installPath))
                Directory.Delete(installPath, true);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[Endfield] Uninstall failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _downloadHttpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}