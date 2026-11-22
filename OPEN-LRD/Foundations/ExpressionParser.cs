using System;
using System.Globalization;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Foundations
{
    internal static class ExpressionParser
    {
        internal const int MaximumInputLength = 16_384;
        internal const int MaximumNestingDepth = 256;

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
                throw new FormatException($"Unexpected character '{parser.CurrentCharacter}' at position {parser.Position}.");
            }
            return expression;
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;
            private int recursionDepth;

            internal Parser(string text)
            {
                this.text = text;
                index = 0;
            }

            internal int Position => index;

            internal char CurrentCharacter => index < text.Length ? text[index] : '\0';

            internal bool IsAtEnd => index >= text.Length;

            internal void SkipWhitespace()
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                {
                    index++;
                }
            }

            internal Expression ParseExpression()
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
                    else if (StartsImplicitFactor())
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
                return ParsePower();
            }

            private Expression ParsePower()
            {
                Expression left = ParsePrimary();
                SkipWhitespace();
                if (Match('^'))
                {
                    return ExpressionBuilder.Power(left, ParseNested(ParseUnary));
                }
                return left;
            }

            private Expression ParsePrimary()
            {
                SkipWhitespace();
                if (IsAtEnd)
                {
                    throw new FormatException($"Expected an expression at position {index}.");
                }
                char current = CurrentCharacter;
                if (current == '(')
                {
                    index++;
                    Expression inside = ParseNested(ParseExpression);
                    Expect(')');
                    return inside;
                }
                if (IsAsciiDigit(current) || current == '.')
                {
                    return ParseNumber();
                }
                if (IsAsciiLetter(current) || current == '_')
                {
                    return ParseIdentifier();
                }
                throw new FormatException($"Unexpected character '{current}' at position {index}.");
            }

            private Expression ParseNumber()
            {
                int start = index;
                bool hasDigits = false;
                while (index < text.Length && IsAsciiDigit(text[index]))
                {
                    hasDigits = true;
                    index++;
                }
                if (index < text.Length && text[index] == '.')
                {
                    index++;
                    while (index < text.Length && IsAsciiDigit(text[index]))
                    {
                        hasDigits = true;
                        index++;
                    }
                }
                if (!hasDigits)
                {
                    throw new FormatException($"Invalid number at position {start}.");
                }
                if (index < text.Length && text[index] == '.')
                {
                    throw new FormatException($"A numeric literal cannot contain multiple decimal points at position {index}.");
                }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    int exponentStart = index;
                    index++;
                    if (index < text.Length && (text[index] == '+' || text[index] == '-'))
                    {
                        index++;
                    }
                    bool hasExponentDigits = false;
                    while (index < text.Length && char.IsDigit(text[index]))
                    {
                        hasExponentDigits = true;
                        index++;
                    }
                    if (!hasExponentDigits)
                    {
                        throw new FormatException($"Expected exponent digits at position {exponentStart + 1}.");
                    }
                }
                string token = text.Substring(start, index - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                {
                    throw new FormatException($"Invalid number '{token}' at position {start}.");
                }
                return ExpressionBuilder.Constant(value);
            }

            private Expression ParseIdentifier()
            {
                int start = index;
                index++;
                while (index < text.Length && (IsAsciiLetter(text[index]) || IsAsciiDigit(text[index]) || text[index] == '_'))
                {
                    index++;
                }
                string name = text.Substring(start, index - start);
                string lowerName = name.ToLowerInvariant();
                if (lowerName == "pi")
                {
                    return ExpressionBuilder.Constant(Math.PI);
                }
                if (lowerName == "e")
                {
                    return ExpressionBuilder.Constant(Math.E);
                }
                if (TryGetFunctionKind(lowerName, out FunctionKind function))
                {
                    SkipWhitespace();
                    if (CurrentCharacter != '(')
                    {
                        throw new FormatException($"Function '{name}' requires parentheses at position {index}.");
                    }
                    index++;
                    Expression argument = ParseNested(ParseExpression);
                    Expect(')');
                    return ExpressionBuilder.Function(function, argument);
                }
                SkipWhitespace();
                if (CurrentCharacter == '(')
                {
                    throw new FormatException($"Unknown function '{name}' at position {start}.");
                }
                return ExpressionBuilder.Variable(name);
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
                    default:
                        function = default;
                        return false;
                }
            }

            private bool StartsImplicitFactor()
            {
                SkipWhitespace();
                if (IsAtEnd)
                {
                    return false;
                }
                char current = CurrentCharacter;
                return IsAsciiLetter(current) || IsAsciiDigit(current) || current == '_' || current == '.' || current == '(';
            }

            private Expression ParseNested(Func<Expression> parse)
            {
                recursionDepth++;
                if (recursionDepth > MaximumNestingDepth)
                {
                    recursionDepth--;
                    throw new FormatException($"Expression nesting exceeds the {MaximumNestingDepth} level limit at position {index}.");
                }
                try
                {
                    return parse();
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
                    throw new FormatException($"Expected '{character}' at position {index}.");
                }
                index++;
            }
        }
    }
}
