namespace LocalizationTools.ReSharper
{
    using JetBrains.Application.BuildScript.Application.Zones;

    [ZoneDefinition]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "OK")]
    //// [ZoneDefinitionConfigurableFeature("Title", "Description", IsInProductSection: false)]
    public interface ILocalizationToolsReSharperZone : IZone
    {
    }
}
