namespace LocalizationTools.ReSharper.Daemon.Resx.CSharpDaemon
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using JetBrains.Annotations;
    using JetBrains.Application;
    using JetBrains.Application.Settings;
    using JetBrains.Application.Threading.Tasks;
    using JetBrains.DocumentModel;
    using JetBrains.ProjectModel;
    using JetBrains.ReSharper.Daemon.CSharp.Stages;
    using JetBrains.ReSharper.Daemon.Stages;
    using JetBrains.ReSharper.Feature.Services.CSharp.Daemon;
    using JetBrains.ReSharper.Feature.Services.Daemon;
    using JetBrains.ReSharper.Feature.Services.Localization;
    using JetBrains.ReSharper.Feature.Services.Resx;
    using JetBrains.ReSharper.I18n.Services;
    using JetBrains.ReSharper.I18n.Services.CSharp;
    using JetBrains.ReSharper.I18n.Services.CSharp.Options;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.CodeAnnotations;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.CSharp.Util;
    using JetBrains.ReSharper.Psi.Files;
    using JetBrains.ReSharper.Psi.Resx.Tree;
    using JetBrains.ReSharper.Psi.Services;
    using JetBrains.ReSharper.Psi.SourceGenerators;
    using JetBrains.ReSharper.Psi.Tree;
    using JetBrains.Util;
    using JetBrains.Util.DataStructures.Collections;
    using JetBrains.Util.Special;
    using LocalizationTools.ReSharper.I18n.Services.Services.CSharp.Daemon.Errors;

    [DaemonStage(
        HighlightingTypes = new[] { typeof(LocalizableAttributeStringWarning) },
        StagesBefore = new[] { typeof(LanguageSpecificDaemonStage), typeof(GlobalFileStructureCollectorStage) })]
    public class LocalizableAttributeDaemonStage : CSharpDaemonStageBase
    {
        private readonly CodeAnnotationsCache myCodeAnnotationsCache;

        public LocalizableAttributeDaemonStage(CodeAnnotationsCache codeAnnotationsCache)
        {
            this.myCodeAnnotationsCache = codeAnnotationsCache;
        }

        protected override IDaemonStageProcess CreateProcess(
            IDaemonProcess process,
            IContextBoundSettingsStore settings,
            DaemonProcessKind processKind,
            ICSharpFile file1)
        {
            if (process.SourceFile.ToProjectFile().IfNotNull(file => file.GetProject()) == null)
            {
                return null;
            }

            return new LocalizableAttributeProcess(
                settings.GetValue(CSharpLocalizationOptionsSettingsAccessor.DontAnalyseVerbatimStrings),
                LocalizableProperty.GetLocalizableProperty(settings),
                LocalizableInspectorProperty.GetLocalizableInspectorProperty(settings),
                this.myCodeAnnotationsCache,
                process,
                settings,
                file1);
        }

        private class LocalizableAttributeProcess : CSharpIncrementalDaemonStageProcessBase
        {
            [NotNull]
            private readonly IDictionary<IDeclaredElement, Localizable> myCacheLocalizableItems;

            private readonly bool myDontAnalyseVerbatimStrings;
            private readonly bool myHasAvailableResources;
            private readonly LocalizableInspector myInspector;

            [NotNull]
            private readonly LiteralService myLiteralService;

            private readonly Localizable myLocalizable;

            [NotNull]
            private readonly LocalizationRequiredAnnotationProvider myLocalizationRequiredAnnotationProvider;

            [NotNull]
            private readonly OneToListMap<ICSharpTypeMemberDeclaration, DocumentRange> myMemberRanges;

            [NotNull]
            private readonly JetHashSet<ICSharpExpression> myProcessedItems;

            public LocalizableAttributeProcess(
                bool dontAnalyseVerbatimStrings,
                Localizable localizable,
                LocalizableInspector inspector,
                CodeAnnotationsCache codeAnnotationsCache,
                IDaemonProcess process,
                IContextBoundSettingsStore settingsStore,
                ICSharpFile file1)
                : base(process, settingsStore, file1)
            {
                this.myDontAnalyseVerbatimStrings = dontAnalyseVerbatimStrings;
                this.myLocalizable = localizable;
                this.myInspector = inspector;
                this.myCacheLocalizableItems = new Dictionary<IDeclaredElement, Localizable>();
                this.myProcessedItems = new JetHashSet<ICSharpExpression>();
                this.myLiteralService = LiteralService.Get(this.File);
                this.myHasAvailableResources = LocalizableAttributeProcess.CalculateHasAvailableResources(localizable, process.SourceFile);
                this.myLocalizationRequiredAnnotationProvider = codeAnnotationsCache.GetProvider<LocalizationRequiredAnnotationProvider>();
                this.myMemberRanges = new OneToListMap<ICSharpTypeMemberDeclaration, DocumentRange>();
            }

            public override void Execute(Action<DaemonStageResult> committer)
            {
                CSharpFileStructure fileStructure = this.FileStructure;
                this.ExploreDocumentRanges(fileStructure);
                using (PooledHashSet<ICSharpTypeMemberDeclaration> instance = PooledHashSet<ICSharpTypeMemberDeclaration>.GetInstance())
                {
                    DocumentRange documentRange = this.DaemonProcess.VisibleRange;
                    foreach (ICSharpTypeMemberDeclaration element in fileStructure.MembersToRehighlight)
                    {
                        foreach (DocumentRange intersectingRange in this.File.GetIntersectingRanges(element.GetTreeTextRange()))
                        {
                            if (intersectingRange.IntersectsOrContacts(in documentRange))
                            {
                                instance.Add(element);
                                break;
                            }
                        }
                    }

                    ////PsiIsolationScope isolationScope = PsiIsolationScope.Current;
                    ////foreach (ICSharpTypeMemberDeclaration memberDeclaration in instance)
                    ////{
                    ////    ICSharpTypeMemberDeclaration declaration = memberDeclaration;
                    ////    using (CompilationContextCookie.GetOrCreate(this.ResolveContext))
                    ////    {
                    ////        using (PsiIsolationScope.SetForCurrentThread(isolationScope))
                    ////        {
                    ////            MemberHighlighter(declaration);
                    ////        }
                    ////    }
                    ////}

                    ////if (this.DaemonProcess.FullRehighlightingRequired)
                    ////{
                    ////    using (CompilationContextCookie.GetOrCreate(this.ResolveContext))
                    ////    {
                    ////        using (PsiIsolationScope.SetForCurrentThread(isolationScope))
                    ////        {
                    ////            GlobalHighlighter();
                    ////        }
                    ////    }
                    ////}

                    ////foreach (ICSharpTypeMemberDeclaration memberDeclaration in fileStructure.MembersToRehighlight)
                    ////{
                    ////    ICSharpTypeMemberDeclaration declaration = memberDeclaration;
                    ////    if (!instance.Contains(declaration))
                    ////    {
                    ////        using (CompilationContextCookie.GetOrCreate(this.ResolveContext))
                    ////        {
                    ////            using (PsiIsolationScope.SetForCurrentThread(isolationScope))
                    ////            {
                    ////                MemberHighlighter(declaration);
                    ////            }
                    ////        }
                    ////    }
                    ////}

                    using (ITaskBarrier fibers = this.DaemonProcess.CreateFibers())
                    {
                        PsiIsolationScope isolationScope = PsiIsolationScope.Current;
                        foreach (ICSharpTypeMemberDeclaration memberDeclaration in instance)
                        {
                            ICSharpTypeMemberDeclaration declaration = memberDeclaration;
                            fibers.EnqueueJob(() =>
                            {
                                using (CompilationContextCookie.GetOrCreate(this.ResolveContext))
                                {
                                    using (PsiIsolationScope.SetForCurrentThread(isolationScope))
                                    {
                                        MemberHighlighter(declaration);
                                    }
                                }
                            });
                        }

                        if (this.DaemonProcess.FullRehighlightingRequired)
                        {
                            fibers.EnqueueJob(() =>
                            {
                                using (CompilationContextCookie.GetOrCreate(this.ResolveContext))
                                {
                                    using (PsiIsolationScope.SetForCurrentThread(isolationScope))
                                    {
                                        GlobalHighlighter();
                                    }
                                }
                            });
                        }

                        foreach (ICSharpTypeMemberDeclaration memberDeclaration in fileStructure.MembersToRehighlight)
                        {
                            ICSharpTypeMemberDeclaration declaration = memberDeclaration;
                            if (!instance.Contains(declaration))
                            {
                                fibers.EnqueueJob(() =>
                                {
                                    using (CompilationContextCookie.GetOrCreate(this.ResolveContext))
                                    {
                                        using (PsiIsolationScope.SetForCurrentThread(isolationScope))
                                        {
                                            MemberHighlighter(declaration);
                                        }
                                    }
                                });
                            }
                        }
                    }

                    if (!this.DaemonProcess.FullRehighlightingRequired)
                    {
                        return;
                    }

                    committer(new DaemonStageResult(EmptyList<HighlightingInfo>.Instance));
                }

                void MemberHighlighter(ICSharpTypeMemberDeclaration declaration)
                {
                    OneToListMap<ICSharpTypeMemberDeclaration, DocumentRange>.ValueCollection memberRange = this.myMemberRanges[declaration];
                    if (memberRange.Count == 0)
                    {
                        return;
                    }

                    FilteringHighlightingConsumer consumer = new FilteringHighlightingConsumer(this.DaemonProcess.SourceFile, this.File, this.DaemonProcess.ContextBoundSettingsStore);
                    declaration.ProcessThisAndDescendants(new LocalProcessor(this, consumer));
                    foreach (DaemonStageResult daemonStageResult in DaemonStageResult.CreateResultsPerRehighlightRange(consumer.Highlightings, memberRange))
                    {
                        committer(daemonStageResult);
                    }
                }

                void GlobalHighlighter()
                {
                    FilteringHighlightingConsumer consumer = new FilteringHighlightingConsumer(this.DaemonProcess.SourceFile, this.File, this.DaemonProcess.ContextBoundSettingsStore);
                    this.File.ProcessThisAndDescendants(new GlobalProcessor(this, consumer));
                    committer(new DaemonStageResult(consumer.Highlightings, 1));
                }
            }

            private void ExploreDocumentRanges([NotNull] CSharpFileStructure structure)
            {
                foreach (ICSharpTypeMemberDeclaration key in structure.MembersToRehighlight)
                {
                    ITreeNode element = key;
                    if (key is IMultipleDeclarationMember declarationMember)
                    {
                        element = declarationMember.MultipleDeclaration;
                    }

                    bool flag = false;
                    IDocument document1 = null;
                    IDocument document2 = null;
                    foreach (DocumentRange intersectingRange in this.File.GetIntersectingRanges(element.GetTreeTextRange()))
                    {
                        if (intersectingRange.Document == this.Document)
                        {
                            this.myMemberRanges.AddValue(key, intersectingRange);
                        }
                        else
                        {
                            if (!flag)
                            {
                                if (this.File.GetSourceFile() is ISourceGeneratorOutputFile sourceFile)
                                {
                                    document2 = sourceFile.Document;
                                    if (this.Document == sourceFile.AssociatedEmbeddedSourceDocument)
                                    {
                                        document1 = sourceFile.AssociatedEmbeddedSourceDocument;
                                    }
                                    else if (this.Document == sourceFile.AssociatedEditorDocument)
                                    {
                                        document1 = sourceFile.AssociatedEditorDocument;
                                    }
                                }

                                flag = true;
                            }

                            if (document1 != null && this.Document == document1 && intersectingRange.Document == document2)
                            {
                                this.myMemberRanges.AddValue(key, intersectingRange);
                            }
                        }
                    }
                }
            }

            private static bool CalculateHasAvailableResources(Localizable localizable, IPsiSourceFile sourceFile)
            {
                if (localizable == Localizable.No)
                {
                    return false;
                }

                IProject project1 = sourceFile.ToProjectFile().IfNotNull(file => file.GetProject());
                if (project1 == null)
                {
                    return false;
                }

                ISolution solution = project1.GetSolution();
                List<IResourceChecker> extractors = solution.GetComponents<IResourceChecker>().ToList();
                ISolutionResourceCache component = solution.GetComponent<ISolutionResourceCache>();

                ResourceAccessibleUtil.ResourceAccessContext context =
                    new ResourceAccessibleUtil.ResourceAccessContext(
                        project1,
                        sourceFile.PsiModule.TargetFrameworkId);

                IProject project2 = project1;

                return component.GetResourcesInReferencedProjects(
                    project2,
                    file => LocalizableAttributeProcess.CheckInterruption() && file.IsDefaultCulture())
                    .Any(file => extractors.Any(extractor =>
                        LocalizableAttributeProcess.CheckInterruption() &&
                        extractor.CanUseResource(
                            file,
                            context)));
            }

            public override bool InteriorShouldBeProcessed(
                ITreeNode element,
                IHighlightingConsumer context)
            {
                if (this.myLocalizable == Localizable.No
                    || (this.myLocalizable == Localizable.Default && !this.myHasAvailableResources))
                {
                    return false;
                }

                if (element is IParameter parameter)
                {
                    return LocalizableManager.IsLocalizable(
                        parameter,
                        this.myCacheLocalizableItems,
                        this.myLocalizationRequiredAnnotationProvider) !=
                           Localizable.No;
                }

                if (element is IAttribute &&
                    element.Parent?.Parent?.Parent?.Parent is ITypeMemberDeclaration parent &&
                    parent.IsValid() &&
                    !(parent is ITypeDeclaration))
                {
                    return true;
                    ////return LocalizableManager.IsLocalizable(parent.DeclaredElement,
                    ////           this.myCacheLocalizableItems,
                    ////           this.myLocalizationRequiredAnnotationProvider) !=
                    ////       Localizable.No;
                }

                return base.InteriorShouldBeProcessed(element, context);
            }

            public override void VisitCSharpLiteralExpression(
                ICSharpLiteralExpression literalExpression,
                IHighlightingConsumer consumer)
            {
                if (!literalExpression.Literal.GetTokenType().IsStringLiteral)
                {
                    return;
                }

                var isConst = this.myLiteralService.IsConstantLiteral(literalExpression);
                Debug.Print("VisitCSharpLiteralExpression - IsConst: {0} - Expr: {1}", isConst, literalExpression);

                ////if (!this.myLiteralService.IsConstantLiteral(literalExpression))
                ////{
                ////    return;
                ////}

                if (this.IsProcessed(literalExpression))
                {
                    return;
                }

                if (this.myDontAnalyseVerbatimStrings && this.myLiteralService.IsVerbatimStringLiteral(literalExpression))
                {
                    return;
                }

                ICSharpExpression localizableExpression;
                Localizable localizable = LocalizableManager.IsLocalizable(
                    literalExpression,
                    out localizableExpression,
                    this.myCacheLocalizableItems,
                    this.myLocalizationRequiredAnnotationProvider);

                if (localizableExpression == null)
                {
                    return;
                }

                if (!this.NeedToLocalizeElement(localizable))
                {
                    return;
                }

                if (this.LocalizeExpression(localizableExpression, consumer))
                {
                    return;
                }

                this.AddHighlighting(consumer, literalExpression);
            }

            public override void VisitInterpolatedStringExpression(
                IInterpolatedStringExpression interpolatedStringExpressionParam,
                IHighlightingConsumer consumer)
            {
                if (this.IsProcessed(interpolatedStringExpressionParam) ||
                    (this.myDontAnalyseVerbatimStrings && interpolatedStringExpressionParam.IsVerbatim()))
                {
                    return;
                }

                ICSharpExpression localizableExpression;
                Localizable localizable = LocalizableManager.IsLocalizable(
                    interpolatedStringExpressionParam,
                    out localizableExpression,
                    this.myCacheLocalizableItems,
                    this.myLocalizationRequiredAnnotationProvider);

                if (localizableExpression == null ||
                    !this.NeedToLocalizeElement(localizable) ||
                    this.LocalizeExpression(
                        localizableExpression,
                        consumer))
                {
                    return;
                }

                this.AddHighlighting(consumer, interpolatedStringExpressionParam);
            }

            private bool LocalizeExpression(
                ICSharpExpression expression,
                IHighlightingConsumer consumer)
            {
                if (this.IsProcessed(expression))
                {
                    return true;
                }

                bool flag = expression is IInterpolatedStringExpression;
                if (!flag && !expression.ConstantValue.IsString())
                {
                    return false;
                }

                ////bool flag = expression is IInterpolatedStringExpression;
                ////if ((!flag && !expression.ConstantValue.IsString()) ||
                ////    LocalizableAttributeProcess.Any(
                ////        expression,
                ////        (Predicate<ICSharpExpression>)(expression1 => this.myLiteralService.IsConstantLiteral(expression1))))
                ////{
                ////    return false;
                ////}

                if (flag)
                {
                    if (this.myDontAnalyseVerbatimStrings &&
                        LocalizableAttributeProcess.Any(
                            expression,
                            (Predicate<IInterpolatedStringExpression>)(item => item.IsVerbatim())))
                    {
                        return false;
                    }
                }

                if (this.myDontAnalyseVerbatimStrings &&
                         LocalizableAttributeProcess.Any(
                             expression,
                             (Predicate<ILiteralExpression>)(item => this.myLiteralService.IsVerbatimStringLiteral(item))))
                {
                    return false;
                }

                this.AddHighlighting(consumer, expression);
                return true;
            }

            private void AddHighlighting(
                [NotNull] IHighlightingConsumer consumer,
                [NotNull] ICSharpExpression expression)
            {
                lock (this.myProcessedItems)
                {
                    DocumentRange documentRange = expression.GetDocumentRange();
                    switch (expression)
                    {
                        case IInterpolatedStringExpression stringExpression:
                            ////if (documentRange.TextRange.IsEmpty ||
                            ////    !InterpolatedStringExtractUtil.CanExtractInterpolation(stringExpression))
                            ////{
                            ////    return;
                            ////}

                            ////RecursiveElementProcessor<ICSharpExpression> processor1 =
                            ////    new RecursiveElementProcessor<ICSharpExpression>(
                            ////        item => this.myProcessedItems.Add(item))
                            ////    {
                            ////        ProcessingIsFinishedHandler = JetFunc.False,
                            ////    };
                            ////expression.ProcessThisAndDescendants(processor1);
                            ////if (!expression.GetReferences<IResourceReference>().IsEmpty)
                            ////{
                            ////    return;
                            ////}

                            ////consumer.AddHighlighting(new LocalizableInterpolatedStringWarning(
                            ////    stringExpression,
                            ////    documentRange));
                            return;

                        case ICSharpLiteralExpression literalExpression:
                            documentRange =
                                this.File.GetDocumentRange(literalExpression.GetStringLiteralContentTreeRange());
                            break;
                    }

                    if (documentRange.TextRange.IsEmpty || string.IsNullOrEmpty(expression.ConstantValue.Value as string))
                    {
                        return;
                    }

                    RecursiveElementProcessor<ICSharpExpression> processor2
                        = new RecursiveElementProcessor<ICSharpExpression>(item => this.myProcessedItems.Add(item))
                        {
                            ProcessingIsFinishedHandler = JetFunc.False,
                        };

                    expression.ProcessThisAndDescendants(processor2);
                    if (!expression.GetReferences<IResourceReference>().IsEmpty)
                    {
                        return;
                    }

                    consumer.AddHighlighting(new LocalizableAttributeStringWarning(expression, documentRange));
                }
            }

            private bool IsProcessed([NotNull] ICSharpExpression expression)
            {
                lock (this.myProcessedItems)
                {
                    return this.myProcessedItems.Contains(expression);
                }
            }

            private bool NeedToLocalizeElement(Localizable localizable)
            {
                switch (this.myLocalizable)
                {
                    case Localizable.Yes:
                        return LocalizableAttributeProcess.NeedToLocalize(localizable, this.myInspector);
                    case Localizable.No:
                        return false;
                    default:
                        return localizable != Localizable.No && this.myHasAvailableResources && LocalizableAttributeProcess.NeedToLocalize(localizable, this.myInspector);
                }
            }

            private static bool NeedToLocalize(
                Localizable localizable,
                LocalizableInspector localizableInspector)
            {
                if (localizableInspector == LocalizableInspector.Optimistic)
                {
                    return localizable == Localizable.Yes;
                }

                return localizableInspector == LocalizableInspector.Pessimistic && localizable != Localizable.No;
            }

            private static bool Any<T>([NotNull] ITreeNode element, [NotNull] Predicate<T> predicate)
                where T : ITreeNode
            {
                bool result = false;

                RecursiveElementProcessor<T> processor
                    = new RecursiveElementProcessor<T>(item => result = predicate(item))
                    {
                        ProcessingIsFinishedHandler = () => result,
                    };

                element.ProcessThisAndDescendants(processor);
                return result;
            }

            private static bool CheckInterruption()
            {
#pragma warning disable CS0618 // Type or member is obsolete
                InterruptableActivityCookie.CheckAndThrow(null);
#pragma warning restore CS0618 // Type or member is obsolete
                return true;
            }
        }

        private class ProcessorBase : IRecursiveElementProcessor
        {
            [NotNull]
            private readonly IHighlightingConsumer myConsumer;

            [NotNull]
            private readonly CSharpDaemonStageProcessBase myProcess;

            protected ProcessorBase([NotNull] CSharpDaemonStageProcessBase process, [NotNull] IHighlightingConsumer consumer)
            {
                this.myProcess = process;
                this.myConsumer = consumer;
            }

            public bool ProcessingIsFinished
            {
                get { return this.myProcess.IsProcessingFinished(this.myConsumer); }
            }

            public virtual void ProcessBeforeInterior(ITreeNode element)
            {
                this.myProcess.ProcessBeforeInterior(element, this.myConsumer);
            }

            public virtual void ProcessAfterInterior(ITreeNode element)
            {
                this.myProcess.ProcessAfterInterior(element, this.myConsumer);
            }

            public virtual bool InteriorShouldBeProcessed(ITreeNode element)
            {
                return this.myProcess.InteriorShouldBeProcessed(element, this.myConsumer);
            }
        }

        private class LocalProcessor : ProcessorBase
        {
            public LocalProcessor([NotNull] CSharpDaemonStageProcessBase process, [NotNull] IHighlightingConsumer consumer)
                : base(process, consumer)
            {
            }
        }

        private class GlobalProcessor : ProcessorBase
        {
            public GlobalProcessor([NotNull] CSharpDaemonStageProcessBase process, [NotNull] IHighlightingConsumer consumer)
                : base(process, consumer)
            {
            }

            public override void ProcessBeforeInterior(ITreeNode element)
            {
                if (GlobalProcessor.IsTypeMemberDeclaration(element))
                {
                    return;
                }

                base.ProcessBeforeInterior(element);
            }

            public override void ProcessAfterInterior(ITreeNode element)
            {
                if (GlobalProcessor.IsTypeMemberDeclaration(element))
                {
                    return;
                }

                base.ProcessAfterInterior(element);
            }

            public override bool InteriorShouldBeProcessed(ITreeNode element)
            {
                return base.InteriorShouldBeProcessed(element) && !GlobalProcessor.IsTypeMemberDeclaration(element);
            }

            private static bool IsTypeMemberDeclaration(ITreeNode element)
            {
                switch (element)
                {
                    case IPropertyDeclaration _:
                    case IIndexerDeclaration _:
                    case IEventDeclaration _:
                        return true;
                    default:
                        return element is IFunctionDeclaration;
                }
            }
        }
    }
}