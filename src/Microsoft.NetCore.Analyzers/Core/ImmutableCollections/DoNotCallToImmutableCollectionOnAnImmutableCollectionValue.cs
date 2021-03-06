// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.ImmutableCollections
{
    /// <summary>
    /// RS0012: Do not call ToImmutableCollection on an ImmutableCollection value
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer : DiagnosticAnalyzer
    {
        private const string ImmutableArrayMetadataName = "System.Collections.Immutable.ImmutableArray`1";
        internal const string RuleId = "RS0012";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(SystemCollectionsImmutableAnalyzersResources.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueTitle), SystemCollectionsImmutableAnalyzersResources.ResourceManager, typeof(SystemCollectionsImmutableAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(SystemCollectionsImmutableAnalyzersResources.DoNotCallToImmutableCollectionOnAnImmutableCollectionValueMessage), SystemCollectionsImmutableAnalyzersResources.ResourceManager, typeof(SystemCollectionsImmutableAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Reliability,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             helpLinkUri: null,
                                                                             customTags: WellKnownDiagnosticTags.Telemetry);

        private static readonly ImmutableDictionary<string, string> ImmutableCollectionMetadataNames = new Dictionary<string, string>
        {
            ["ToImmutableArray"] = "System.Collections.Immutable.ImmutableArray`1",
            ["ToImmutableDictionary"] = "System.Collections.Immutable.ImmutableDictionary`2",
            ["ToImmutableHashSet"] = "System.Collections.Immutable.ImmutableHashSet`1",
            ["ToImmutableList"] = "System.Collections.Immutable.ImmutableList`1",
            ["ToImmutableSortedDictionary"] = "System.Collections.Immutable.ImmutableSortedDictionary`2",
            ["ToImmutableSortedSet"] = "System.Collections.Immutable.ImmutableSortedSet`1",
        }.ToImmutableDictionary();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var compilation = compilationStartContext.Compilation;
                if (compilation.GetTypeByMetadataName(ImmutableArrayMetadataName) == null)
                {
                    return;
                }

                compilationStartContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    var targetMethod = invocation.TargetMethod;
                    if (targetMethod == null || !ImmutableCollectionMetadataNames.TryGetValue(targetMethod.Name, out string metadataName))
                    {
                        return;
                    }

                    Debug.Assert(!string.IsNullOrEmpty(metadataName));
                    var immutableCollectionType = compilation.GetTypeByMetadataName(metadataName);
                    if (immutableCollectionType == null)
                    {
                        // The user might be running against a custom system assembly that defines ImmutableArray,
                        // but not other immutable collection types.
                        return;
                    }

                    var receiverType = invocation.GetReceiverType(operationContext.Compilation, beforeConversion: true, cancellationToken: operationContext.CancellationToken);
                    if (receiverType != null &&
                        receiverType.DerivesFromOrImplementsAnyConstructionOf(immutableCollectionType))
                    {
                        operationContext.ReportDiagnostic(Diagnostic.Create(
                            Rule,
                            invocation.Syntax.GetLocation(),
                            targetMethod.Name,
                            immutableCollectionType.Name));
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}
