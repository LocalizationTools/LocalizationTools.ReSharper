namespace LocalizationTools.ReSharper.Daemon.Resx.CSharpDaemon
{
    using JetBrains.Application.BuildScript.Application.Zones;
    using JetBrains.ReSharper.Psi.CSharp;

    [ZoneMarker]
    public class ZoneMarker : IRequire<ILanguageCSharpZone>
    {
    }
}
