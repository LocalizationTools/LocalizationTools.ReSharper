namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.QuickFixes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using JetBrains.Annotations;
    using JetBrains.Application.Progress;
    using JetBrains.ProjectModel;
    using JetBrains.ReSharper.Feature.Services.Bulbs;
    using JetBrains.ReSharper.Feature.Services.Intentions;
    using JetBrains.ReSharper.Feature.Services.QuickFixes;
    using JetBrains.ReSharper.Feature.Services.Resx;
    using JetBrains.ReSharper.Feature.Services.Resx.Services;
    using JetBrains.ReSharper.I18n.Services;
    using JetBrains.ReSharper.I18n.Services.Impl;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.Resolve;
    using JetBrains.ReSharper.Psi.Tree;
    using JetBrains.TextControl;
    using JetBrains.Util;
    using JetBrains.Util.DataStructures;
    using JetBrains.Util.Special;
    using LocalizationTools.ReSharper.I18n.Services.Services.CSharp.Daemon.Errors;

    internal class UseExistentAttributeResourceFix : IQuickFix
    {
        private readonly ICSharpExpression myExpression;

        public UseExistentAttributeResourceFix(LocalizableAttributeStringWarning error)
        {
            this.myExpression = error.Expression;
            this.Items = EmptyArray<IBulbAction>.Instance;
            this.Items = UseExistentAttributeResourceFix.GetBulbItems(this.myExpression, this.myExpression.ConstantValue.Value as string);
        }

        public IBulbAction[] Items { get; private set; }

        public IEnumerable<IntentionAction> CreateBulbItems()
        {
            return this.Items.ToQuickFixIntentions();
        }

        public bool IsAvailable(IUserDataHolder cache)
        {
            return this.myExpression.IsValid() && Enumerable.Any(this.Items);
        }

        [NotNull]
        [ItemNotNull]
        private static IBulbAction[] GetBulbItems([CanBeNull] ITreeNode treeNode, [CanBeNull] string value)
        {
            if (treeNode == null || !treeNode.IsValid() || string.IsNullOrEmpty(value))
            {
                return EmptyArray<IBulbAction>.Instance;
            }

            ISolution solution = treeNode.GetSolution();
            ISolutionResourceCache component = solution.GetComponent<ISolutionResourceCache>();
            List<IBulbAction> bulbActionList = new List<IBulbAction>();
            ElementAccessContext context = new ElementAccessContext(treeNode);
            List<IResourceExtractor> list = solution.GetComponents<IResourceExtractor>().ToList();
            foreach (IPsiSourceFile referencedProject in component.GetResourcesInReferencedProjects(treeNode.GetProject(), file => file.IsDefaultCulture()))
            {
                IResourceProvider service = referencedProject.TryGetService<IResourceProvider>();
                if (service != null)
                {
                    foreach (IResourceExtractor extractor in list)
                    {
                        if (extractor.CanUseResource(referencedProject, context))
                        {
                            ISourceElement sourceElement = extractor.GetSourceElement(treeNode);
                            CompactOneToListMap<int, IResourceItem> itemsByHashValue;
                            if (sourceElement != null && extractor.CanExtractTo(sourceElement, referencedProject) && component.TryGetMapItemsByHashValue(referencedProject, out itemsByHashValue))
                            {
                                foreach (IResourceItem resourceItem in itemsByHashValue[value.GetHashCode()])
                                {
                                    ConstantValue resourceItemValue = service.GetResourceItemValue(referencedProject, resourceItem.Name);
                                    if (!resourceItemValue.IsString() || value.Equals(resourceItemValue.Value as string, StringComparison.Ordinal))
                                    {
                                        bulbActionList.Add(new UseResorceQuickFix(sourceElement, extractor, resourceItem.DeclaredElement));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return bulbActionList.ToArray();
        }

        private class UseResorceQuickFix : BulbActionBase
        {
            private readonly ISourceElement mySourceElement;
            private readonly IResourceExtractor myExtractor;
            private readonly IResourceItemDeclaredElement myResourceItemDeclaredElement;
            private readonly string myText;

            public UseResorceQuickFix(
                ISourceElement sourceElement,
                IResourceExtractor extractor,
                IResourceItemDeclaredElement resourceItemDeclaredElement)
            {
                this.mySourceElement = sourceElement;
                this.myExtractor = extractor;
                this.myResourceItemDeclaredElement = resourceItemDeclaredElement;
                this.myText = string.Format("Use {0} '{1}' instead of literal", DeclaredElementPresenter.Format(resourceItemDeclaredElement.PresentationLanguage, DeclaredElementPresenter.KIND_PRESENTER, resourceItemDeclaredElement), DeclaredElementPresenter.Format(resourceItemDeclaredElement.PresentationLanguage, DeclaredElementPresenter.NAME_PRESENTER, resourceItemDeclaredElement));
            }

            public override string Text
            {
                get
                {
                    return this.myText;
                }
            }

            protected override Action<ITextControl> ExecutePsiTransaction(
                ISolution solution,
                IProgressIndicator progress)
            {
                ISolutionResourceCache resourceManager = solution.GetComponent<ISolutionResourceCache>();
                IResourceItem resourceItem = this.myResourceItemDeclaredElement.GetDeclarations().SelectMany(declaration => declaration.GetSourceFile().IfNotNull(file => resourceManager.EnumerateResourceItems(file, declaration.DeclaredName))).FirstOrDefault();
                if (resourceItem != null)
                {
                    this.myExtractor.Extract(this.mySourceElement, resourceItem, null);
                }

                return null;
            }
        }
    }
}
