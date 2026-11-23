using System;
using System.Collections.Generic;
using System.Globalization;

namespace OPEN_LRD.Foundations
{
    internal abstract record Expression
    {
        public abstract override string ToString();
    }

    internal sealed record ConstantExpression : Expression
    {
        public double Value { get; }

        public ConstantExpression(double value)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Expression constants must be finite.");
            }
            Value = value;
        }

        public override string ToString() => Value.ToString("R", CultureInfo.InvariantCulture);
    }

    internal sealed record VariableExpression : Expression
    {
        public string Name { get; }

        public VariableExpression(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A variable name is required.", nameof(name));
            }
            string normalized = name.Trim();
            if (normalized.Length > ExpressionParser.MaximumIdentifierLength)
            {
                throw new FormatException($"Variable identifier exceeds the {ExpressionParser.MaximumIdentifierLength} character limit.");
            }
            Name = normalized;
        }

        public override string ToString() => Name;
    }

    internal sealed record NegateExpression : Expression
    {
        public Expression Operand { get; }

        public NegateExpression(Expression operand)
        {
            Operand = operand ?? throw new ArgumentNullException(nameof(operand));
        }

        public override string ToString() => $"-{Operand}";
    }

    internal sealed record BinaryExpression : Expression
    {
        public BinaryOperator Operator { get; }
        public Expression Left { get; }
        public Expression Right { get; }

        public BinaryExpression(BinaryOperator @operator, Expression left, Expression right)
        {
            if (!Enum.IsDefined(@operator))
            {
                throw new ArgumentOutOfRangeException(nameof(@operator));
            }
            Operator = @operator;
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

        public override string ToString()
        {
            string symbol = Operator switch
            {
                BinaryOperator.Add => "+",
                BinaryOperator.Subtract => "-",
                BinaryOperator.Multiply => "*",
                BinaryOperator.Divide => "/",
                BinaryOperator.Power => "^",
                BinaryOperator.LessThan => "<",
                BinaryOperator.LessThanOrEqual => "<=",
                BinaryOperator.GreaterThan => ">",
                BinaryOperator.GreaterThanOrEqual => ">=",
                BinaryOperator.Equal => "==",
                BinaryOperator.NotEqual => "!=",
                BinaryOperator.And => "&&",
                BinaryOperator.Or => "||",
                _ => throw new InvalidOperationException("Expression operator is invalid.")
            };
            return $"({Left} {symbol} {Right})";
        }
    }

    internal sealed record FunctionExpression : Expression
    {
        public FunctionKind Function { get; }
        public Expression Argument { get; }

        public FunctionExpression(FunctionKind function, Expression argument)
        {
            if (!Enum.IsDefined(function))
            {
                throw new ArgumentOutOfRangeException(nameof(function));
            }
            Function = function;
            Argument = argument ?? throw new ArgumentNullException(nameof(argument));
        }

        public override string ToString() => $"{FunctionName(Function)}({Argument})";

        private static string FunctionName(FunctionKind function)
        {
            return function switch
            {
                FunctionKind.Sin => "sin",
                FunctionKind.Cos => "cos",
                FunctionKind.Tan => "tan",
                FunctionKind.Cot => "cot",
                FunctionKind.Sec => "sec",
                FunctionKind.Csc => "csc",
                FunctionKind.Asin => "asin",
                FunctionKind.Acos => "acos",
                FunctionKind.Atan => "atan",
                FunctionKind.Sinh => "sinh",
                FunctionKind.Cosh => "cosh",
                FunctionKind.Tanh => "tanh",
                FunctionKind.Asinh => "asinh",
                FunctionKind.Acosh => "acosh",
                FunctionKind.Atanh => "atanh",
                FunctionKind.Exp => "exp",
                FunctionKind.Ln => "ln",
                FunctionKind.Log10 => "log10",
                FunctionKind.Sqrt => "sqrt",
                FunctionKind.Cbrt => "cbrt",
                FunctionKind.Abs => "abs",
                FunctionKind.Sign => "sign",
                FunctionKind.Log2 => "log2",
                FunctionKind.Sech => "sech",
                FunctionKind.Csch => "csch",
                FunctionKind.Coth => "coth",
                FunctionKind.Asech => "asech",
                FunctionKind.Acsch => "acsch",
                FunctionKind.Acoth => "acoth",
                FunctionKind.Erf => "erf",
                FunctionKind.Erfc => "erfc",
                FunctionKind.Gamma => "gamma",
                FunctionKind.LogGamma => "lgamma",
                FunctionKind.NormalPdf => "normpdf",
                FunctionKind.NormalCdf => "normcdf",
                FunctionKind.NormalQuantile => "norminv",
                FunctionKind.Sigmoid => "sigmoid",
                FunctionKind.Softplus => "softplus",
                FunctionKind.Relu => "relu",
                FunctionKind.Gelu => "gelu",
                FunctionKind.Floor => "floor",
                FunctionKind.Ceiling => "ceil",
                FunctionKind.Round => "round",
                FunctionKind.Truncate => "trunc",
                FunctionKind.FractionalPart => "frac",
                FunctionKind.DegreesToRadians => "deg2rad",
                FunctionKind.RadiansToDegrees => "rad2deg",
                FunctionKind.Factorial => "factorial",
                _ => throw new InvalidOperationException("Expression function is invalid.")
            };
        }
    }

    internal sealed record CallExpression : Expression
    {
        public string FunctionName { get; }
        public IReadOnlyList<Expression> Arguments { get; }

        public CallExpression(string functionName, IReadOnlyList<Expression> arguments)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                throw new ArgumentException("A call function name is required.", nameof(functionName));
            }
            string normalizedName = functionName.Trim();
            if (normalizedName.Length > ExpressionParser.MaximumIdentifierLength)
            {
                throw new FormatException($"Call identifier exceeds the {ExpressionParser.MaximumIdentifierLength} character limit.");
            }
            ArgumentNullException.ThrowIfNull(arguments);
            if (arguments.Count == 0)
            {
                throw new ArgumentException("A call requires at least one argument.", nameof(arguments));
            }
            if (arguments.Count > ExpressionParser.MaximumFunctionArguments)
            {
                throw new FormatException($"Function call exceeds the {ExpressionParser.MaximumFunctionArguments} argument limit.");
            }
            List<Expression> copy = new List<Expression>(arguments.Count);
            foreach (Expression argument in arguments)
            {
                if (argument == null)
                {
                    throw new ArgumentException("Call arguments cannot contain null entries.", nameof(arguments));
                }
                copy.Add(argument);
            }
            FunctionName = normalizedName;
            Arguments = copy.AsReadOnly();
        }

        public override string ToString() => $"{FunctionName}({string.Join(", ", Arguments)})";

        public bool Equals(CallExpression? other)
        {
            if (other is null
                || !string.Equals(FunctionName, other.FunctionName, StringComparison.OrdinalIgnoreCase)
                || Arguments.Count != other.Arguments.Count)
            {
                return false;
            }
            for (int index = 0; index < Arguments.Count; index++)
            {
                if (!Arguments[index].Equals(other.Arguments[index]))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(FunctionName, StringComparer.OrdinalIgnoreCase);
            foreach (Expression argument in Arguments)
            {
                hash.Add(argument);
            }
            return hash.ToHashCode();
        }
    }

    internal sealed record ConditionalExpression : Expression
    {
        public Expression Condition { get; }
        public Expression WhenTrue { get; }
        public Expression WhenFalse { get; }

        public ConditionalExpression(Expression condition, Expression whenTrue, Expression whenFalse)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            WhenTrue = whenTrue ?? throw new ArgumentNullException(nameof(whenTrue));
            WhenFalse = whenFalse ?? throw new ArgumentNullException(nameof(whenFalse));
        }

        public override string ToString() => $"if({Condition}, {WhenTrue}, {WhenFalse})";
    }

    internal enum BinaryOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Power,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal,
        NotEqual,
        And,
        Or
    }

    internal enum FunctionKind
    {
        Sin,
        Cos,
        Tan,
        Cot,
        Sec,
        Csc,
        Asin,
        Acos,
        Atan,
        Sinh,
        Cosh,
        Tanh,
        Asinh,
        Acosh,
        Atanh,
        Exp,
        Ln,
        Log10,
        Sqrt,
        Cbrt,
        Abs,
        Sign,
        Log2,
        Sech,
        Csch,
        Coth,
        Asech,
        Acsch,
        Acoth,
        Erf,
        Erfc,
        Gamma,
        LogGamma,
        NormalPdf,
        NormalCdf,
        NormalQuantile,
        Sigmoid,
        Softplus,
        Relu,
        Gelu,
        Floor,
        Ceiling,
        Round,
        Truncate,
        FractionalPart,
        DegreesToRadians,
        RadiansToDegrees,
        Factorial
    }

    internal static class ExpressionBuilder
    {
        internal static Expression Constant(double value) => new ConstantExpression(value);
        internal static Expression Variable(string name) => new VariableExpression(name);
        internal static Expression Negate(Expression operand) => new NegateExpression(operand);
        internal static Expression Add(Expression left, Expression right) => new BinaryExpression(BinaryOperator.Add, left, right);
        internal static Expression Subtract(Expression left, Expression right) => new BinaryExpression(BinaryOperator.Subtract, left, right);
        internal static Expression Multiply(Expression left, Expression right) => new BinaryExpression(BinaryOperator.Multiply, left, right);
        internal static Expression Divide(Expression left, Expression right) => new BinaryExpression(BinaryOperator.Divide, left, right);
        internal static Expression Power(Expression left, Expression right) => new BinaryExpression(BinaryOperator.Power, left, right);
        internal static Expression Function(FunctionKind function, Expression argument) => new FunctionExpression(function, argument);
        internal static Expression Call(string functionName, IReadOnlyList<Expression> arguments) => new CallExpression(functionName, arguments);
        internal static Expression Call(string functionName, params Expression[] arguments) => new CallExpression(functionName, arguments);
        internal static Expression Binary(BinaryOperator @operator, Expression left, Expression right) => new BinaryExpression(@operator, left, right);
        internal static Expression Conditional(Expression condition, Expression whenTrue, Expression whenFalse) => new ConditionalExpression(condition, whenTrue, whenFalse);
        internal static Expression LessThan(Expression left, Expression right) => new BinaryExpression(BinaryOperator.LessThan, left, right);
        internal static Expression LessThanOrEqual(Expression left, Expression right) => new BinaryExpression(BinaryOperator.LessThanOrEqual, left, right);
        internal static Expression GreaterThan(Expression left, Expression right) => new BinaryExpression(BinaryOperator.GreaterThan, left, right);
        internal static Expression GreaterThanOrEqual(Expression left, Expression right) => new BinaryExpression(BinaryOperator.GreaterThanOrEqual, left, right);
        internal static Expression Equal(Expression left, Expression right) => new BinaryExpression(BinaryOperator.Equal, left, right);
        internal static Expression NotEqual(Expression left, Expression right) => new BinaryExpression(BinaryOperator.NotEqual, left, right);
        internal static Expression And(Expression left, Expression right) => new BinaryExpression(BinaryOperator.And, left, right);
        internal static Expression Or(Expression left, Expression right) => new BinaryExpression(BinaryOperator.Or, left, right);
        internal static Expression Not(Expression operand) => new CallExpression("not", new[] { operand });
    }
}
