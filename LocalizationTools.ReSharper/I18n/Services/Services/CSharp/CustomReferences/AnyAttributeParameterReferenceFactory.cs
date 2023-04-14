namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.CustomReferences
{
    using System.Linq;
    using JetBrains.Application.Threading;
    using JetBrains.DataFlow;
    using JetBrains.Lifetimes;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.Caches;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
    using JetBrains.ReSharper.Psi.Resolve;
    using JetBrains.ReSharper.Psi.Tree;
    using JetBrains.ReSharper.Psi.Util;

    internal class AnyAttributeParameterReferenceFactory :
        ResxAttributeParameterReferenceFactory
    {
        ////private const string ParameterName = "Description";
        ////private static readonly IClrTypeName AttributeClrName = new ClrTypeName("LocalizationTools.Attributes.LocalizedDescriptionAttribute");
        private static readonly string[] LocalizableAttributeNames = new[] { "LocalizableAttribute", "LocalizationRequiredAttribute" };
        private static readonly string[] ResourceTypeAttributeNames = new[] { "ResourceTypeAttribute" };

        public override bool HasReference(ITreeNode element, IReferenceNameContainer names)
        {
            if (!(element is ICSharpExpression expression) || !this.CheckExpressionIsApplicable(expression))
            {
                return false;
            }

            IAttribute attribute = AttributeNavigator.GetByConstructorArgumentExpression(expression);
            var argument = expression.Parent as ICSharpArgument;
            var parameter = argument?.MatchingParameter?.Element;

            if (attribute != null && parameter != null && this.CheckParameterIsApplicable(parameter))
            {
                if (expression.ConstantValue.IsString())
                {
                    if (parameter.ContainingParametersOwner?.Parameters.Any(e1 => e1.ShortName == "resourceType") ?? false)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected override bool CheckParameterIsApplicable(IParameter parameter)
        {
            var paramAttributes = parameter.GetAttributeInstances(true);

            foreach (var paramAttribute in paramAttributes)
            {
                var shortName = paramAttribute.GetAttributeShortName();

                if (LocalizableAttributeNames.Contains(shortName))
                {
                    return true;
                }
            }

            return false;
        }

        protected override bool CanUseOldReferences(
            ITreeNode element,
            ReferenceCollection oldReferences)
        {
            return oldReferences.Count == 1
                   && oldReferences[0] is PublicPropertyReference<AnyAttributeParameterReferenceFactory>
                   && ((TreeReferenceBase<ICSharpExpression>)oldReferences[0]).GetElement() == element;
        }

        protected override ReferenceCollection CreateReferenceCollection(ICSharpArgument argument, IParameter parameter, ICSharpExpression expression)
        {
            var typeParam = parameter.ContainingParametersOwner?.Parameters.FirstOrDefault(CheckParameterIsResourceTypeIndicator);

            ITypeElement containingType = parameter.GetContainingType();

            if (typeParam != null)
            {
                var reference = new ParameterReference<AnyAttributeParameterReferenceFactory>(expression, typeParam, typeParam.ShortName);
                var collection = new ReferenceCollection(reference);
                return collection;
            }

            return ReferenceCollection.Empty;
        }

        private static bool CheckParameterIsResourceTypeIndicator(IParameter parameter)
        {
            ////    argument.MatchingParameter.Element.HasAttributeInstance(new ClrTypeName(""), AttributesSource.All)

            if (parameter.IsConstant())
            {
                return false;
            }

            var paramAttributes = parameter.GetAttributeInstances(true);

            foreach (var paramAttribute in paramAttributes)
            {
                var shortName = paramAttribute.GetAttributeShortName();

                if (ResourceTypeAttributeNames.Contains(shortName))
                {
                    return true;
                }
            }

            return false;
        }

        [ReferenceProviderFactory]
        public class Factory : IReferenceProviderFactory
        {
            private readonly IShellLocks myShellLocks;

            public Factory(Lifetime lifetime, IShellLocks shellLocks)
            {
                this.myShellLocks = shellLocks;
                this.Changed = new Signal<IReferenceProviderFactory>(this.GetType().FullName);
            }

            public IReferenceFactory CreateFactory(
                IPsiSourceFile sourceFile,
                IFile file,
                IWordIndex wordIndexForChecks)
            {
                if (!(file is ICSharpFile))
                {
                    return null;
                }

                return new AnyAttributeParameterReferenceFactory();
            }

            public ISignal<IReferenceProviderFactory> Changed { get; private set; }
        }
    }
}
