namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.CustomReferences
{
    using System.Collections.Generic;
    using JetBrains.Annotations;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.Resolve;
    using JetBrains.ReSharper.Psi.Tree;

    public abstract class ResxAttributeParameterReferenceFactory : IReferenceFactory
    {
        public ReferenceCollection GetReferences(
            ITreeNode element,
            ReferenceCollection oldReferences)
        {
            if (this.CanUseOldReferences(element, oldReferences))
            {
                return oldReferences;
            }

            if (!(element is ICSharpExpression expression)
                || !this.CheckExpressionIsApplicable(expression)
                || !this.HasReference(element, new ReferenceNameContainer(new List<string>(), false)))
            {
                return ReferenceCollection.Empty;
            }

            var argument = expression.Parent as ICSharpArgument;
            var parameter = argument?.MatchingParameter?.Element;
            return this.CreateReferenceCollection(argument, parameter, expression);
        }

        public abstract bool HasReference(ITreeNode element, IReferenceNameContainer names);

        protected abstract bool CheckParameterIsApplicable(IParameter name);

        protected abstract bool CanUseOldReferences(
            ITreeNode element,
            ReferenceCollection oldReferences);

        protected abstract ReferenceCollection CreateReferenceCollection(
            [NotNull] ICSharpArgument property,
            [NotNull] IParameter propertyAssignment,
            [NotNull] ICSharpExpression expression);

        protected virtual bool CheckExpressionIsApplicable([NotNull] ICSharpExpression expression)
        {
            return true;
        }
    }
}