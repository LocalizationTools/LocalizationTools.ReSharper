namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.Refactoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using JetBrains.Annotations;
    using JetBrains.Application.DataContext;
    using JetBrains.DocumentModel;
    using JetBrains.Metadata.Reader.API;
    using JetBrains.ProjectModel;
    using JetBrains.ReSharper.Feature.Services.Refactorings;
    using JetBrains.ReSharper.Feature.Services.Refactorings.Conflicts;
    using JetBrains.ReSharper.Feature.Services.Resx;
    using JetBrains.ReSharper.I18n.Services;
    using JetBrains.ReSharper.I18n.Services.CSharp.Services;
    using JetBrains.ReSharper.I18n.Services.Refactoring.ExtractToResource;
    using JetBrains.ReSharper.I18n.Services.Searching;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.CSharp;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.CSharp.Util;
    using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve.Managed;
    using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
    using JetBrains.ReSharper.Psi.Modules;
    using JetBrains.ReSharper.Psi.Resolve;
    using JetBrains.ReSharper.Psi.Resolve.Managed;
    using JetBrains.ReSharper.Psi.Tree;
    using JetBrains.ReSharper.Psi.Util;
    using JetBrains.ReSharper.Resources.Shell;
    using JetBrains.Util;

    [SolutionComponent]
    internal class AnyAttributeToResourceExtractor :
        ExpressionToResourceExtractorBase<ICSharpExpression>
    {
        private static readonly string[] LocalizableAttributeNames = new[] { "LocalizableAttribute", "LocalizationRequiredAttribute" };
        private static readonly string[] ResourceTypeAttributeNames = new[] { "ResourceTypeAttribute" };

        public AnyAttributeToResourceExtractor(
            ResourceAccessorFinder resourceAccessorFinder,
            ExpressionToResourceChecker expressionToResourceChecker)
            : base(resourceAccessorFinder, expressionToResourceChecker)
        {
        }

        public override byte Priority
        {
            get
            {
                return 4;
            }
        }

        public override bool IsAvailable(IDataContext context)
        {
            return this.GetSourceElement(context) is SourceElement sourceElement && sourceElement.IsValid();
        }

        public override ISourceElement GetSourceElement(ITreeNode node)
        {
            if (!(base.GetSourceElement(node) is ExpressionSourceElement<ICSharpExpression> sourceElement) ||
                !sourceElement.Expression.IsConstantValue() ||
                !sourceElement.Expression.ConstantValue.IsString())
            {
                return null;
            }

            return GetParameterSourceElement(sourceElement);
        }

        public override ISourceElement GetSourceElement(IDataContext dataContext)
        {
            return base.GetSourceElement(dataContext);
        }

        public override string GetDefaultResourceName(ISourceElement sourceElement)
        {
            if (!(sourceElement is SourceElement sourceElement1))
            {
                return null;
            }

            return ResourcesHelper.GetNewResourceIdentifier(sourceElement1.Expression, sourceElement.Value);
        }

        public override bool CanExtractTo(ISourceElement sourceElement, IPsiSourceFile resourceFile)
        {
            return sourceElement is SourceElement sourceElement1 && sourceElement1.IsValid() && this.CanUseResource(resourceFile, new ElementAccessContext(sourceElement1.Expression));
        }

        protected override bool IsApplicable(IExpression expression)
        {
            return base.IsApplicable(expression);
        }

        public override bool Extract(ISourceElement sourceElement, IResourceItem resourceItem, IRefactoringDriver driver)
        {
            SourceElement element = sourceElement as SourceElement;
            if (element == null || !element.IsValid())
            {
                return false;
            }

            ICollection<IResourceAccessor> accessors = this.ResourceAccessorFinder.FindAccessors(resourceItem.DeclaredElement);
            if (accessors.IsEmpty())
            {
                return false;
            }

            IProperty property = accessors.Select(accessor => accessor.DeclaredElement).OfType<IProperty>().FirstOrDefault();
            if (property == null)
            {
                return false;
            }

            if (driver != null && property.GetAccessRights() != AccessRights.PUBLIC)
            {
                driver.AddConflict(Conflict.Create(property, "The property {0} is not public. Resource access modifier should be public. Fix it in the 'Managed Resource Editor'."));
            }

            ITypeElement containingType = property.GetContainingType();
            if (containingType == null)
            {
                return false;
            }

            IAttribute containingAttribute = element.ContainingAttribute;

            var attributeElem = containingAttribute?.GetAttributeType().GetTypeElement();
            var argument = element.Expression.Parent as ICSharpArgument;
            var parameter = argument?.MatchingParameter?.Element;

            if (attributeElem == null || argument == null || parameter == null)
            {
                return false;
            }

            IConstructor newConstructor = null;

            foreach (var constructor in attributeElem.Constructors)
            {
                if (constructor.Parameters.Any(e => e.ShortName == parameter.ShortName))
                {
                    if (constructor.Parameters.Any(CheckParameterIsResourceTypeIndicator))
                    {
                        newConstructor = constructor;
                        break;
                    }
                }
            }

            if (newConstructor == null)
            {
                return false;
            }

            var newParams = new List<AttributeValue>();
            var psiModule = element.Expression.GetPsiModule();
            var factory = CSharpElementFactory.GetInstance(containingAttribute);
            var resolveContext = new ResolveContext(psiModule);
            var nameOfResourceProperty = property.ToLiteralOrNameofExpression(argument, factory);
            var position = 0;

            for (var i = 0; i < newConstructor.Parameters.Count; i++)
            {
                var constructorParameter = newConstructor.Parameters[i];
                if (constructorParameter.ShortName == element.Name)
                {
                    position = i;

                    ////var cval = nameOfResourceProperty.ConstantValue(resolveContext);
                    ////newParams.Add(new AttributeValue(cval));
                    newParams.Add(new AttributeValue(new ConstantValue(property.ShortName, psiModule)));
                }
                else if (AnyAttributeToResourceExtractor.CheckParameterIsResourceTypeIndicator(constructorParameter))
                {
                    newParams.Add(new AttributeValue(TypeFactory.CreateType(containingType)));
                }
                else
                {
                    var existingArgument = argument.ContainingArgumentList
                        .Arguments.FirstOrDefault(e => e.ArgumentName == constructorParameter.ShortName);

                    if (existingArgument?.Value != null)
                    {
                        newParams.Add(new AttributeValue(existingArgument.Value.ConstantValue));
                    }
                    else
                    {
                        newParams.Add(new AttributeValue(new ConstantValue(null, (IType)null)));
                    }
                }
            }

            ////var factory = CSharpElementFactory.GetInstance(containingAttribute.GetPsiModule());

            IAttribute attribute = factory.CreateAttribute(attributeElem, newParams.ToArray(), Array.Empty<Pair<string, AttributeValue>>());

            ////ModificationUtil.ReplaceChild(attribute.ConstructorArgumentExpressions[position], nameOfResourceProperty);

            ////IAttribute attribute =
            ////    .CreateAttribute(
            ////        containingAttribute.GetAttributeInstance()
            ////            .GetAttributeType()
            ////            .GetTypeElement(),
            ////        EmptyArray<AttributeValue>.Instance,
            ////        AnyAttributeToResourceExtractor.GetNamedParameters(
            ////            element.Name,
            ////            property.ShortName,
            ////            containingType,
            ////            element.Expression.GetPsiModule())
            ////            .ToArray());

            using (WriteLockCookie.Create())
            {
                ModificationUtil.ReplaceChild(containingAttribute, attribute);

                ////return this.FixResourceType(containingAttribute, attribute, element);
            }

            return true;
        }

        private bool FixResourceType(
            IAttribute oldAttribute,
            IAttribute attribute,
            SourceElement element)
        {
            IPropertyAssignment propertyAssignment1 = null;
            IPropertyAssignment oldChild = null;
            foreach (IPropertyAssignment propertyAssignment2 in oldAttribute.PropertyAssignments)
            {
                ICSharpIdentifier propertyNameIdentifier = propertyAssignment2.PropertyNameIdentifier;
                if (propertyNameIdentifier != null)
                {
                    if (string.Equals(propertyNameIdentifier.Name, "ResourceType", StringComparison.Ordinal))
                    {
                        oldChild = propertyAssignment2;
                    }
                    else if (string.Equals(propertyNameIdentifier.Name, element.Name, StringComparison.Ordinal))
                    {
                        propertyAssignment1 = propertyAssignment2;
                    }

                    if (propertyAssignment1 != null)
                    {
                        if (oldChild != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (oldChild == null && propertyAssignment1 != null)
            {
                using (WriteLockCookie.Create())
                {
                    TreeNodeCollection<IPropertyAssignment> propertyAssignments = oldAttribute.PropertyAssignments;
                    int count1 = propertyAssignments.Count;
                    ModificationUtil.AddChildRangeAfter(propertyAssignment1, new TreeRange(attribute.LPar.GetNextMeaningfulSibling(), attribute.RPar.GetPreviousMeaningfulSibling()));
                    int num1 = count1 + 2;
                    propertyAssignments = oldAttribute.PropertyAssignments;
                    int count2 = propertyAssignments.Count;
                    if (num1 != count2)
                    {
                        return false;
                    }

                    ModificationUtil.DeleteChild(propertyAssignment1);
                    int num2 = count1 + 1;
                    propertyAssignments = oldAttribute.PropertyAssignments;
                    int count3 = propertyAssignments.Count;
                    return num2 == count3;
                }
            }

            if (oldChild == null || propertyAssignment1 == null)
            {
                return false;
            }

            ModificationUtil.ReplaceChild(oldChild, attribute.PropertyAssignments.ElementAt(0));
            ModificationUtil.ReplaceChild(propertyAssignment1, attribute.PropertyAssignments.ElementAt(1));
            return true;
        }

        private static SourceElement GetParameterSourceElement(
            [NotNull] ExpressionSourceElement<ICSharpExpression> element)
        {
            var attribute = AttributeNavigator.GetByConstructorArgumentExpression(element.Expression);
            var attributeElem = attribute?.GetAttributeType().GetTypeElement();
            var argument = element.Expression.Parent as ICSharpArgument;
            var parameter = argument?.MatchingParameter?.Element;

            if (element.Expression.ConstantValue.IsString()
                && attribute != null && attributeElem != null && parameter != null && CheckParameterIsApplicable(parameter))
            {
                foreach (var constructor in attributeElem.Constructors)
                {
                    if (constructor.Parameters.Any(e => e.ShortName == parameter.ShortName))
                    {
                        if (constructor.Parameters.Any(CheckParameterIsResourceTypeIndicator))
                        {
                            return new SourceElement(element, argument, parameter);
                        }
                    }
                }
            }

            return null;
        }

        private static bool CheckParameterIsApplicable(IParameter parameter)
        {
            ////    argument.MatchingParameter.Element.HasAttributeInstance(new ClrTypeName(""), AttributesSource.All)

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

        private static IEnumerable<Pair<string, AttributeValue>> GetNamedParameters(
            string propertyName,
            string resourceName,
            ITypeElement resourceClassType,
            IPsiModule psiModule)
        {
            yield return Pair.Of("ResourceType", new AttributeValue(TypeFactory.CreateType(resourceClassType)));
            yield return Pair.Of(propertyName, new AttributeValue(new ConstantValue(resourceName, psiModule)));
        }

        private class SourceElement : ISourceElement
        {
            private readonly string myName;
            private readonly ExpressionSourceElement<ICSharpExpression> myElement;
            private readonly ITreeNodePointer<ICSharpArgument> myArgument;
            private readonly IParameter myParameter;

            public SourceElement(
                [NotNull] ExpressionSourceElement<ICSharpExpression> element,
                [NotNull] ICSharpArgument argument,
                [NotNull] IParameter parameter)
            {
                if (element == null)
                {
                    throw new ArgumentNullException(nameof(element));
                }

                if (argument == null)
                {
                    throw new ArgumentNullException(nameof(argument));
                }

                if (parameter == null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }

                this.myElement = element;
                this.myArgument = argument.GetPsiServices().Pointers.CreateTreeElementPointer(argument);
                this.myParameter = parameter;
                this.myName = parameter.ShortName;
            }

            [NotNull]
            public string Name
            {
                get
                {
                    return this.myName;
                }
            }

            public IType Type
            {
                get
                {
                    return this.myElement.Type;
                }
            }

            [CanBeNull]
            public IAttribute ContainingAttribute
            {
                get
                {
                    var attribute = AttributeNavigator.GetByArgument(this.myArgument.GetTreeNode());
                    return attribute;
                }
            }

            [NotNull]
            public ICSharpExpression Expression
            {
                get
                {
                    return this.myElement.Expression;
                }
            }

            public IPsiSourceFile SourceFile
            {
                get
                {
                    return this.myElement.SourceFile;
                }
            }

            public PsiLanguageType PsiLanguageType
            {
                get
                {
                    return this.myElement.PsiLanguageType;
                }
            }

            public DocumentRange DocumentRange
            {
                get
                {
                    return this.myElement.DocumentRange;
                }
            }

            public object Value
            {
                get
                {
                    return this.myElement.Value;
                }
            }

            public bool IsValid()
            {
                return this.myElement.IsValid();
            }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as SourceElement);
            }

            public override int GetHashCode()
            {
                return this.myArgument.GetTreeNode()?.GetHashCode() ?? this.myArgument.GetHashCode();
            }

            private bool Equals(
                SourceElement other)
            {
                if (other == null)
                {
                    return false;
                }

                return this == other || object.Equals(other.myArgument, this.myArgument);
            }
        }
    }
}
