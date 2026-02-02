using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Endfield.Management.Api;
using Hi3Helper.Plugin.Endfield.Utils;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Endfield.Management;

[GeneratedComClass]
internal partial class EndfieldGameManager : GameManagerBase
{
    private readonly string _apiUrl;
    private readonly string _appCode;
    private readonly string _channel;
    private readonly string _launcherAppCode;
    private readonly string _seq;
    private readonly string _subChannel;
    private readonly string _webApiUrl;

    private EndfieldGetLatestGameRsp? _latestGameInfo;

    internal EndfieldGameManager(string gameExecutableNameByPreset, string apiUrl, string webApiUrl, string appCode,
        string launcherAppCode, string channel, string subChannel, string seq)
    {
        CurrentGameExecutableByPreset = gameExecutableNameByPreset;
        _apiUrl = apiUrl;
        _webApiUrl = webApiUrl;
        _appCode = appCode;
        _launcherAppCode = launcherAppCode;
        _channel = channel;
        _subChannel = subChannel;
        _seq = seq;
    }

    internal string? GameResourceBaseUrl { get; set; }
    private bool IsInitialized { get; set; }

    internal List<EndfieldPack>? GamePacks => _latestGameInfo?.Pkg?.Packs;

    private string CurrentGameExecutableByPreset { get; }

    protected override HttpClient ApiResponseHttpClient { get; set; } = new();

    protected override bool IsInstalled
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentGameInstallPath)) return false;

            var exePath = Path.Combine(CurrentGameInstallPath, CurrentGameExecutableByPreset);
            var configPath = Path.Combine(CurrentGameInstallPath, "config.ini");

            return File.Exists(exePath) && File.Exists(configPath);
        }
    }

    protected override bool HasUpdate => IsInstalled && _latestGameInfo?.Action == 1;

    protected override bool HasPreload => false;
    protected override GameVersion ApiGameVersion { get; set; }

    protected override void SetGamePathInner(string gamePath)
    {
        SharedStatic.InstanceLogger.LogInformation($"[Endfield] SetGamePathInner called! Input path: '{gamePath}'");
        CurrentGameInstallPath = gamePath;

        _latestGameInfo = null;

        if (!string.IsNullOrEmpty(gamePath))
            _ = Task.Run(async () =>
            {
                try
                {
                    SharedStatic.InstanceLogger.LogInformation("[Endfield] Path updated, triggering re-initialization...");
                    await InitAsyncInner(true);
                }
                catch (Exception ex)
                {
                    SharedStatic.InstanceLogger.LogError($"[Endfield]Re-initialization failed: {ex}");
                }
            });
    }

    internal async Task<int> InitAsyncInner(bool forceInit = false, CancellationToken token = default)
    {
        if (!forceInit && IsInitialized) return 0;

        var requestVersion = "";

        SharedStatic.InstanceLogger.LogInformation($"[Endfield] InitAsyncInner started. Current path: '{CurrentGameInstallPath}'");

        if (IsInstalled)
            try
            {
                var configPath = Path.Combine(CurrentGameInstallPath!, "config.ini");
                if (File.Exists(configPath))
                {
                    SharedStatic.InstanceLogger.LogInformation($"[Endfield] Config file found: {configPath}");
                    var iniContent = ConfigTool.ReadConfig(configPath);
                    var ver = ConfigTool.ParseVersion(iniContent);
                    if (!string.IsNullOrEmpty(ver))
                    {
                        requestVersion = ver!;
                        CurrentGameVersion = new GameVersion(requestVersion);
                        SharedStatic.InstanceLogger.LogInformation($"[Endfield] Successfully read local version: {requestVersion}");
                    }
                }
            }
            catch (Exception ex)
            {
                SharedStatic.InstanceLogger.LogError($"[Endfield] Exception occurred while reading local configuration: {ex}");
            }
        else
            SharedStatic.InstanceLogger.LogWarning("[Endfield] No installation detected; version set to empty.");

        var requestBody = new EndfieldBatchRequest
        {
            Seq = _seq,
            ProxyReqs = new List<EndfieldProxyRequest>
            {
                new()
                {
                    Kind = "get_latest_game",
                    GetLatestGameReq = new EndfieldGetLatestGameReq
                    {
                        AppCode = _appCode,
                        LauncherAppCode = _launcherAppCode,
                        Channel = _channel,
                        SubChannel = _subChannel,
                        Version = requestVersion
                    }
                }
            }
        };

        try
        {
            using var response = await ApiResponseHttpClient!.PostAsJsonAsync(_apiUrl, requestBody,
                EndfieldApiContext.Default.EndfieldBatchRequest, token);
            response.EnsureSuccessStatusCode();

            var responseBody =
                await response.Content.ReadFromJsonAsync(EndfieldApiContext.Default.EndfieldBatchResponse, token);
            _latestGameInfo = responseBody?.ProxyRsps?.FirstOrDefault(x => x.Kind == "get_latest_game")
                ?.GetLatestGameRsp;

            if (_latestGameInfo == null)
            {
                SharedStatic.InstanceLogger.LogError("[Endfield] API data error: get_latest_game_rsp is null.");
                return -1;
            }

            SharedStatic.InstanceLogger.LogInformation(
                $"[Endfield] API Response - Action: {_latestGameInfo.Action}, Version: {_latestGameInfo.Version}");

            if (!string.IsNullOrEmpty(_latestGameInfo.Version))
                ApiGameVersion = new GameVersion(_latestGameInfo.Version);

            if (_latestGameInfo.Pkg != null) GameResourceBaseUrl = _latestGameInfo.Pkg.FilePath;

            IsInitialized = true;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[Endfield] API request failed: {ex}");
            return -1;
        }

        return 0;
    }

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return InitAsyncInner(true, token);
    }

    protected override void SetCurrentGameVersionInner(in GameVersion gameVersion)
    {
        CurrentGameVersion = gameVersion;
    }

    protected override Task<string?> FindExistingInstallPathAsyncInner(CancellationToken token)
    {
        return Task.FromResult<string?>(null);
    }

    public override void LoadConfig()
    {
    }

    public override void SaveConfig()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
        ApiResponseHttpClient?.Dispose();
    }
}