using System;
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
            Name = name.Trim();
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
                _ => throw new InvalidOperationException("Expression function is invalid.")
            };
        }
    }

    internal enum BinaryOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Power
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
        Sign
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
    }
}
