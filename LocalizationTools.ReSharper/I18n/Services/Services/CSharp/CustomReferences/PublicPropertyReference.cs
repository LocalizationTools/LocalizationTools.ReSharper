namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.CustomReferences
{
    using JetBrains.ReSharper.I18n.Services.Resolve;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.CSharp.Tree;

    internal class PublicPropertyReference<T> : PropertyReference<T>
    {
        public PublicPropertyReference(
            ICSharpExpression expression,
            string resourceTypePropertyName)
            : base(expression, resourceTypePropertyName)
        {
        }

        protected override I18nResolveErrorType ResoleErrorType
        {
            get
            {
                return I18nResolveErrorType.PUBLIC_PROPERTY_NOT_RESOLVED;
            }
        }

        protected override bool IsValidAccessRights(AccessRights accessRights)
        {
            return accessRights == AccessRights.PUBLIC;
        }
    }
}
