namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.QuickFixes
{
    using JetBrains.Application.BuildScript.Application.Zones;
    using JetBrains.ReSharper.Feature.Services;
    using JetBrains.ReSharper.Feature.Services.Daemon;
    using JetBrains.ReSharper.Psi.CSharp;

    [ZoneMarker]
    public class ZoneMarker :
        IRequire<DaemonZone>,
        IRequire<ICodeEditingZone>,
        IRequire<ILanguageCSharpZone>
    {
    }
}
