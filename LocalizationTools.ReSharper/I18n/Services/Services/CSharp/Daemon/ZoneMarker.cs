namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.Daemon
{
    using JetBrains.Application.BuildScript.Application.Zones;
    using JetBrains.ReSharper.Feature.Services.Daemon;
    using JetBrains.ReSharper.Psi.CSharp;

    [ZoneMarker]
    public class ZoneMarker : IRequire<DaemonZone>, IRequire<ILanguageCSharpZone>
    {
    }
}
