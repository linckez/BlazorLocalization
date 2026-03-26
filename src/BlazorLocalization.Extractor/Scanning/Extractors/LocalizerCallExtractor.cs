using System.Collections.Immutable;
using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Scanning.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StringLocalizerExtensions = BlazorLocalization.Extensions.StringLocalizerExtensions;

namespace BlazorLocalization.Extractor.Scanning.Extractors;

/// <summary>
/// Detects <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/> method calls
/// (<c>Translate()</c>, <c>GetString()</c>, etc.) and indexer access (<c>localizer["key"]</c>),
/// producing both an <see cref="ExtractedCall"/> and an optional <see cref="TranslationEntry"/>.
/// </summary>
internal static class LocalizerCallExtractor
{
	public static (ExtractedCall Call, TranslationEntry? Entry)? TryExtractInvocation(
		InvocationExpressionSyntax invocation,
		SemanticModel semanticModel,
		SourceOrigin origin,
		BuilderSymbolTable symbols)
	{
		var receiverType = GetReceiverType(invocation, semanticModel);
		if (!IsStringLocalizerType(receiverType))
			return null;

		var symbolInfo = semanticModel.GetSymbolInfo(invocation);
		var methodSymbol = symbolInfo.Symbol as IMethodSymbol
			?? BestCandidateByArgCount(symbolInfo, invocation.ArgumentList.Arguments.Count);

		if (methodSymbol is null)
			return null;

		// When BestCandidateByArgCount returns an unreduced extension method,
		// parameters[0] is the 'this' parameter — skip it so args align correctly.
		var parameters = methodSymbol is { IsExtensionMethod: true, ReducedFrom: null }
			? methodSymbol.Parameters.RemoveAt(0)
			: methodSymbol.Parameters;

		var isTranslate = methodSymbol.Name == nameof(StringLocalizerExtensions.Translation);

		var fluentChain = isTranslate
			? CollectFluentChain(invocation, semanticModel)
			: null;

		var call = new ExtractedCall(
			methodSymbol.ContainingType.Name,
			methodSymbol.Name,
			CallKind.MethodInvocation,
			origin.ResolveLocation(invocation),
			ResolveOverloadStatus(symbolInfo),
			ExtractArguments(invocation.ArgumentList.Arguments, parameters, semanticModel),
			fluentChain?.Select(c => new ChainedMethodCall(c.MethodName, c.Arguments)).ToList());

		TranslationEntry? entry = null;
		if (isTranslate)
			entry = ChainInterpreter.InterpretTranslateCall(call, methodSymbol, fluentChain, symbols);

		return (call, entry);
	}

	public static (ExtractedCall Call, TranslationEntry? Entry)? TryExtractIndexer(
		ElementAccessExpressionSyntax elementAccess,
		SemanticModel semanticModel,
		SourceOrigin origin)
	{
		var expressionType = semanticModel.GetTypeInfo(elementAccess.Expression).Type;
		if (!IsStringLocalizerType(expressionType))
			return null;

		var symbolInfo = semanticModel.GetSymbolInfo(elementAccess);
		var indexerSymbol = symbolInfo.Symbol as IPropertySymbol;
		var parameters = indexerSymbol?.Parameters ?? [];

		var typeName = expressionType?.Name ?? "<unknown>";

		var call = new ExtractedCall(
			typeName,
			"this[]",
			CallKind.IndexerAccess,
			origin.ResolveLocation(elementAccess),
			ResolveOverloadStatus(symbolInfo),
			ExtractArguments(elementAccess.ArgumentList.Arguments, parameters, semanticModel));

		var key = call.Arguments.FirstOrDefault()?.Value;
		var entry = key is not null ? new TranslationEntry(key, null, ChainInterpreter.MakeSource(call)) : null;

		return (call, entry);
	}

	/// <summary>
	/// Walks up the syntax tree from a <c>Translate()</c> invocation to capture all
	/// fluent builder chain calls, preserving the Roslyn <see cref="IMethodSymbol"/>
	/// for identity-based dispatch.
	/// </summary>
	internal static IReadOnlyList<ResolvedChainLink>? CollectFluentChain(
		InvocationExpressionSyntax translateInvocation,
		SemanticModel semanticModel)
	{
		List<ResolvedChainLink>? chain = null;
		SyntaxNode current = translateInvocation;

		while (current.Parent is MemberAccessExpressionSyntax memberAccess
		       && memberAccess.Parent is InvocationExpressionSyntax nextInvocation)
		{
			var methodName = memberAccess.Name.Identifier.Text;

			// ToString() is a terminator, not part of the fluent chain
			if (methodName == "ToString")
				break;

			var methodSymbol = semanticModel.GetSymbolInfo(nextInvocation).Symbol as IMethodSymbol;
			var parameters = methodSymbol?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;
			var args = ExtractArguments(nextInvocation.ArgumentList.Arguments, parameters, semanticModel);

			chain ??= [];
			chain.Add(new ResolvedChainLink(methodName, args, methodSymbol));

			current = nextInvocation;
		}

		return chain;
	}

	private static ITypeSymbol? GetReceiverType(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
		invocation.Expression is MemberAccessExpressionSyntax memberAccess
			? semanticModel.GetTypeInfo(memberAccess.Expression).Type
			: null;

	private static bool IsStringLocalizerType(ITypeSymbol? type)
	{
		if (type is null) return false;
		if (IsStringLocalizerInterface(type)) return true;
		return type.AllInterfaces.Any(IsStringLocalizerInterface);
	}

	private static bool IsStringLocalizerInterface(ITypeSymbol type) =>
		type is INamedTypeSymbol { Name: "IStringLocalizer", ContainingNamespace: { Name: "Localization", ContainingNamespace: { Name: "Extensions", ContainingNamespace: { Name: "Microsoft" } } } };

	// ── Argument extraction (absorbed from RoslynHelpers) ──────────────

	private static IMethodSymbol? BestCandidateByArgCount(SymbolInfo symbolInfo, int argCount) =>
		symbolInfo.CandidateSymbols
			.OfType<IMethodSymbol>()
			.OrderBy(m => Math.Abs(EffectiveParameterCount(m) - argCount))
			.FirstOrDefault();

	private static int EffectiveParameterCount(IMethodSymbol method) =>
		method.ReducedFrom is not null
			? method.Parameters.Length
			: method.IsExtensionMethod
				? method.Parameters.Length - 1
				: method.Parameters.Length;

	private static OverloadResolutionStatus ResolveOverloadStatus(SymbolInfo symbolInfo)
	{
		if (symbolInfo.Symbol is not null)
			return OverloadResolutionStatus.Resolved;

		return symbolInfo.CandidateReason == CandidateReason.Ambiguous
			? OverloadResolutionStatus.Ambiguous
			: OverloadResolutionStatus.BestCandidate;
	}

	private static IReadOnlyList<ResolvedArgument> ExtractArguments(
		SeparatedSyntaxList<ArgumentSyntax> args,
		ImmutableArray<IParameterSymbol> parameters,
		SemanticModel semanticModel)
	{
		var result = new List<ResolvedArgument>(args.Count);

		for (var i = 0; i < args.Count; i++)
		{
			var arg = args[i];
			var syntaxName = arg.NameColon?.Name.Identifier.Text;
			var literalValue = TryGetStringLiteral(arg.Expression);
			var parameter = ResolveParameter(parameters, arg, i);
			var objectCreation = ExtractObjectCreation(arg.Expression, semanticModel);

			result.Add(new ResolvedArgument(
				i,
				literalValue ?? arg.Expression.ToString(),
				literalValue is not null,
				syntaxName,
				parameter?.Name,
				objectCreation));
		}

		return result;
	}

	private static ObjectCreation? ExtractObjectCreation(ExpressionSyntax expression, SemanticModel semanticModel) =>
		expression switch
		{
			ObjectCreationExpressionSyntax explicit_ => ExtractObjectCreationCore(explicit_, explicit_.ArgumentList?.Arguments, semanticModel),
			ImplicitObjectCreationExpressionSyntax implicit_ => ExtractObjectCreationCore(implicit_, implicit_.ArgumentList?.Arguments, semanticModel),
			_ => null
		};

	private static ObjectCreation? ExtractObjectCreationCore(
		ExpressionSyntax creationExpression,
		SeparatedSyntaxList<ArgumentSyntax>? args,
		SemanticModel semanticModel)
	{
		var ctorSymbol = semanticModel.GetSymbolInfo(creationExpression).Symbol as IMethodSymbol;
		var createdType = semanticModel.GetTypeInfo(creationExpression).Type
			?? semanticModel.GetTypeInfo(creationExpression).ConvertedType;

		var typeName = ctorSymbol?.ContainingType.ToDisplayString()
			?? createdType?.ToDisplayString()
			?? "<unknown type>";

		var ctorArgs = args ?? default;
		var ctorArguments = new List<ResolvedArgument>(ctorArgs.Count);

		for (var i = 0; i < ctorArgs.Count; i++)
		{
			var arg = ctorArgs[i];
			var syntaxName = arg.NameColon?.Name.Identifier.Text;
			var literalValue = TryGetStringLiteral(arg.Expression);
			var parameter = ctorSymbol is not null ? ResolveParameter(ctorSymbol.Parameters, arg, i) : null;

			ctorArguments.Add(new ResolvedArgument(
				i,
				literalValue ?? arg.Expression.ToString(),
				literalValue is not null,
				syntaxName,
				parameter?.Name));
		}

		return new ObjectCreation(typeName, ctorArguments);
	}

	private static IParameterSymbol? ResolveParameter(ImmutableArray<IParameterSymbol> parameters, ArgumentSyntax arg, int position)
	{
		var named = arg.NameColon?.Name.Identifier.Text;
		if (!string.IsNullOrWhiteSpace(named))
			return parameters.FirstOrDefault(p => p.Name == named);

		return position < parameters.Length ? parameters[position] : null;
	}

	private static string? TryGetStringLiteral(ExpressionSyntax expression) =>
		expression switch
		{
			LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression)
				=> lit.Token.ValueText,
			BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression)
				=> TryGetStringLiteral(bin.Left) is { } left && TryGetStringLiteral(bin.Right) is { } right
					? left + right
					: null,
			_ => null
		};
}
