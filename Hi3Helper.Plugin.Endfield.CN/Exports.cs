using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;

namespace Hi3Helper.Plugin.Endfield.CN;

public partial class Exports : SharedStaticV1Ext<Exports>
{
    static Exports()
    {
        Load<EndfieldPlugin>(!RuntimeFeature.IsDynamicCodeCompiled ? new GameVersion(1, 0, 0, 0) : default);
    }

    [UnmanagedCallersOnly(EntryPoint = "TryGetApiExport", CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe int TryGetApiExport(char* exportName, void** delegateP)
    {
        return TryGetApiExportPointer(exportName, delegateP);
    }
}