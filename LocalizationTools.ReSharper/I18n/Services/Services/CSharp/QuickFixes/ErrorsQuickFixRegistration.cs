namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.QuickFixes
{
    using System;
    using System.Collections.Generic;
    using JetBrains.Application;
    using JetBrains.Lifetimes;
    using JetBrains.ReSharper.Feature.Services.QuickFixes;
    using JetBrains.ReSharper.I18n.Services.CSharp.QuickFixes;
    using LocalizationTools.ReSharper.I18n.Services.Services.CSharp.Daemon.Errors;

    [ShellComponent]
    public class ErrorsQuickFixRegistration : IQuickFixesProvider
    {
        public IEnumerable<Type> Dependencies => Array.Empty<Type>();

        public void Register(IQuickFixesRegistrar table)
        {
            table.RegisterQuickFix<LocalizableAttributeStringWarning>(Lifetime.Eternal, a => new MoveFromAttributeToResourceFix(a), typeof(MoveToResourceFix));
            table.RegisterQuickFix<LocalizableAttributeStringWarning>(Lifetime.Eternal, a => new UseExistentAttributeResourceFix(a), typeof(UseExistentAttributeResourceFix));
        }
    }
}