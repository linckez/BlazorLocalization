using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Structured result from visiting an IOperation argument value.
/// Adapter-internal — used by <see cref="CallInterpreter"/> for domain type production
/// and by the inspect command for diagnostic detail.
/// </summary>
internal abstract record OperationValue
{
    /// <summary>Compile-time constant (literal, const field, nameof result, folded expression).</summary>
    public sealed record Constant(object? Value, ITypeSymbol? Type) : OperationValue;

    /// <summary>Reference to a non-constant symbol (field, local, parameter).</summary>
    public sealed record SymbolReference(ISymbol Symbol, ITypeSymbol? Type) : OperationValue;

    /// <summary>Array initializer with per-element values.</summary>
    public sealed record ArrayElements(ImmutableArray<OperationValue> Items) : OperationValue;

    /// <summary>A nameof() expression that resolved to a string constant.</summary>
    public sealed record NameOf(string Name) : OperationValue;

    /// <summary>Anything else — the adapter couldn't classify it.</summary>
    public sealed record Unrecognized(OperationKind Kind, string Syntax) : OperationValue;

    /// <summary>
    /// Tries to extract a string value. Returns true for Constant(string) and NameOf.
    /// This is the adapter→domain bridge: if this returns true, the argument is literal.
    /// </summary>
    public bool TryGetString(out string? value)
    {
        switch (this)
        {
            case Constant { Value: string s }:
                value = s;
                return true;
            case NameOf { Name: var n }:
                value = n;
                return true;
            default:
                value = null;
                return false;
        }
    }

    /// <summary>
    /// Tries to extract an int value. Returns true for Constant(int).
    /// </summary>
    public bool TryGetInt(out int value)
    {
        if (this is Constant { Value: int i })
        {
            value = i;
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>Whether this value is a compile-time constant.</summary>
    public bool IsLiteral => this is Constant or NameOf;

    /// <summary>
    /// Human-readable display text for diagnostic output.
    /// Same pattern as <see cref="Domain.SourceFilePath.Display"/>.
    /// </summary>
    public string Display(int maxLength = 80) => this switch
    {
        Constant { Value: string s } => $"\"{Truncate(s, maxLength - 2)}\"",
        Constant { Value: var v } => v?.ToString() ?? "null",
        SymbolReference { Symbol: var s } => s.Name,
        NameOf { Name: var n } => $"nameof({n})",
        ArrayElements { Items: var items } => $"[{items.Length} elements]",
        Unrecognized { Syntax: var s } => Truncate(s, maxLength),
        _ => "?"
    };

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..(maxLength - 3)] + "..." : text;
}

/// <summary>
/// Extracts <see cref="OperationValue"/> from an <see cref="IOperation"/> using Roslyn's
/// visitor dispatch. Called via <c>operation.Accept(ValueExtractor.Instance, null)</c>.
/// </summary>
internal sealed class ValueExtractor : OperationVisitor<object?, OperationValue>
{
    public static readonly ValueExtractor Instance = new();

    public override OperationValue VisitLiteral(ILiteralOperation operation, object? argument)
        => new OperationValue.Constant(operation.ConstantValue.Value, operation.Type);

    public override OperationValue VisitFieldReference(IFieldReferenceOperation operation, object? argument)
    {
        // Enum members have constant values (0, 1, 2...) but we need the field NAME
        // for select/selectPlural interpretation. Preserve as SymbolReference.
        if (operation.Field.ContainingType?.TypeKind == TypeKind.Enum)
            return new OperationValue.SymbolReference(operation.Field, operation.Field.Type);

        return operation.ConstantValue is { HasValue: true } cv
            ? new OperationValue.Constant(cv.Value, operation.Field.Type)
            : new OperationValue.SymbolReference(operation.Field, operation.Field.Type);
    }

    public override OperationValue VisitLocalReference(ILocalReferenceOperation operation, object? argument)
        => operation.ConstantValue is { HasValue: true } cv
            ? new OperationValue.Constant(cv.Value, operation.Local.Type)
            : new OperationValue.SymbolReference(operation.Local, operation.Local.Type);

    public override OperationValue VisitParameterReference(IParameterReferenceOperation operation, object? argument)
        => new OperationValue.SymbolReference(operation.Parameter, operation.Parameter.Type);

    public override OperationValue VisitNameOf(INameOfOperation operation, object? argument)
        => operation.ConstantValue is { HasValue: true, Value: string name }
            ? new OperationValue.NameOf(name)
            : new OperationValue.Unrecognized(operation.Kind, operation.Syntax.ToString());

    public override OperationValue VisitBinaryOperator(IBinaryOperation operation, object? argument)
        => operation.ConstantValue is { HasValue: true } cv
            ? new OperationValue.Constant(cv.Value, operation.Type)
            : new OperationValue.Unrecognized(operation.Kind, operation.Syntax.ToString());

    public override OperationValue VisitParenthesized(IParenthesizedOperation operation, object? argument)
        => operation.Operand.Accept(this, argument)!;

    public override OperationValue VisitConditional(IConditionalOperation operation, object? argument)
        => operation.ConstantValue is { HasValue: true } cv
            ? new OperationValue.Constant(cv.Value, operation.Type)
            : new OperationValue.Unrecognized(operation.Kind, operation.Syntax.ToString());

    public override OperationValue VisitInterpolatedString(IInterpolatedStringOperation operation, object? argument)
        => operation.ConstantValue is { HasValue: true } cv
            ? new OperationValue.Constant(cv.Value, operation.Type)
            : new OperationValue.Unrecognized(operation.Kind, operation.Syntax.ToString());

    public override OperationValue VisitDefaultValue(IDefaultValueOperation operation, object? argument)
        => operation.ConstantValue is { HasValue: true } cv
            ? new OperationValue.Constant(cv.Value, operation.Type)
            : new OperationValue.Unrecognized(operation.Kind, operation.Syntax.ToString());

    public override OperationValue VisitConversion(IConversionOperation operation, object? argument)
        => operation.Operand.Accept(this, argument)!;

    public override OperationValue VisitArrayCreation(IArrayCreationOperation operation, object? argument)
    {
        if (operation.Initializer is not { } init)
            return new OperationValue.Unrecognized(operation.Kind, operation.Syntax.ToString());

        var items = init.ElementValues
            .Select(e => e.Accept(this, argument)!)
            .ToImmutableArray();
        return new OperationValue.ArrayElements(items);
    }

    public override OperationValue DefaultVisit(IOperation operation, object? argument)
        => new OperationValue.Unrecognized(operation.Kind, operation.Syntax.ToString());
}
