using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Endfield.Management.Api;

[GeneratedComClass]
public partial class EndfieldLauncherApiNews : LauncherApiNewsBase
{
    private readonly string _appCode;
    private readonly string _channel;
    private readonly string _seq;
    private readonly string _subChannel;
    private readonly string _webApiUrl;

    private EndfieldGetBannerRsp? _bannerResponse;
    private EndfieldGetAnnouncementRsp? _newsResponse;
    private EndfieldGetSidebarRsp? _sidebarResponse;

    public EndfieldLauncherApiNews(string webApiUrl, string appCode, string channel, string subChannel, string seq)
    {
        _webApiUrl = webApiUrl;
        _appCode = appCode;
        _channel = channel;
        _subChannel = subChannel;
        _seq = seq;
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

    protected override string ApiResponseBaseUrl => _webApiUrl;

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        var requestBody = new EndfieldBatchRequest
        {
            Seq = _seq,
            ProxyReqs = new[]
            {
                new EndfieldProxyRequest { Kind = "get_announcement", GetAnnouncementReq = CreateCommonReq() },
                new EndfieldProxyRequest { Kind = "get_banner", GetBannerReq = CreateCommonReq() },
                new EndfieldProxyRequest { Kind = "get_sidebar", GetSidebarReq = CreateCommonReq() }
            }.ToList()
        };

        try
        {
            var jsonRequest = JsonSerializer.Serialize(requestBody, EndfieldApiContext.Default.EndfieldBatchRequest);
            SharedStatic.InstanceLogger.LogDebug($"[EndfieldNews] Request Body:\n{jsonRequest}");

            using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            using var response = await ApiResponseHttpClient.PostAsync(_webApiUrl, content, token);

            SharedStatic.InstanceLogger.LogDebug($"[EndfieldNews] API Response Code: {response.StatusCode}");
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(token);
            SharedStatic.InstanceLogger.LogDebug($"[EndfieldNews] Response Body:\n{jsonResponse}");

            var rspBody = JsonSerializer.Deserialize(jsonResponse, EndfieldApiContext.Default.EndfieldBatchResponse);

            _newsResponse = rspBody?.ProxyRsps?.FirstOrDefault(x => x.Kind == "get_announcement")?.GetAnnouncementRsp;
            _bannerResponse = rspBody?.ProxyRsps?.FirstOrDefault(x => x.Kind == "get_banner")?.GetBannerRsp;
            _sidebarResponse = rspBody?.ProxyRsps?.FirstOrDefault(x => x.Kind == "get_sidebar")?.GetSidebarRsp;

            SharedStatic.InstanceLogger.LogDebug(
                $"[EndfieldNews] Parsed responses: News={_newsResponse != null}, Banner={_bannerResponse != null}, Sidebar={_sidebarResponse != null}");
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
            string tName = tab.TabName ?? "Info";

            foreach (var item in tab.Announcements)
            {
                flatList.Add(new FlatNewsItem { Item = item, TypeName = tName });
            }
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
        //Todo: i am unable to support all the languages. before the launcher allows custom tabs, this method can only be used temporarily for classification.
        for (int i = 0; i < count; i++)
        {
            var flatItem = flatList[i];
            var item = flatItem.Item;
            // 资讯/新闻/News/Other
            LauncherNewsEntryType type = LauncherNewsEntryType.Info;
            string typeNameLower = flatItem.TypeName?.ToLowerInvariant() ?? "";

            // 公告 / Notice
            if (typeNameLower.Contains("公告") ||
                typeNameLower.Contains("notice") ||
                typeNameLower.Contains("announcement") ||
                typeNameLower.Contains("お知らせ") ||
                typeNameLower.Contains("공지"))
            {
                type = LauncherNewsEntryType.Notice;
            }
            // 活动 / Event
            else if (typeNameLower.Contains("活动") ||
                     typeNameLower.Contains("活動") ||
                     typeNameLower.Contains("event") ||
                     typeNameLower.Contains("イベント") ||
                     typeNameLower.Contains("이벤트"))
            {
                type = LauncherNewsEntryType.Event;
            }

            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            if (!string.IsNullOrEmpty(item.StartTs) && long.TryParse(item.StartTs, out long ts))
            {
                try
                {
                    dateStr = DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime().ToString("yyyy-MM-dd");
                }
                catch
                {
                }
            }

            string content = item.Content ?? "";
            string jumpUrl = item.JumpUrl ?? "";

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
        // currently, we are unable to obtain the icon link from the API. the hard-coded method has certain limitations and we have temporarily stopped adding.
        if (true)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
        }
        else
        {
            var sidebars = _sidebarResponse?.Sidebars;
            if (sidebars == null || sidebars.Count == 0)
            {
                InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
                return;
            }

            count = sidebars.Count;
            var memory = PluginDisposableMemory<LauncherSocialMediaEntry>.Alloc(count);
            handle = memory.AsSafePointer();
            isDisposable = true;
            isAllocated = true;

            for (int i = 0; i < count; i++)
            {
                var item = sidebars[i];
                string iconUrl = item.Pic?.Url ?? "";
                string description = item.Pic?.Description ?? "";
                string jumpUrl = item.JumpUrl ?? "";

                ref var entry = ref memory[i];
                entry.WriteIcon(iconUrl);
                entry.WriteClickUrl(jumpUrl);
                entry.WriteDescription(description);
            }
        }
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream,
        PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress,
        CancellationToken token)
    {
        SharedStatic.InstanceLogger.LogDebug($"[EndfieldNews] Downloading asset: {fileUrl}");
        try
        {
            await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum,
                downloadProgress, token);
            SharedStatic.InstanceLogger.LogDebug(
                $"[EndfieldNews] Download COMPLETED: {fileUrl} (Size: {outputStream.Length} bytes)");
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[EndfieldNews] Download FAILED: {fileUrl}\nException: {ex}");
        }
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
            SubChannel = _subChannel,
            Language = SharedStatic.PluginLocaleCode?.ToLower() ?? "en-us"
        };
    }

    private struct FlatNewsItem
    {
        public EndfieldAnnouncement Item;
        public string TypeName;
    }
}