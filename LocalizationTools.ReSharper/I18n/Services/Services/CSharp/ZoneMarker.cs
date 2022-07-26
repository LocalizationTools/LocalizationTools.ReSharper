namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp
{
    using JetBrains.Application.BuildScript.Application.Zones;
    using JetBrains.ReSharper.Psi.CSharp;
    using JetBrains.ReSharper.Psi.Resx;

    [ZoneMarker]
    public class ZoneMarker : IRequire<ILanguageCSharpZone>, IRequire<ILanguageResxZone>
    {
    }
}
