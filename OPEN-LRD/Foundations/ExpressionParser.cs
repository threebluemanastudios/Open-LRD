using System;
using System.Collections.Generic;
using System.Globalization;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;

namespace OPEN_LRD.Foundations
{
    internal static class ExpressionParser
    {
        internal const int MaximumInputLength = 16_384;
        internal const int MaximumNestingDepth = 256;
        internal const int MaximumFunctionArguments = 32;
        internal const int MaximumAstNodes = 4_096;
        internal const int MaximumAstDepth = 256;
        internal const int MaximumIdentifierLength = 128;
        internal const int MaximumNumericLiteralLength = 512;
        private const int MaximumIndexedIterations = 1_000;

        internal static void ValidateVariableIdentifier(string variable, string parameterName = "variable")
        {
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentException("The variable name cannot be null or empty.", parameterName);
            }
            string normalized = variable.Trim();
            if (normalized.Length > MaximumIdentifierLength)
            {
                throw new FormatException($"Identifier '{normalized[..Math.Min(16, normalized.Length)]}...' exceeds the {MaximumIdentifierLength} character limit.");
            }
        }

        internal static Expression Parse(string input)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (input.Length > MaximumInputLength)
            {
                throw new FormatException($"Expression input exceeds the {MaximumInputLength} character limit.");
            }
            Parser parser = new Parser(input);
            Expression expression = parser.ParseExpression();
            parser.SkipWhitespace();
            if (!parser.IsAtEnd)
            {
                throw parser.Error($"Unexpected character '{parser.CurrentCharacter}'", parser.Position);
            }
            ValidateExpressionLimits(expression);
            return expression;
        }

        internal static void ValidateExpressionLimits(Expression expression)
        {
            ArgumentNullException.ThrowIfNull(expression);
            int nodes = 0;
            int maxDepth = 0;
            Stack<(Expression Expression, int Depth)> stack = new Stack<(Expression, int)>();
            stack.Push((expression, 1));
            while (stack.Count > 0)
            {
                (Expression current, int depth) = stack.Pop();
                nodes++;
                if (nodes > MaximumAstNodes)
                {
                    throw new FormatException($"Expression AST exceeds the {MaximumAstNodes} node limit.");
                }
                if (depth > maxDepth)
                {
                    maxDepth = depth;
                    if (maxDepth > MaximumAstDepth)
                    {
                        throw new FormatException($"Expression AST exceeds the {MaximumAstDepth} depth limit.");
                    }
                }
                switch (current)
                {
                    case VariableExpression variable:
                        if (variable.Name.Length > MaximumIdentifierLength)
                        {
                            throw new FormatException($"Variable identifier exceeds the {MaximumIdentifierLength} character limit.");
                        }
                        break;
                    case NegateExpression negate:
                        stack.Push((negate.Operand, depth + 1));
                        break;
                    case BinaryExpression binary:
                        stack.Push((binary.Right, depth + 1));
                        stack.Push((binary.Left, depth + 1));
                        break;
                    case FunctionExpression function:
                        stack.Push((function.Argument, depth + 1));
                        break;
                    case CallExpression call:
                        if (call.FunctionName.Length > MaximumIdentifierLength)
                        {
                            throw new FormatException($"Call identifier exceeds the {MaximumIdentifierLength} character limit.");
                        }
                        if (call.Arguments.Count > MaximumFunctionArguments)
                        {
                            throw new FormatException($"Function call exceeds the {MaximumFunctionArguments} argument limit.");
                        }
                        for (int i = call.Arguments.Count - 1; i >= 0; i--)
                        {
                            stack.Push((call.Arguments[i], depth + 1));
                        }
                        break;
                    case ConditionalExpression conditional:
                        stack.Push((conditional.WhenFalse, depth + 1));
                        stack.Push((conditional.WhenTrue, depth + 1));
                        stack.Push((conditional.Condition, depth + 1));
                        break;
                }
            }
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;
            private int recursionDepth;
            private int absBarDepth;

            internal Parser(string text)
            {
                this.text = text;
                index = 0;
            }

            internal int Position => index;

            internal char CurrentCharacter => index < text.Length ? text[index] : '\0';

            internal bool IsAtEnd => index >= text.Length;

            internal FormatException Error(string message, int position)
            {
                int line = 1;
                int column = 1;
                for (int offset = 0; offset < position && offset < text.Length; offset++)
                {
                    if (text[offset] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }

                int lineStart = Math.Min(position, text.Length);
                while (lineStart > 0 && text[lineStart - 1] != '\n')
                {
                    lineStart--;
                }
                int lineEnd = Math.Min(position, text.Length);
                while (lineEnd < text.Length && text[lineEnd] != '\n')
                {
                    lineEnd++;
                }
                if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
                {
                    lineEnd--;
                }
                string snippet = lineEnd > lineStart ? text.Substring(lineStart, lineEnd - lineStart) : string.Empty;
                return new FormatException($"{message} (line {line}, column {column}): {snippet}{Environment.NewLine}{new string(' ', Math.Max(0, column - 1))}^");
            }

            internal void SkipWhitespace()
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                {
                    index++;
                }
            }

            internal Expression ParseExpression()
            {
                return ParseConditional();
            }

            private Expression ParseConditional()
            {
                Expression condition = ParseLogicalOr();
                SkipWhitespace();
                if (!Match('?'))
                {
                    return condition;
                }
                Expression whenTrue = ParseNested(ParseExpression);
                Expect(':');
                Expression whenFalse = ParseNested(ParseExpression);
                return ExpressionBuilder.Conditional(condition, whenTrue, whenFalse);
            }

            private Expression ParseLogicalOr()
            {
                Expression left = ParseLogicalAnd();
                while (true)
                {
                    SkipWhitespace();
                    if (!StartsOperator("||"))
                    {
                        return left;
                    }
                    index += 2;
                    left = ExpressionBuilder.Or(left, ParseNested(ParseLogicalAnd));
                }
            }

            private Expression ParseLogicalAnd()
            {
                Expression left = ParseEquality();
                while (true)
                {
                    SkipWhitespace();
                    if (!StartsOperator("&&"))
                    {
                        return left;
                    }
                    index += 2;
                    left = ExpressionBuilder.And(left, ParseNested(ParseEquality));
                }
            }

            private Expression ParseEquality()
            {
                Expression left = ParseRelational();
                while (true)
                {
                    SkipWhitespace();
                    if (StartsOperator("=="))
                    {
                        index += 2;
                        left = ExpressionBuilder.Equal(left, ParseNested(ParseRelational));
                    }
                    else if (StartsOperator("!="))
                    {
                        index += 2;
                        left = ExpressionBuilder.NotEqual(left, ParseNested(ParseRelational));
                    }
                    else
                    {
                        return left;
                    }
                }
            }

            private Expression ParseRelational()
            {
                Expression left = ParseAdditive();
                while (true)
                {
                    SkipWhitespace();
                    if (StartsOperator("<="))
                    {
                        index += 2;
                        left = ExpressionBuilder.LessThanOrEqual(left, ParseNested(ParseAdditive));
                    }
                    else if (StartsOperator(">="))
                    {
                        index += 2;
                        left = ExpressionBuilder.GreaterThanOrEqual(left, ParseNested(ParseAdditive));
                    }
                    else if (Match('<'))
                    {
                        left = ExpressionBuilder.LessThan(left, ParseNested(ParseAdditive));
                    }
                    else if (Match('>'))
                    {
                        left = ExpressionBuilder.GreaterThan(left, ParseNested(ParseAdditive));
                    }
                    else
                    {
                        return left;
                    }
                }
            }

            private Expression ParseAdditive()
            {
                Expression left = ParseTerm();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('+'))
                    {
                        left = ExpressionBuilder.Add(left, ParseTerm());
                    }
                    else if (Match('-'))
                    {
                        left = ExpressionBuilder.Subtract(left, ParseTerm());
                    }
                    else
                    {
                        return left;
                    }
                }
            }

            private Expression ParseTerm()
            {
                Expression left = ParseUnary();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('*'))
                    {
                        left = ExpressionBuilder.Multiply(left, ParseUnary());
                    }
                    else if (Match('/'))
                    {
                        left = ExpressionBuilder.Divide(left, ParseUnary());
                    }
                    else if (Match('%'))
                    {
                        left = ExpressionBuilder.Call("mod", left, ParseUnary());
                    }
                    else if (!StartsInKeyword() && StartsImplicitFactor())
                    {
                        left = ExpressionBuilder.Multiply(left, ParseUnary());
                    }
                    else
                    {
                        return left;
                    }
                }
            }

            private Expression ParseUnary()
            {
                SkipWhitespace();
                if (Match('+'))
                {
                    return ParseNested(ParseUnary);
                }
                if (Match('-'))
                {
                    return ExpressionBuilder.Negate(ParseNested(ParseUnary));
                }
                if (CurrentCharacter == '!' && !StartsOperator("!="))
                {
                    index++;
                    return ExpressionBuilder.Not(ParseNested(ParseUnary));
                }
                return ParsePower();
            }

            private Expression ParsePower()
            {
                Expression left = ParsePostfix();
                SkipWhitespace();
                if (Match('^'))
                {
                    return ExpressionBuilder.Power(left, ParseNested(ParseUnary));
                }
                return left;
            }

            private Expression ParsePostfix()
            {
                Expression left = ParsePrimary();
                while (true)
                {
                    SkipWhitespace();
                    if (CurrentCharacter != '!' || StartsOperator("!="))
                    {
                        return left;
                    }
                    index++;
                    left = ExpressionBuilder.Function(FunctionKind.Factorial, left);
                }
            }

            private Expression ParsePrimary()
            {
                SkipWhitespace();
                if (IsAtEnd)
                {
                    throw Error("Expected an expression", index);
                }
                char current = CurrentCharacter;
                if (current == '(')
                {
                    index++;
                    Expression inside = ParseNested(ParseExpression);
                    Expect(')');
                    return inside;
                }
                if (current == '|')
                {
                    index++;
                    absBarDepth++;
                    try
                    {
                        Expression inside = ParseNested(ParseExpression);
                        Expect('|');
                        return ExpressionBuilder.Function(FunctionKind.Abs, inside);
                    }
                    finally
                    {
                        absBarDepth--;
                    }
                }
                if (IsAsciiDigit(current) || current == '.')
                {
                    return ParseNumber();
                }
                if (IsAsciiLetter(current) || current == '_')
                {
                    Expression? letExpression = TryParseLetExpression();
                    return letExpression ?? ParseIdentifier();
                }
                throw Error($"Unexpected character '{current}'", index);
            }

            private Expression ParseNumber()
            {
                int start = index;
                if (CurrentCharacter == '0' && index + 1 < text.Length && (text[index + 1] == 'x' || text[index + 1] == 'X'))
                {
                    index += 2;
                    double radixValue = ParseRadixDigits(16, start);
                    return ExpressionBuilder.Constant(radixValue);
                }
                if (CurrentCharacter == '0' && index + 1 < text.Length && (text[index + 1] == 'b' || text[index + 1] == 'B'))
                {
                    index += 2;
                    double radixValue = ParseRadixDigits(2, start);
                    return ExpressionBuilder.Constant(radixValue);
                }

                bool hasDigits = ReadDecimalDigits(out _);
                if (index < text.Length && text[index] == '.')
                {
                    index++;
                    hasDigits |= ReadDecimalDigits(out _);
                }
                if (!hasDigits)
                {
                    throw Error("Invalid number", start);
                }
                if (index < text.Length && text[index] == '.')
                {
                    throw Error("A numeric literal cannot contain multiple decimal points", index);
                }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    int exponentStart = index;
                    index++;
                    if (index < text.Length && (text[index] == '+' || text[index] == '-'))
                    {
                        index++;
                    }
                    if (!ReadDecimalDigits(out _))
                    {
                        throw Error("Expected exponent digits", exponentStart + 1);
                    }
                }
                if (index - start > MaximumNumericLiteralLength)
                {
                    throw Error($"Numeric literal exceeds the {MaximumNumericLiteralLength} character limit", start);
                }
                string token = text.Substring(start, index - start).Replace("_", string.Empty);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                {
                    throw Error($"Invalid number '{token}'", start);
                }
                return ExpressionBuilder.Constant(value);
            }

            private double ParseRadixDigits(int radix, int start)
            {
                bool hasDigits = false;
                ulong value = 0;
                while (index < text.Length)
                {
                    if (index - start > MaximumNumericLiteralLength)
                    {
                        throw Error($"Numeric literal exceeds the {MaximumNumericLiteralLength} character limit", start);
                    }
                    int digit = GetDigit(text[index], radix);
                    if (digit >= 0)
                    {
                        try
                        {
                            value = checked((value * (ulong)radix) + (ulong)digit);
                        }
                        catch (OverflowException)
                        {
                            throw Error($"Integer literal at position {start} is too large", start);
                        }
                        hasDigits = true;
                        index++;
                        continue;
                    }
                    if (text[index] == '_')
                    {
                        if (!hasDigits || index + 1 >= text.Length || GetDigit(text[index + 1], radix) < 0)
                        {
                            throw Error("Invalid digit separator in integer literal", index);
                        }
                        index++;
                        continue;
                    }
                    break;
                }
                if (!hasDigits)
                {
                    throw Error("Invalid integer literal", start);
                }
                return value;
            }

            private bool ReadDecimalDigits(out bool hasDigits)
            {
                hasDigits = false;
                bool previousWasDigit = false;
                while (index < text.Length)
                {
                    if (IsAsciiDigit(text[index]))
                    {
                        hasDigits = true;
                        previousWasDigit = true;
                        index++;
                        continue;
                    }
                    if (text[index] == '_')
                    {
                        if (!previousWasDigit || index + 1 >= text.Length || !IsAsciiDigit(text[index + 1]))
                        {
                            throw Error("Invalid digit separator in numeric literal", index);
                        }
                        previousWasDigit = false;
                        index++;
                        continue;
                    }
                    break;
                }
                return hasDigits;
            }

            private static int GetDigit(char value, int radix)
            {
                int digit = value switch
                {
                    >= '0' and <= '9' => value - '0',
                    >= 'a' and <= 'f' => value - 'a' + 10,
                    >= 'A' and <= 'F' => value - 'A' + 10,
                    _ => -1
                };
                return digit >= 0 && digit < radix ? digit : -1;
            }

            private Expression? TryParseLetExpression()
            {
                int start = index;
                if (!TryReadKeyword("let"))
                {
                    return null;
                }
                SkipWhitespace();
                if (CurrentCharacter == '(')
                {
                    return ParseFunctionStyleLet(start);
                }
                if (!TryParseLetBinding(out string firstName, out Expression firstValue))
                {
                    index = start;
                    return null;
                }

                List<(string Name, Expression Value)> bindings = new List<(string, Expression)>
                {
                    (firstName, firstValue)
                };
                while (true)
                {
                    SkipWhitespace();
                    if (TryReadKeyword("in"))
                    {
                        Expression body = ParseNested(ParseExpression);
                        return BuildLet(bindings, body);
                    }
                    Expect(',');
                    if (!TryParseLetBinding(out string name, out Expression value))
                    {
                        throw Error("Expected a let binding", index);
                    }
                    bindings.Add((name, value));
                }
            }

            private Expression ParseFunctionStyleLet(int start)
            {
                Expect('(');
                List<(string Name, Expression Value)> bindings = new List<(string, Expression)>();
                while (true)
                {
                    SkipWhitespace();
                    if (!TryParseLetBinding(out string name, out Expression value))
                    {
                        if (bindings.Count == 0)
                        {
                            throw Error("let requires at least one binding", start);
                        }
                        Expression body = ParseNested(ParseExpression);
                        Expect(')');
                        return BuildLet(bindings, body);
                    }
                    bindings.Add((name, value));
                    SkipWhitespace();
                    if (!Match(','))
                    {
                        throw Error("Expected a let body after the binding list", index);
                    }
                }
            }

            private bool TryParseLetBinding(out string name, out Expression value)
            {
                int start = index;
                SkipWhitespace();
                if (!IsAsciiLetter(CurrentCharacter) && CurrentCharacter != '_')
                {
                    name = string.Empty;
                    value = null!;
                    index = start;
                    return false;
                }
                name = ReadIdentifierPart();
                SkipWhitespace();
                if (CurrentCharacter != '=' || (index + 1 < text.Length && text[index + 1] == '='))
                {
                    name = string.Empty;
                    value = null!;
                    index = start;
                    return false;
                }
                index++;
                value = ParseNested(ParseExpression);
                return true;
            }

            private Expression BuildLet(List<(string Name, Expression Value)> bindings, Expression body)
            {
                HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
                foreach ((string name, _) in bindings)
                {
                    if (!names.Add(name))
                    {
                        throw Error($"Duplicate let binding '{name}'", index);
                    }
                }

                for (int index = 0; index < bindings.Count; index++)
                {
                    Expression value = bindings[index].Value;
                    for (int later = index + 1; later < bindings.Count; later++)
                    {
                        if (SymbolicCalculus.DependsOn(value, bindings[later].Name))
                        {
                            throw new NotSupportedException($"Let binding '{bindings[index].Name}' cannot reference binding '{bindings[later].Name}'.");
                        }
                    }
                    for (int previous = 0; previous < index; previous++)
                    {
                        value = Algebra.Substitute(value, bindings[previous].Name, bindings[previous].Value);
                    }
                    bindings[index] = (bindings[index].Name, value);
                }

                Expression result = body;
                for (int index = bindings.Count - 1; index >= 0; index--)
                {
                    result = Algebra.Substitute(result, bindings[index].Name, bindings[index].Value);
                }
                return result;
            }

            private Expression ParseIdentifier()
            {
                int start = index;
                string name = ReadIdentifierPart();
                string? qualifier = null;
                if (CurrentCharacter == '.' && index + 1 < text.Length && IsAsciiLetter(text[index + 1]))
                {
                    index++;
                    qualifier = name;
                    name = ReadIdentifierPart();
                }
                string fullName = qualifier == null ? name : $"{qualifier}.{name}";
                string lowerName = name.ToLowerInvariant();
                if (qualifier == null)
                {
                    switch (lowerName)
                    {
                        case "pi":
                            return ExpressionBuilder.Constant(Math.PI);
                        case "e":
                            return ExpressionBuilder.Constant(Math.E);
                        case "tau":
                            return ExpressionBuilder.Constant(2.0 * Math.PI);
                        case "phi":
                            return ExpressionBuilder.Constant((1.0 + Math.Sqrt(5.0)) / 2.0);
                    }
                }

                SkipWhitespace();
                bool hasArguments = CurrentCharacter == '(';
                if (!hasArguments && qualifier != null)
                {
                    if (TryResolvePhysicalConstant(fullName, out double constantValue))
                    {
                        return ExpressionBuilder.Constant(constantValue);
                    }
                    throw Error($"Unknown qualified constant '{fullName}'", start);
                }
                if (qualifier == null && TryGetFunctionKind(lowerName, out FunctionKind function))
                {
                    if (!hasArguments)
                    {
                        throw Error($"Function '{name}' requires parentheses", index);
                    }
                    IReadOnlyList<Expression> unaryArguments = ParseArguments(start);
                    if (unaryArguments.Count == 1)
                    {
                        return ExpressionBuilder.Function(function, unaryArguments[0]);
                    }
                    if (lowerName == "log" && unaryArguments.Count == 2)
                    {
                        return ExpressionBuilder.Call("log", unaryArguments);
                    }
                    throw Error($"Function '{name}' expects 1 argument but received {unaryArguments.Count}", start);
                }
                if (!hasArguments)
                {
                    return ExpressionBuilder.Variable(name);
                }

                IReadOnlyList<Expression> arguments = ParseArguments(start);
                Expression? expanded = TryExpandIndexedCall(lowerName, arguments);
                if (expanded != null)
                {
                    return expanded;
                }
                if (qualifier == null && lowerName == "pow" && arguments.Count == 2)
                {
                    return ExpressionBuilder.Power(arguments[0], arguments[1]);
                }
                if (qualifier == null && TryGetBuiltInCallName(lowerName, arguments.Count, out string canonicalName))
                {
                    if (canonicalName == "if")
                    {
                        return ExpressionBuilder.Conditional(arguments[0], arguments[1], arguments[2]);
                    }
                    return ExpressionBuilder.Call(canonicalName, arguments);
                }
                if (MathFunctionDispatcher.CanResolveScalarExpressionCall(fullName, arguments.Count, out string dispatchName))
                {
                    return ExpressionBuilder.Call(dispatchName, arguments);
                }
                throw Error($"Unknown function '{fullName}'", start);
            }

            private Expression? TryExpandIndexedCall(string name, IReadOnlyList<Expression> arguments)
            {
                if ((name != "sum" && name != "prod") || arguments.Count != 4 || arguments[0] is not VariableExpression indexVariable)
                {
                    return null;
                }
                if (!TryGetIntegerValue(arguments[1], out int start) || !TryGetIntegerValue(arguments[2], out int end))
                {
                    throw Error($"'{name}' bounds must simplify to integer constants", Position);
                }
                long span = (long)end - start + 1L;
                if (span > MaximumIndexedIterations)
                {
                    throw new FormatException($"'{name}' bounds may cover at most {MaximumIndexedIterations} values.");
                }
                Expression result = ExpressionBuilder.Constant(name == "sum" ? 0.0 : 1.0);
                if (span <= 0)
                {
                    return result;
                }
                for (long value = start; value <= end; value++)
                {
                    if (value < int.MinValue || value > int.MaxValue)
                    {
                        throw new NotSupportedException($"'{name}' index value {value} exceeds supported integer bounds.");
                    }
                    Expression term = Algebra.Substitute(arguments[3], indexVariable.Name, ExpressionBuilder.Constant((double)value));
                    result = name == "sum"
                        ? ExpressionBuilder.Add(result, term)
                        : ExpressionBuilder.Multiply(result, term);
                }
                return SymbolicCalculus.Simplify(result);
            }

            private static bool TryGetIntegerValue(Expression expression, out int value)
            {
                Expression simplified = Algebra.Simplify(expression);
                if (simplified is ConstantExpression constant
                    && double.IsFinite(constant.Value)
                    && constant.Value == Math.Truncate(constant.Value)
                    && constant.Value >= int.MinValue
                    && constant.Value <= int.MaxValue)
                {
                    value = (int)constant.Value;
                    return true;
                }
                value = 0;
                return false;
            }

            private static bool TryResolvePhysicalConstant(string fullName, out double value)
            {
                value = 0.0;
                switch (fullName.ToLowerInvariant())
                {
                    case "physicalconstants.speedoflight":
                    case "const.c":
                        value = PhysicalConstants.SpeedOfLight.ValueSI;
                        return true;
                    case "physicalconstants.standardgravity":
                    case "const.g0":
                        value = PhysicalConstants.StandardGravity.ValueSI;
                        return true;
                    case "physicalconstants.gravitationalconstant":
                    case "const.g":
                        value = PhysicalConstants.GravitationalConstant.ValueSI;
                        return true;
                    case "physicalconstants.planckconstant":
                    case "const.h":
                        value = PhysicalConstants.PlanckConstant.ValueSI;
                        return true;
                    case "physicalconstants.reducedplanckconstant":
                    case "const.hbar":
                        value = PhysicalConstants.ReducedPlanckConstant.ValueSI;
                        return true;
                    case "physicalconstants.boltzmannconstant":
                    case "const.kb":
                    case "const.k":
                        value = PhysicalConstants.BoltzmannConstant.ValueSI;
                        return true;
                    case "physicalconstants.elementarycharge":
                    case "const.e":
                    case "const.qe":
                        value = PhysicalConstants.ElementaryCharge.ValueSI;
                        return true;
                    case "physicalconstants.avogadroconstant":
                    case "const.na":
                        value = PhysicalConstants.AvogadroConstant.ValueSI;
                        return true;
                    case "physicalconstants.idealgasconstant":
                    case "const.r":
                        value = PhysicalConstants.IdealGasConstant.ValueSI;
                        return true;
                    case "physicalconstants.vacuumpermittivity":
                    case "const.eps0":
                    case "const.e0":
                        value = PhysicalConstants.VacuumPermittivity.ValueSI;
                        return true;
                    case "physicalconstants.vacuumpermeability":
                    case "const.mu0":
                    case "const.m0":
                        value = PhysicalConstants.VacuumPermeability.ValueSI;
                        return true;
                    default:
                        return false;
                }
            }

            private string ReadIdentifierPart()
            {
                int start = index;
                index++;
                while (index < text.Length && (IsAsciiLetter(text[index]) || IsAsciiDigit(text[index]) || text[index] == '_'))
                {
                    index++;
                    if (index - start > MaximumIdentifierLength)
                    {
                        throw Error($"Identifier exceeds the {MaximumIdentifierLength} character limit", start);
                    }
                }
                return text.Substring(start, index - start);
            }

            private IReadOnlyList<Expression> ParseArguments(int functionStart)
            {
                Expect('(');
                SkipWhitespace();
                if (CurrentCharacter == ')')
                {
                    throw Error("Function requires at least one argument", functionStart);
                }
                List<Expression> arguments = new List<Expression>();
                while (true)
                {
                    arguments.Add(ParseNested(ParseExpression));
                    if (arguments.Count > MaximumFunctionArguments)
                    {
                        throw Error($"Function exceeds the {MaximumFunctionArguments} argument limit", functionStart);
                    }
                    SkipWhitespace();
                    if (Match(','))
                    {
                        SkipWhitespace();
                        if (CurrentCharacter == ')')
                        {
                            throw Error("Trailing comma in function argument list", index);
                        }
                        continue;
                    }
                    Expect(')');
                    return arguments;
                }
            }

            private static bool TryGetFunctionKind(string name, out FunctionKind function)
            {
                switch (name)
                {
                    case "sin":
                        function = FunctionKind.Sin;
                        return true;
                    case "cos":
                        function = FunctionKind.Cos;
                        return true;
                    case "tan":
                        function = FunctionKind.Tan;
                        return true;
                    case "cot":
                        function = FunctionKind.Cot;
                        return true;
                    case "sec":
                        function = FunctionKind.Sec;
                        return true;
                    case "csc":
                        function = FunctionKind.Csc;
                        return true;
                    case "asin":
                    case "arcsin":
                        function = FunctionKind.Asin;
                        return true;
                    case "acos":
                    case "arccos":
                        function = FunctionKind.Acos;
                        return true;
                    case "atan":
                    case "arctan":
                        function = FunctionKind.Atan;
                        return true;
                    case "sinh":
                        function = FunctionKind.Sinh;
                        return true;
                    case "cosh":
                        function = FunctionKind.Cosh;
                        return true;
                    case "tanh":
                        function = FunctionKind.Tanh;
                        return true;
                    case "asinh":
                        function = FunctionKind.Asinh;
                        return true;
                    case "acosh":
                        function = FunctionKind.Acosh;
                        return true;
                    case "atanh":
                        function = FunctionKind.Atanh;
                        return true;
                    case "exp":
                        function = FunctionKind.Exp;
                        return true;
                    case "ln":
                        function = FunctionKind.Ln;
                        return true;
                    case "log10":
                    case "log":
                        function = FunctionKind.Log10;
                        return true;
                    case "sqrt":
                        function = FunctionKind.Sqrt;
                        return true;
                    case "cbrt":
                        function = FunctionKind.Cbrt;
                        return true;
                    case "abs":
                        function = FunctionKind.Abs;
                        return true;
                    case "sign":
                        function = FunctionKind.Sign;
                        return true;
                    case "log2":
                    case "lb":
                        function = FunctionKind.Log2;
                        return true;
                    case "sech":
                        function = FunctionKind.Sech;
                        return true;
                    case "csch":
                        function = FunctionKind.Csch;
                        return true;
                    case "coth":
                        function = FunctionKind.Coth;
                        return true;
                    case "asech":
                        function = FunctionKind.Asech;
                        return true;
                    case "acsch":
                        function = FunctionKind.Acsch;
                        return true;
                    case "acoth":
                        function = FunctionKind.Acoth;
                        return true;
                    case "erf":
                        function = FunctionKind.Erf;
                        return true;
                    case "erfc":
                        function = FunctionKind.Erfc;
                        return true;
                    case "gamma":
                        function = FunctionKind.Gamma;
                        return true;
                    case "lgamma":
                    case "lngamma":
                    case "loggamma":
                        function = FunctionKind.LogGamma;
                        return true;
                    case "normpdf":
                    case "normalpdf":
                        function = FunctionKind.NormalPdf;
                        return true;
                    case "normcdf":
                    case "normalcdf":
                        function = FunctionKind.NormalCdf;
                        return true;
                    case "norminv":
                    case "normquantile":
                        function = FunctionKind.NormalQuantile;
                        return true;
                    case "sigmoid":
                        function = FunctionKind.Sigmoid;
                        return true;
                    case "softplus":
                        function = FunctionKind.Softplus;
                        return true;
                    case "relu":
                        function = FunctionKind.Relu;
                        return true;
                    case "gelu":
                        function = FunctionKind.Gelu;
                        return true;
                    case "floor":
                        function = FunctionKind.Floor;
                        return true;
                    case "ceil":
                    case "ceiling":
                        function = FunctionKind.Ceiling;
                        return true;
                    case "round":
                        function = FunctionKind.Round;
                        return true;
                    case "trunc":
                    case "truncate":
                        function = FunctionKind.Truncate;
                        return true;
                    case "frac":
                        function = FunctionKind.FractionalPart;
                        return true;
                    case "deg2rad":
                        function = FunctionKind.DegreesToRadians;
                        return true;
                    case "rad2deg":
                        function = FunctionKind.RadiansToDegrees;
                        return true;
                    case "fact":
                    case "factorial":
                        function = FunctionKind.Factorial;
                        return true;
                    default:
                        function = default;
                        return false;
                }
            }

            private static bool TryGetBuiltInCallName(string name, int argumentCount, out string canonicalName)
            {
                canonicalName = string.Empty;
                bool supported = name switch
                {
                    "if" => argumentCount == 3,
                    "log" => argumentCount == 2,
                    "atan2" => argumentCount == 2,
                    "root" or "nthroot" => argumentCount == 2,
                    "min" or "max" or "hypot" or "sum" or "avg" or "mean" or "prod" => argumentCount >= 2,
                    "gcd" or "lcm" or "mod" or "beta" or "gammap" or "gammaq" => argumentCount == 2,
                    "ncr" or "comb" or "npr" or "perm" => argumentCount == 2,
                    "clamp" or "lerp" or "modpow" or "betainc" => argumentCount == 3,
                    _ => false
                };
                if (!supported)
                {
                    return false;
                }
                canonicalName = name switch
                {
                    "root" => "nthroot",
                    "comb" => "ncr",
                    "perm" => "npr",
                    "mean" => "avg",
                    _ => name
                };
                return true;
            }

            private bool StartsImplicitFactor()
            {
                SkipWhitespace();
                if (IsAtEnd)
                {
                    return false;
                }
                char current = CurrentCharacter;
                if (current == '|')
                {
                    return absBarDepth == 0 && !StartsOperator("||");
                }
                return IsAsciiLetter(current) || IsAsciiDigit(current) || current == '_' || current == '.' || current == '(';
            }

            private bool StartsOperator(string value)
            {
                SkipWhitespace();
                if (index + value.Length > text.Length)
                {
                    return false;
                }
                for (int offset = 0; offset < value.Length; offset++)
                {
                    if (text[index + offset] != value[offset])
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool StartsInKeyword()
            {
                int start = index;
                SkipWhitespace();
                bool result = TryReadKeyword("in");
                index = start;
                return result;
            }

            private bool TryReadKeyword(string keyword)
            {
                int start = index;
                SkipWhitespace();
                if (IsAtEnd || (!IsAsciiLetter(CurrentCharacter) && CurrentCharacter != '_'))
                {
                    index = start;
                    return false;
                }
                string word = ReadIdentifierPart();
                if (!string.Equals(word, keyword, StringComparison.Ordinal))
                {
                    index = start;
                    return false;
                }
                return true;
            }

            private Expression ParseNested(Func<Expression> parse)
            {
                recursionDepth++;
                if (recursionDepth > MaximumNestingDepth)
                {
                    recursionDepth--;
                    throw Error($"Expression nesting exceeds the {MaximumNestingDepth} level limit", index);
                }
                try
                {
                    Expression result = parse();
                    ValidateExpressionLimits(result);
                    return result;
                }
                finally
                {
                    recursionDepth--;
                }
            }

            private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
            private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

            private bool Match(char character)
            {
                SkipWhitespace();
                if (CurrentCharacter == character)
                {
                    index++;
                    return true;
                }
                return false;
            }

            private void Expect(char character)
            {
                SkipWhitespace();
                if (CurrentCharacter != character)
                {
                    throw Error($"Expected '{character}'", index);
                }
                index++;
            }
        }
    }
}
