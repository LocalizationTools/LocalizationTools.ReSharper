namespace LocalizationTools.ReSharper.I18n.Services.Services.CSharp.QuickFixes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using JetBrains.Annotations;
    using JetBrains.Application.DataContext;
    using JetBrains.Application.Progress;
    using JetBrains.Application.UI.Actions.ActionManager;
    using JetBrains.Diagnostics;
    using JetBrains.Lifetimes;
    using JetBrains.ProjectModel;
    using JetBrains.ProjectModel.DataContext;
    using JetBrains.ReSharper.Feature.Services.Bulbs;
    using JetBrains.ReSharper.Feature.Services.QuickFixes;
    using JetBrains.ReSharper.Feature.Services.Refactorings;
    using JetBrains.ReSharper.Feature.Services.Resx;
    using JetBrains.ReSharper.I18n.Services;
    using JetBrains.ReSharper.I18n.Services.Refactoring.ExtractToResource;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.DataContext;
    using JetBrains.ReSharper.Psi.Tree;
    using JetBrains.ReSharper.Resources.Shell;
    using JetBrains.TextControl;
    using JetBrains.TextControl.DataContext;
    using JetBrains.Util;
    using LocalizationTools.ReSharper.I18n.Services.Services.CSharp.Daemon.Errors;

    public class MoveFromAttributeToResourceFix : QuickFixBase, IReadOnlyBulbAction, IBulbAction
    {
        [CanBeNull]
        private readonly ICSharpExpression myExpression;

        public MoveFromAttributeToResourceFix([NotNull] LocalizableAttributeStringWarning error) => this.myExpression = error.Expression;

        public override string Text => "Move from attribute to resource";

        public bool IsReadOnly => true;

        public override bool IsAvailable(IUserDataHolder cache)
        {
            if (this.myExpression == null || !this.myExpression.IsValid())
            {
                return false;
            }

            ISolution solution = this.myExpression.GetSolution();
            ISolutionResourceCache component = solution.GetComponent<ISolutionResourceCache>();
            ICollection<Pair<ISourceElement, IResourceExtractor>> sourceElements = MoveFromAttributeToResourceFix.GetSourceElements(solution.GetComponents<IResourceExtractor>(), this.myExpression);
            if (sourceElements.Count == 0)
            {
                return false;
            }

            foreach (IPsiSourceFile referencedProject in component.GetResourcesInReferencedProjects(this.myExpression.GetProject(), file => file.IsDefaultCulture()))
            {
                foreach (Pair<ISourceElement, IResourceExtractor> pair in sourceElements)
                {
                    if (pair.Second.CanExtractTo(pair.First, referencedProject))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected override Action<ITextControl> ExecutePsiTransaction(
          ISolution solution,
          IProgressIndicator progress)
        {
            return null;
        }

        public override void Execute(ISolution solution, ITextControl textControl)
        {
            using (LifetimeDefinition lifetimeDefinition = Lifetime.Define(Lifetime.Eternal))
            {
                ICSharpExpression csharpExpression = this.myExpression.NotNull("expression != null");
                IList<IDataRule> datarulesAdditional = DataRules.AddRule(nameof(MoveFromAttributeToResourceFix), TextControlDataConstants.TEXT_CONTROL, textControl).AddRule(nameof(MoveFromAttributeToResourceFix), ProjectModelDataConstants.SOLUTION, solution).AddRule(nameof(MoveFromAttributeToResourceFix), PsiDataConstants.SELECTED_EXPRESSION, csharpExpression);
                RefactoringActionUtil.ExecuteRefactoring(Shell.Instance.GetComponent<IActionManager>().DataContexts.CreateWithDataRules(lifetimeDefinition.Lifetime, datarulesAdditional), new MoveToResourceDrivenWorkflow(solution, null));
            }
        }

        [NotNull]
        private static ICollection<Pair<ISourceElement, IResourceExtractor>> GetSourceElements(
          [NotNull] IEnumerable<IResourceExtractor> extractors,
          [NotNull] ICSharpExpression expression)
        {
            List<Pair<ISourceElement, IResourceExtractor>> sourceElements = new List<Pair<ISourceElement, IResourceExtractor>>();
            foreach (IResourceExtractor second in extractors.OrderByDescending(e => e.Priority))
            {
                ISourceElement sourceElement = second.GetSourceElement(expression);
                if (sourceElement != null)
                {
                    sourceElements.Add(Pair.Of(sourceElement, second));
                }
            }

            return sourceElements;
        }
    }
}
