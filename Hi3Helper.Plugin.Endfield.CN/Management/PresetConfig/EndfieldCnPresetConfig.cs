using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Endfield.CN.Management.Api;

namespace Hi3Helper.Plugin.Endfield.CN.Management.PresetConfig;

[GeneratedComClass]
public partial class EndfieldCnPresetConfig : PluginPresetConfigBase
{
    private const string ExEcutableName = "Endfield.exe";

    private const string ExAppCode = "6LL0KJuqHBVz33WK";
    private const string ExChannel = "1";
    private const string ExSubChannel = "1";

    [field: AllowNull] [field: MaybeNull] public override string GameName => field ??= "明日方舟：终末地";
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

    [field: AllowNull] [field: MaybeNull] public override string GameLogFileName => field ??= null;

    [field: AllowNull] [field: MaybeNull] public override string GameVendorName => field ??= "Hypergryph";
    [field: AllowNull] [field: MaybeNull] public override string GameRegistryKeyName => field ??= "Endfield";
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "EndfieldCn";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneDescription => field ??= "《明日方舟：终末地》是一款由鹰角网络出品的3D即时策略RPG。";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "国服";
    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "明日方舟：终末地 (CN)";
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
        get => field ??= new EndfieldLauncherApiMedia(ExAppCode, ExChannel, ExSubChannel);
        set;
    }

    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new EndfieldLauncherApiNews(ExAppCode, ExChannel, ExSubChannel);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new EndfieldGameManager(ExEcutableName, "");
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