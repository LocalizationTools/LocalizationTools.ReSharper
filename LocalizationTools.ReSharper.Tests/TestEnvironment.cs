using System.Threading;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ReSharper.Feature.Services;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.TestFramework;
using JetBrains.TestFramework;
using JetBrains.TestFramework.Application.Zones;
using NUnit.Framework;

[assembly: Apartment(ApartmentState.STA)]

namespace LocalizationTools.ReSharper.Tests
{
    [ZoneDefinition]
    public class LocalizationToolsReSharperTestEnvironmentZone : ITestsEnvZone, IRequire<PsiFeatureTestZone>, IRequire<ILocalizationToolsReSharperZone> { }

[ZoneMarker]
public class ZoneMarker : IRequire<ICodeEditingZone>, IRequire<ILanguageCSharpZone>, IRequire<LocalizationToolsReSharperTestEnvironmentZone> { }

[SetUpFixture]
public class LocalizationToolsReSharperTestsAssembly: ExtensionTestEnvironmentAssembly<LocalizationToolsReSharperTestEnvironmentZone> { }
}
