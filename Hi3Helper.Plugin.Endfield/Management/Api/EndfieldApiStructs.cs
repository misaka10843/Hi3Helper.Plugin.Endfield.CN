using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hi3Helper.Plugin.Endfield.Management.Api;

// ==========================================
// 请求结构
// ==========================================
public class EndfieldBatchRequest
{
    [JsonPropertyName("seq")] public string Seq { get; set; } = null!;

    [JsonPropertyName("proxy_reqs")] public List<EndfieldProxyRequest> ProxyReqs { get; set; } = new();
}

public class EndfieldProxyRequest
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = null!;

    [JsonPropertyName("get_latest_game_req")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndfieldGetLatestGameReq? GetLatestGameReq { get; set; }

    // 通用请求体用于 Banner, News, BgImage, Sidebar
    [JsonPropertyName("get_banner_req")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndfieldCommonReq? GetBannerReq { get; set; }

    [JsonPropertyName("get_announcement_req")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndfieldCommonReq? GetAnnouncementReq { get; set; }

    [JsonPropertyName("get_main_bg_image_req")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndfieldCommonReq? GetMainBgImageReq { get; set; }

    [JsonPropertyName("get_sidebar_req")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndfieldCommonReq? GetSidebarReq { get; set; }
}

public class EndfieldGetLatestGameReq
{
    [JsonPropertyName("appcode")] public string AppCode { get; set; } = null!;
    [JsonPropertyName("channel")] public string Channel { get; set; } = null!;
    [JsonPropertyName("sub_channel")] public string SubChannel { get; set; } = null!;
    [JsonPropertyName("version")] public string Version { get; set; } = null!;
    [JsonPropertyName("launcher_appcode")] public string LauncherAppCode { get; set; } = null!;
}

public class EndfieldCommonReq
{
    [JsonPropertyName("appcode")] public string AppCode { get; set; } = null!;
    [JsonPropertyName("language")] public string Language { get; set; } = "zh-cn";
    [JsonPropertyName("channel")] public string Channel { get; set; } = null!;
    [JsonPropertyName("sub_channel")] public string SubChannel { get; set; } = null!;
    [JsonPropertyName("platform")] public string Platform { get; set; } = "Windows";
    [JsonPropertyName("source")] public string Source { get; set; } = "launcher";
}

// ==========================================
// 响应结构
// ==========================================
public class EndfieldBatchResponse
{
    [JsonPropertyName("proxy_rsps")] public List<EndfieldProxyResponse>? ProxyRsps { get; set; }
}

public class EndfieldProxyResponse
{
    [JsonPropertyName("kind")] public string? Kind { get; set; }

    [JsonPropertyName("get_latest_game_rsp")]
    public EndfieldGetLatestGameRsp? GetLatestGameRsp { get; set; }

    [JsonPropertyName("get_banner_rsp")] public EndfieldGetBannerRsp? GetBannerRsp { get; set; }

    [JsonPropertyName("get_announcement_rsp")]
    public EndfieldGetAnnouncementRsp? GetAnnouncementRsp { get; set; }

    [JsonPropertyName("get_main_bg_image_rsp")]
    public EndfieldGetMainBgImageRsp? GetMainBgImageRsp { get; set; }

    [JsonPropertyName("get_sidebar_rsp")] public EndfieldGetSidebarRsp? GetSidebarRsp { get; set; }
}

// --- 版本信息 ---
public class EndfieldGetLatestGameRsp
{
    [JsonPropertyName("action")] public int Action { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("pkg")] public EndfieldPkgInfo? Pkg { get; set; }
}

public class EndfieldPkgInfo
{
    [JsonPropertyName("packs")] public List<EndfieldPack>? Packs { get; set; }
    [JsonPropertyName("file_path")] public string? FilePath { get; set; }
}

public class EndfieldPack
{
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("md5")] public string? Md5 { get; set; }
    [JsonPropertyName("package_size")] public string? PackageSize { get; set; }
}

// --- Banner ---
public class EndfieldGetBannerRsp
{
    [JsonPropertyName("banners")] public List<EndfieldBanner>? Banners { get; set; }
}

public class EndfieldBanner
{
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("jump_url")] public string? JumpUrl { get; set; }
}

// --- 公告 ---
public class EndfieldGetAnnouncementRsp
{
    [JsonPropertyName("tabs")] public List<EndfieldAnnouncementTab>? Tabs { get; set; }
}

public class EndfieldAnnouncementTab
{
    [JsonPropertyName("tabName")] public string? TabName { get; set; }
    [JsonPropertyName("announcements")] public List<EndfieldAnnouncement>? Announcements { get; set; }
}

public class EndfieldAnnouncement
{
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("jump_url")] public string? JumpUrl { get; set; }
    [JsonPropertyName("start_ts")] public string? StartTs { get; set; } // 时间戳字符串
}

// --- 背景图 ---
public class EndfieldGetMainBgImageRsp
{
    [JsonPropertyName("main_bg_image")] public EndfieldBgImageInfo? MainBgImage { get; set; }
}

public class EndfieldBgImageInfo
{
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("video_url")] public string? VideoUrl { get; set; }
}

// --- Sidebar ---
public class EndfieldGetSidebarRsp
{
    [JsonPropertyName("sidebars")] public List<EndfieldSidebar>? Sidebars { get; set; }
}

public class EndfieldSidebar
{
    [JsonPropertyName("media")] public string? Media { get; set; }
    [JsonPropertyName("pic")] public EndfieldSidebarPic? Pic { get; set; }
    [JsonPropertyName("jump_url")] public string? JumpUrl { get; set; }
    [JsonPropertyName("sidebar_labels")] public List<EndfieldSidebarLabel>? SidebarLabels { get; set; }
}

public class EndfieldSidebarPic
{
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class EndfieldSidebarLabel
{
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("jump_url")] public string? JumpUrl { get; set; }
}