namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.CustomReferences
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using JetBrains.Annotations;
    using JetBrains.ReSharper.I18n.Services.Resolve;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.CSharp;
    using JetBrains.ReSharper.Psi.CSharp.Impl;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.CSharp.Util;
    using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
    using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
    using JetBrains.ReSharper.Psi.Resolve;
    using JetBrains.ReSharper.Psi.Resx.Tree;
    using JetBrains.ReSharper.Psi.Tree;
    using JetBrains.ReSharper.Resources.Shell;
    using JetBrains.Util;
    using JetBrains.Util.Logging;

    internal class ParameterReference<T> :
        CheckedReferenceBase<ICSharpExpression>,
        IResourceItemReference,
        IResourceReference,
        ICheckedReference,
        IReference,
        IUserDataHolder,
        ICompletableReference
    {
        private readonly IParameter parameter;
        private readonly string myResourceTypePropertyName;

        public ParameterReference(ICSharpExpression expression, IParameter parameter, string resourceTypePropertyName)
            : base(expression)
        {
            this.parameter = parameter;
            this.myResourceTypePropertyName = resourceTypePropertyName;
        }

        protected virtual I18nResolveErrorType ResoleErrorType
        {
            get
            {
                return I18nResolveErrorType.PROPERTY_NOT_RESOLVED;
            }
        }

        public override string GetName()
        {
            if (this.myOwner.ConstantValue.IsString())
            {
                return (string)this.myOwner.ConstantValue.Value;
            }

            return "???";
        }

        public override TreeTextRange GetTreeTextRange()
        {
            if (this.myOwner is ICSharpLiteralExpression owner)
            {
                TreeTextRange contentTreeRange = owner.GetStringLiteralContentTreeRange();
                if (contentTreeRange.Length != 0)
                {
                    return contentTreeRange;
                }
            }

            return this.myOwner.GetTreeTextRange();
        }

        public string GetDefaultName()
        {
            return this.GetName();
        }

        public ICollection<IPsiSourceFile> FindResourceFiles()
        {
            return EmptyList<IPsiSourceFile>.InstanceList;
        }

        public override ResolveResultWithInfo ResolveWithoutCache()
        {
            return CheckedReferenceImplUtil.Resolve(this, this.GetReferenceSymbolTable(true));
        }

        public ISymbolTable GetCompletionSymbolTable()
        {
            return this.GetReferenceSymbolTable(false);
        }

        public override ISymbolTable GetReferenceSymbolTable(bool useReferenceName)
        {
            IAttribute attribute = AttributeNavigator.GetByConstructorArgumentExpression(this.myOwner);

            if (attribute == null)
            {
                return EmptySymbolTable.INSTANCE;
            }

            var paramValue = attribute
                .GetAttributeInstance()
                .PositionParameters()
                .ToList()[this.parameter.IndexOf()];

            if (paramValue?.TypeValue == null)
            {
                return EmptySymbolTable.INSTANCE;
            }

            return paramValue.TypeValue.GetSymbolTable(this.myOwner.GetPsiModule())
                .Filter(new PredicateFilter(info =>
                    info.GetDeclaredElement() is IProperty declaredElement &&
                    declaredElement.IsStatic && this.IsValidAccessRights(declaredElement.GetAccessRights()) &&
                    declaredElement.IsReadable &&
                    declaredElement.ReturnType.IsString()));
        }

        public override IReference BindTo(IDeclaredElement element)
        {
            if (!(element is IProperty property))
            {
                return this;
            }

            if (!property.IsStatic)
            {
                return this;
            }

            if (!this.IsValidAccessRights(property.GetAccessRights()))
            {
                return this;
            }

            if (!property.IsReadable)
            {
                return this;
            }

            if (!property.ReturnType.IsString())
            {
                return this;
            }

            IPropertyAssignment bySource = PropertyAssignmentNavigator.GetBySource(this.myOwner);
            if (bySource == null)
            {
                return this;
            }

            Pair<string, AttributeValue> pair1 = bySource.ContainingAttribute.GetAttributeInstance().NamedParameters().FirstOrDefault(pair => string.Equals(pair.First, this.myResourceTypePropertyName, StringComparison.Ordinal));
            if (pair1.First == null || !pair1.Second.IsType)
            {
                return this;
            }

            IDeclaredType scalarType = pair1.Second.TypeValue.GetScalarType();
            if (scalarType == null)
            {
                return this;
            }

            CSharpElementFactory instance = CSharpElementFactory.GetInstance(this.myOwner);
            ITypeElement typeElement = scalarType.GetTypeElement();
            if (typeElement != null && !typeElement.Properties.Contains(property))
            {
                IPropertyAssignment propertyAssignment = bySource.ContainingAttribute.PropertyAssignments.FirstOrDefault(node => string.Equals(node.PropertyNameIdentifier.Name, this.myResourceTypePropertyName, StringComparison.Ordinal));
                if (propertyAssignment != null)
                {
                    using (WriteLockCookie.Create())
                    {
                        propertyAssignment.SetSource(instance.CreateExpression("typeof($0)", property.GetContainingType()));
                    }
                }
            }

            IReference reference = this.TryBindNameofExpression(instance, property.ShortName);
            if (reference != null)
            {
                return reference;
            }

            using (WriteLockCookie.Create())
            {
                return ModificationUtil.ReplaceChild(this.myOwner, instance.CreateStringLiteralExpression(property.ShortName)).GetReferences<PropertyReference<T>>().First();
            }
        }

        public override IReference BindTo(
            IDeclaredElement element,
            ISubstitution substitution)
        {
            return this.BindTo(element);
        }

        public override ResolveResultWithInfo GetResolveResult(
            ISymbolTable symbolTable,
            string referenceName)
        {
            ResolveResultWithInfo resolveResult = base.GetResolveResult(symbolTable, referenceName);
            if (resolveResult.ResolveErrorType == ResolveErrorType.OK)
            {
                return resolveResult;
            }

            return new ResolveResultWithInfo(resolveResult.Result, this.ResoleErrorType);
        }

        public override IAccessContext GetAccessContext()
        {
            return new ElementAccessContext(this.myOwner);
        }

        public override ISymbolFilter[] GetSymbolFilters()
        {
            return EmptyArray<ISymbolFilter>.Instance;
        }

        protected virtual bool IsValidAccessRights(AccessRights accessRights)
        {
            switch (accessRights)
            {
                case AccessRights.PUBLIC:
                case AccessRights.INTERNAL:
                case AccessRights.PROTECTED_OR_INTERNAL:
                case AccessRights.PROTECTED_AND_INTERNAL:
                    return true;
                default:
                    return false;
            }
        }

        [CanBeNull]
        private IReference TryBindNameofExpression(
            CSharpElementFactory factory,
            string shortName)
        {
            try
            {
                if (!(this.myOwner is IInvocationExpression owner) || !owner.IsNameofOperator())
                {
                    return null;
                }

                ICSharpExpression conditionalQualifier1 = owner.ConditionalQualifier;
                if (conditionalQualifier1 == null)
                {
                    return null;
                }

                ICSharpArgument csharpArgument = owner.Arguments.SingleItem();
                ICSharpExpression csharpExpression;
                if (csharpArgument == null)
                {
                    csharpExpression = null;
                }
                else
                {
                    csharpExpression = csharpArgument.Value;
                }

                if (!(csharpExpression is IReferenceExpression referenceExpression))
                {
                    return null;
                }

                ICSharpExpression conditionalQualifier2 = referenceExpression.ConditionalQualifier;
                ICSharpIdentifier nameIdentifier = referenceExpression.NameIdentifier;
                if (conditionalQualifier2 == null || nameIdentifier == null)
                {
                    return null;
                }

                using (WriteLockCookie.Create())
                {
                    return ModificationUtil.ReplaceChild(this.myOwner, factory.CreateExpression("$0($1.$2)", conditionalQualifier1, conditionalQualifier2, shortName)).GetReferences<PropertyReference<T>>().First();
                }
            }
            catch (Exception ex)
            {
                Logger.GetLogger(nameof(PropertyReference<T>)).Verbose(ex);
                return null;
            }
        }
    }
}
