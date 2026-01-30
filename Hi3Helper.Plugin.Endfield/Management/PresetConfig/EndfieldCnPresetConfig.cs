using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Endfield.Management.Api;

namespace Hi3Helper.Plugin.Endfield.Management.PresetConfig;

[GeneratedComClass]
public partial class EndfieldCnPresetConfig : PluginPresetConfigBase
{
    private const string ExEcutableName = "Endfield.exe";
 
    private const string ExApiUrl = "https://launcher.hypergryph.com/api/proxy/batch_proxy";
    private const string ExWebApiUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy";
    private const string ExAppCode = "6LL0KJuqHBVz33WK";
    private const string ExLauncherAppCode = "abYeZZ16BPluCFyT";
    private const string ExChannel = "1";
    private const string ExSubChannel = "1";
    private const string ExSeq = "5";

    [field: AllowNull] [field: MaybeNull] public override string GameName => field ??= "Arknights: Endfield";
    [field: AllowNull] [field: MaybeNull] public override string GameExecutableName => field ??= ExEcutableName;

    public override string GameAppDataPath
    {
        get
        {
            string? gamePath = null;
            GameManager?.GetGamePath(out gamePath);
            if (!string.IsNullOrEmpty(gamePath)) return Path.Combine(gamePath, "Endfield_Data");
            return string.Empty;
        }
    }

    [field: AllowNull] [field: MaybeNull] public override string GameLogFileName => field ??= null!;

    [field: AllowNull] [field: MaybeNull] public override string GameVendorName => field ??= "Hypergryph";
    [field: AllowNull] [field: MaybeNull] public override string GameRegistryKeyName => field ??= "Endfield";
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "EndfieldCn";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneDescription => field ??= "《明日方舟：终末地》是一款由鹰角网络出品的3D即时策略RPG。";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "Mainland China";
    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "明日方舟：终末地";
    [field: AllowNull] [field: MaybeNull] public override string ZoneLogoUrl => field ??= "";
    [field: AllowNull] [field: MaybeNull] public override string ZonePosterUrl => field ??= "";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://endfield.hypergryph.com/";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;

    [field: AllowNull] [field: MaybeNull] public override string GameMainLanguage => field ??= "zh-CN";

    [field: AllowNull]
    [field: MaybeNull]
    public override string LauncherGameDirectoryName => field ??= "Arknights Endfield Game";

    [field: AllowNull] [field: MaybeNull] public override List<string> SupportedLanguages => field ??= ["Chinese"];

    public override ILauncherApiMedia? LauncherApiMedia
    {
        get => field ??= new EndfieldLauncherApiMedia(ExWebApiUrl, ExAppCode, ExChannel, ExSubChannel, ExSeq);
        set;
    }

    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new EndfieldLauncherApiNews(ExWebApiUrl, ExAppCode, ExChannel, ExSubChannel, ExSeq);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new EndfieldGameManager(ExEcutableName, ExApiUrl, ExWebApiUrl, ExAppCode, ExLauncherAppCode,
            ExChannel, ExSubChannel, ExSeq);
        set;
    }

    public override IGameInstaller? GameInstaller
    {
        get => field ??= new EndfieldGameInstaller(GameManager!);
        set;
    }

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return Task.FromResult(0);
    }
}