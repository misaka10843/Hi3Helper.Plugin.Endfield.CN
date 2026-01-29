using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Endfield.CN.Management.Api;

[GeneratedComClass]
public partial class EndfieldLauncherApiNews : LauncherApiNewsBase
{
    private const string ExWebApiUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy";
    private readonly string _appCode;
    private readonly string _channel;
    private readonly string _subChannel;
    private EndfieldGetBannerRsp? _bannerResponse;

    private EndfieldGetAnnouncementRsp? _newsResponse;

    public EndfieldLauncherApiNews(string appCode, string channel, string subChannel)
    {
        _appCode = appCode;
        _channel = channel;
        _subChannel = subChannel;
    }

    [field: AllowNull] [field: MaybeNull] protected override HttpClient ApiResponseHttpClient { get; set; } = new();

    [field: AllowNull]
    [field: MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.GZip)
            .AllowCookies()
            .AllowRedirections()
            .AllowUntrustedCert()
            .Create();
        set;
    }

    protected override string ApiResponseBaseUrl => ExWebApiUrl;

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        var requestBody = new EndfieldBatchRequest
        {
            ProxyReqs = new[]
            {
                new EndfieldProxyRequest { Kind = "get_announcement", GetAnnouncementReq = CreateCommonReq() },
                new EndfieldProxyRequest { Kind = "get_banner", GetBannerReq = CreateCommonReq() }
            }.ToList()
        };

        try
        {
            using var response = await ApiResponseHttpClient.PostAsJsonAsync(ExWebApiUrl, requestBody,
                EndfieldApiContext.Default.EndfieldBatchRequest, token);
            response.EnsureSuccessStatusCode();

            var rspBody =
                await response.Content.ReadFromJsonAsync(EndfieldApiContext.Default.EndfieldBatchResponse, token);

            _newsResponse = rspBody?.ProxyRsps?.FirstOrDefault(x => x.Kind == "get_announcement")?.GetAnnouncementRsp;
            _bannerResponse = rspBody?.ProxyRsps?.FirstOrDefault(x => x.Kind == "get_banner")?.GetBannerRsp;

            return 0;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[EndfieldNews] Failed to init news: {ex}");
            return -1;
        }
    }

    public override void GetNewsEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        if (_newsResponse?.Tabs == null)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        var flatList = new List<FlatNewsItem>();
        foreach (var tab in _newsResponse.Tabs)
        {
            if (tab.Announcements == null) continue;
            var tName = tab.TabName ?? "公告";

            foreach (var item in tab.Announcements) flatList.Add(new FlatNewsItem { Item = item, TypeName = tName });
        }

        if (flatList.Count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        count = flatList.Count;
        var memory = PluginDisposableMemory<LauncherNewsEntry>.Alloc(count);
        handle = memory.AsSafePointer();
        isDisposable = true;
        isAllocated = true;

        for (var i = 0; i < count; i++)
        {
            var flatItem = flatList[i];
            var item = flatItem.Item;

            var type = flatItem.TypeName switch
            {
                "公告" => LauncherNewsEntryType.Notice,
                "新闻" => LauncherNewsEntryType.Info,
                "资讯" => LauncherNewsEntryType.Info,
                "活动" => LauncherNewsEntryType.Event,
                _ => LauncherNewsEntryType.Info
            };

            var dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            if (!string.IsNullOrEmpty(item.StartTs) && long.TryParse(item.StartTs, out var ts))
                try
                {
                    dateStr = DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime().ToString("yyyy-MM-dd");
                }
                catch
                {
                }

            var content = item.Content ?? "";
            var jumpUrl = item.JumpUrl ?? "";

            ref var entry = ref memory[i];
            entry.Write(content, null, jumpUrl, dateStr, type);
        }
    }

    public override void GetCarouselEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        var banners = _bannerResponse?.Banners;
        if (banners == null || banners.Count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        count = banners.Count;
        var memory = PluginDisposableMemory<LauncherCarouselEntry>.Alloc(count);
        handle = memory.AsSafePointer();
        isDisposable = true;
        isAllocated = true;

        for (var i = 0; i < count; i++)
        {
            var banner = banners[i];
            var imgUrl = banner.Url ?? "";
            var jumpUrl = banner.JumpUrl ?? "";

            ref var entry = ref memory[i];
            entry.Write(null, imgUrl, jumpUrl);
        }
    }

    public override void GetSocialMediaEntries(out nint handle, out int count, out bool isDisposable,
        out bool isAllocated)
    {
        InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream,
        PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress,
        CancellationToken token)
    {
        await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress,
            token);
    }

    private static void InitializeEmpty(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        handle = nint.Zero;
        count = 0;
        isDisposable = false;
        isAllocated = false;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        ApiResponseHttpClient?.Dispose();
        ApiDownloadHttpClient?.Dispose();
        base.Dispose();
    }

    private EndfieldCommonReq CreateCommonReq()
    {
        return new EndfieldCommonReq
        {
            AppCode = _appCode,
            Channel = _channel,
            SubChannel = _subChannel
        };
    }

    private struct FlatNewsItem
    {
        public EndfieldAnnouncement Item;
        public string TypeName;
    }
}