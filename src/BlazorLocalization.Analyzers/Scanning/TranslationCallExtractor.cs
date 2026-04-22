using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazorLocalization.Analyzers.Scanning;

/// <summary>
/// Extracts key and message string literals from Translation()/GetString()/indexer call syntax nodes.
/// All invocation methods require an <see cref="IMethodSymbol"/> so arguments are resolved
/// by parameter name — never by positional guessing.
/// </summary>
internal static class TranslationCallExtractor
{
    /// <summary>
    /// Tries to extract the key string literal from an invocation (Translation/GetString).
    /// Uses the resolved <paramref name="method"/> to find the parameter named <c>key</c>
    /// (or <c>name</c> for GetString), then matches the corresponding argument.
    /// Returns false for variables, interpolated strings, or missing arguments.
    /// </summary>
    public static bool TryGetKeyFromInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out string? key,
        out Location? keyLocation)
    {
        key = null;
        keyLocation = null;

        // Find the parameter named "key" or "name" (GetString uses "name")
        var keyArg = FindArgumentForParameter(invocation.ArgumentList.Arguments, method.Parameters, BlazorLocalizationSymbols.KeyParameterName)
                     ?? FindArgumentForParameter(invocation.ArgumentList.Arguments, method.Parameters, MicrosoftLocalizationSymbols.NameParameterName);

        if (keyArg is null)
            return false;

        if (keyArg.Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            return false;

        key = literal.Token.ValueText;
        keyLocation = keyArg.GetLocation();
        return true;
    }

    /// <summary>
    /// Tries to extract the key string literal from an element access (indexer: Loc["key"]).
    /// Indexers are always positional — the first bracketed argument is the key.
    /// </summary>
    public static bool TryGetKeyFromElementAccess(
        ElementAccessExpressionSyntax elementAccess,
        out string? key,
        out Location? keyLocation)
    {
        key = null;
        keyLocation = null;

        var arguments = elementAccess.ArgumentList.Arguments;
        if (arguments.Count < 1)
            return false;

        // Indexers are always positional — first argument is the key
        var firstArg = arguments[0];
        if (firstArg.Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            return false;

        key = literal.Token.ValueText;
        keyLocation = firstArg.GetLocation();
        return true;
    }

    /// <summary>
    /// Returns true if the GetString invocation has format arguments beyond the name parameter.
    /// Uses the resolved <paramref name="method"/> to count parameters that are not the key/name.
    /// </summary>
    public static bool HasExtraArguments(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        // Count arguments that resolve to parameters other than "name" / "key"
        var arguments = invocation.ArgumentList.Arguments;
        foreach (var arg in arguments)
        {
            var param = ResolveParameter(arg, method.Parameters, arguments);
            if (param is not null && param.Name is not MicrosoftLocalizationSymbols.NameParameterName and not BlazorLocalizationSymbols.KeyParameterName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the element access has additional arguments beyond the first.
    /// Indexers are always positional.
    /// </summary>
    public static bool HasExtraArguments(ElementAccessExpressionSyntax elementAccess)
        => elementAccess.ArgumentList.Arguments.Count > 1;

    /// <summary>
    /// Tries to extract the message string literal from a Translation() invocation.
    /// Uses the resolved <paramref name="method"/> to find the parameter named <c>message</c>.
    /// Returns false for non-literal or missing message arguments.
    /// </summary>
    public static bool TryGetMessageFromInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out string? message)
    {
        message = null;

        var messageArg = FindArgumentForParameter(invocation.ArgumentList.Arguments, method.Parameters, BlazorLocalizationSymbols.MessageParameterName);
        if (messageArg is null)
            return false;

        if (messageArg.Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            return false;

        message = literal.Token.ValueText;
        return true;
    }

    /// <summary>
    /// Finds the argument that corresponds to the parameter with the given <paramref name="parameterName"/>
    /// in the resolved <paramref name="parameters"/> list.
    /// Checks named arguments (<c>name:</c>) first, then falls back to positional matching
    /// against the method signature.
    /// </summary>
    internal static ArgumentSyntax? FindArgumentForParameter(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        ImmutableArray<IParameterSymbol> parameters,
        string parameterName)
    {
        // 1. Find the parameter index in the method signature
        var parameterIndex = -1;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == parameterName)
            {
                parameterIndex = i;
                break;
            }
        }

        if (parameterIndex < 0)
            return null; // Method doesn't have this parameter at all

        // 2. Check if any argument explicitly names this parameter
        foreach (var arg in arguments)
        {
            if (arg.NameColon?.Name.Identifier.ValueText == parameterName)
                return arg;
        }

        // 3. Positional fallback: the Nth non-named argument maps to the Nth parameter
        var positionalIndex = 0;
        foreach (var arg in arguments)
        {
            if (arg.NameColon is not null)
                continue; // Named args don't consume positional slots

            if (positionalIndex == parameterIndex)
                return arg;

            positionalIndex++;
        }

        return null;
    }

    /// <summary>
    /// Resolves which parameter a specific argument maps to.
    /// Named arguments match by name; positional arguments match by position in the signature.
    /// </summary>
    private static IParameterSymbol? ResolveParameter(
        ArgumentSyntax argument,
        ImmutableArray<IParameterSymbol> parameters,
        SeparatedSyntaxList<ArgumentSyntax> allArguments)
    {
        // Named argument — match by name
        var namedValue = argument.NameColon?.Name.Identifier.ValueText;
        if (namedValue is not null)
        {
            foreach (var param in parameters)
            {
                if (param.Name == namedValue)
                    return param;
            }

            return null;
        }

        // Positional — count how many non-named args precede this one
        var positionalIndex = 0;
        foreach (var arg in allArguments)
        {
            if (arg == argument)
                break;

            if (arg.NameColon is null)
                positionalIndex++;
        }

        return positionalIndex < parameters.Length ? parameters[positionalIndex] : null;
    }
}
