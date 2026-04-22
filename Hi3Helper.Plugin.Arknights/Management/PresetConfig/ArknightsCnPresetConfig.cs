using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Hypergryph.Core.Management;
using Hi3Helper.Hypergryph.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;

namespace Hi3Helper.Plugin.Arknights.Management.PresetConfig;

[GeneratedComClass]
public partial class ArknightsCnPresetConfig : PluginPresetConfigBase
{
    private const string ExEcutableName = "Arknights.exe";
 
    private const string ExApiUrl = "https://launcher.hypergryph.com/api/proxy/batch_proxy";
    private const string ExWebApiUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy";
    private const string ExAppCode = "GzD1CpaWgmSq1wew";
    private const string ExLauncherAppCode = "abYeZZ16BPluCFyT";
    private const string ExChannel = "1";
    private const string ExSubChannel = "1";
    private const string ExSeq = "5";

    [field: AllowNull] [field: MaybeNull] public override string GameName => field ??= "Arknights";
    [field: AllowNull] [field: MaybeNull] public override string GameExecutableName => field ??= ExEcutableName;

    public override string GameAppDataPath
    {
        get
        {
            string? gamePath = null;
            GameManager?.GetGamePath(out gamePath);
            if (!string.IsNullOrEmpty(gamePath)) return Path.Combine(gamePath, "Arknights_Data");
            return string.Empty;
        }
    }

    [field: AllowNull] [field: MaybeNull] public override string GameLogFileName => field ??= null!;

    [field: AllowNull] [field: MaybeNull] public override string GameVendorName => field ??= "Hypergryph";
    [field: AllowNull] [field: MaybeNull] public override string GameRegistryKeyName => field ??= "Arknights";
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "ArknightsCn";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneDescription => field ??= "《明日方舟》是一款魔物主题的策略手游。";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "Mainland China";
    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "明日方舟 (中国大陆)";
    [field: AllowNull] [field: MaybeNull] public override string ZoneLogoUrl => field ??= "";
    [field: AllowNull] [field: MaybeNull] public override string ZonePosterUrl => field ??= "";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://ak.hypergryph.com/";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;

    [field: AllowNull] [field: MaybeNull] public override string GameMainLanguage => field ??= "zh-CN";

    [field: AllowNull]
    [field: MaybeNull]
    public override string LauncherGameDirectoryName => field ??= "Arknights Game";

    [field: AllowNull] [field: MaybeNull] public override List<string> SupportedLanguages => field ??= ["Chinese"];

    public override ILauncherApiMedia? LauncherApiMedia
    {
        get => field ??= new HgLauncherApiMedia(ExWebApiUrl, ExAppCode, ExChannel, ExSubChannel, ExSeq);
        set;
    }

    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new HgLauncherApiNews(ExWebApiUrl, ExAppCode, ExChannel, ExSubChannel, ExSeq);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new HgGameManager(ExEcutableName, ExApiUrl, ExWebApiUrl, ExAppCode, ExLauncherAppCode,
            ExChannel, ExSubChannel, ExSeq);
        set;
    }

    public override IGameInstaller? GameInstaller
    {
        get => field ??= new HgGameInstaller(GameManager!);
        set;
    }

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return Task.FromResult(0);
    }
}