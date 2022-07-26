namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.Daemon.Errors
{
    using JetBrains.DocumentModel;
    using JetBrains.ReSharper.Feature.Services.Daemon;
    using JetBrains.ReSharper.Feature.Services.Resx;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.Util;

    [ConfigurableSeverityHighlighting("LocalizableElement", "CSHARP", Languages = "CSHARP", OverlapResolve = OverlapResolveKind.WARNING, ToolTipFormatString = "Localizable atttribute string: \"{0}\"")]
    public class LocalizableAttributeStringWarning : IHighlighting
    {
        protected const string MESSAGE = "Localizable attribute string: \"{0}\"";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "OK")]
        public const string HIGHLIGHTING_ID = "LocalizableElement";

        public LocalizableAttributeStringWarning(ICSharpExpression expression, DocumentRange range)
        {
            this.Expression = expression;
            this.Range = range;
            this.ToolTip = string.Format("Localizable attribute string: \"{0}\"", StringLiteralConverter.EscapeToRegular(this.Expression.ConstantValue.Value.ToString().TrimToLength()));
        }

        public ICSharpExpression Expression { get; }

        public DocumentRange Range { get; }

        public string ToolTip { get; }

        public string ErrorStripeToolTip => this.ToolTip;

        public DocumentRange CalculateRange() => this.Range;

        public bool IsValid() => this.Expression != null && this.Expression.IsValid();
    }
}
