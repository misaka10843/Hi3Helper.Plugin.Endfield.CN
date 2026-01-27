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
using Hi3Helper.Plugin.Endfield.CN.Management.Api;
using Hi3Helper.Plugin.Endfield.CN.Utils;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Endfield.CN.Management;

[GeneratedComClass]
internal partial class EndfieldGameManager : GameManagerBase
{
    private const string ApiUrl = "https://launcher.hypergryph.com/api/proxy/batch_proxy";
    private const string WebApiUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy"; 
    
    private const string AppCode = "6LL0KJuqHBVz33WK";
    private const string LauncherAppCode = "abYeZZ16BPluCFyT";
    private const string Channel = "1";
    private const string SubChannel = "1";

    private EndfieldGetLatestGameRsp? _latestGameInfo;

    internal string? GameResourceBaseUrl { get; set; }
    private bool IsInitialized { get; set; }

    // 【新增】公开安装包列表供 Installer 使用
    internal List<EndfieldPack>? GamePacks => _latestGameInfo?.Pkg?.Packs;

    internal EndfieldGameManager(string gameExecutableNameByPreset, string apiResponseUrl)
    {
        CurrentGameExecutableByPreset = gameExecutableNameByPreset;
    }

    private string CurrentGameExecutableByPreset { get; }
    
    protected override HttpClient? ApiResponseHttpClient { get; set; } = new HttpClient();

    protected override void SetGamePathInner(string gamePath)
    {
        SharedStatic.InstanceLogger.LogWarning($"[Endfield] SetGamePathInner 被调用! 传入路径: '{gamePath}'");
        CurrentGameInstallPath = gamePath;
        
        _latestGameInfo = null; 
        
        if (!string.IsNullOrEmpty(gamePath))
        {
            _ = Task.Run(async () => 
            {
                try 
                {
                    SharedStatic.InstanceLogger.LogWarning("[Endfield] 路径已更新，触发重新初始化...");
                    await InitAsyncInner(true);
                }
                catch (Exception ex)
                {
                    SharedStatic.InstanceLogger.LogError($"[Endfield] 重新初始化失败: {ex}");
                }
            });
        }
    }

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

    internal async Task<int> InitAsyncInner(bool forceInit = false, CancellationToken token = default)
    {
        if (!forceInit && IsInitialized) return 0;

        string requestVersion = "1.0.0";
        
        SharedStatic.InstanceLogger.LogWarning($"[Endfield] InitAsyncInner 开始运行. 当前路径: '{CurrentGameInstallPath}'");

        if (IsInstalled)
        {
            try
            {
                string configPath = Path.Combine(CurrentGameInstallPath, "config.ini");
                if (File.Exists(configPath))
                {
                    SharedStatic.InstanceLogger.LogWarning($"[Endfield] 发现配置文件: {configPath}");
                    string iniContent = ConfigTool.ReadConfig(configPath);
                    string? ver = ConfigTool.ParseVersion(iniContent);
                    if (!string.IsNullOrEmpty(ver))
                    {
                        requestVersion = ver!;
                        CurrentGameVersion = new GameVersion(requestVersion);
                        SharedStatic.InstanceLogger.LogWarning($"[Endfield] 成功读取本地版本: {requestVersion}");
                    }
                }
            }
            catch (Exception ex)
            {
                SharedStatic.InstanceLogger.LogError($"[Endfield] 读取本地配置时发生异常: {ex}");
            }
        }
        else
        {
            SharedStatic.InstanceLogger.LogWarning($"[Endfield] 未检测到安装，使用默认版本 1.0.0");
        }

        var requestBody = new EndfieldBatchRequest
        {
            ProxyReqs = new List<EndfieldProxyRequest>
            {
                new EndfieldProxyRequest
                {
                    Kind = "get_latest_game",
                    GetLatestGameReq = new EndfieldGetLatestGameReq
                    {
                        AppCode = AppCode,
                        LauncherAppCode = LauncherAppCode,
                        Channel = Channel,
                        SubChannel = SubChannel,
                        Version = requestVersion
                    }
                }
            }
        };

        try
        {
            using var response = await ApiResponseHttpClient!.PostAsJsonAsync(ApiUrl, requestBody, EndfieldApiContext.Default.EndfieldBatchRequest, token);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadFromJsonAsync(EndfieldApiContext.Default.EndfieldBatchResponse, cancellationToken: token);
            _latestGameInfo = responseBody?.ProxyRsps?.FirstOrDefault(x => x.Kind == "get_latest_game")?.GetLatestGameRsp;

            if (_latestGameInfo == null)
            {
                SharedStatic.InstanceLogger.LogError("[Endfield] API 返回数据异常: get_latest_game_rsp 为空");
                return -1;
            }

            SharedStatic.InstanceLogger.LogWarning($"[Endfield] API 响应 - Action: {_latestGameInfo.Action}, Version: {_latestGameInfo.Version}");

            if (!string.IsNullOrEmpty(_latestGameInfo.Version))
            {
                ApiGameVersion = new GameVersion(_latestGameInfo.Version);
            }
            
            if (_latestGameInfo.Pkg != null)
            {
                GameResourceBaseUrl = _latestGameInfo.Pkg.FilePath; 
            }

            IsInitialized = true;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[Endfield] API 请求失败: {ex}");
            return -1;
        }

        return 0;
    }

    public async Task<string?> GetDynamicBackgroundImageUrl(CancellationToken token = default)
    {
        var requestBody = new EndfieldBatchRequest
        {
            ProxyReqs = new List<EndfieldProxyRequest>
            {
                new EndfieldProxyRequest 
                { 
                    Kind = "get_main_bg_image", 
                    GetMainBgImageReq = new EndfieldCommonReq 
                    { 
                        AppCode = AppCode, 
                        Channel = Channel, 
                        SubChannel = SubChannel 
                    } 
                }
            }
        };

        try 
        {
            using var response = await ApiResponseHttpClient!.PostAsJsonAsync(WebApiUrl, requestBody, EndfieldApiContext.Default.EndfieldBatchRequest, token);
            response.EnsureSuccessStatusCode();
            
            var responseBody = await response.Content.ReadFromJsonAsync(EndfieldApiContext.Default.EndfieldBatchResponse, cancellationToken: token);
            var bgInfo = responseBody?.ProxyRsps?.FirstOrDefault(x => x.Kind == "get_main_bg_image")?.GetMainBgImageRsp?.MainBgImage;
            
            return bgInfo?.Url;
        }
        catch
        {
            return null;
        }
    }

    protected override Task<int> InitAsync(CancellationToken token) => InitAsyncInner(true, token);

    protected override bool HasPreload => false; 
    protected override GameVersion ApiGameVersion { get; set; }

    protected override void SetCurrentGameVersionInner(in GameVersion gameVersion) => CurrentGameVersion = gameVersion;
    protected override Task<string?> FindExistingInstallPathAsyncInner(CancellationToken token) => Task.FromResult<string?>(null);
    public override void LoadConfig() { }
    public override void SaveConfig() { }
    public override void Dispose() { base.Dispose(); ApiResponseHttpClient?.Dispose(); }
}