using System;
using System.Globalization;

namespace OPEN_LRD.Foundations
{
    internal abstract record Expression
    {
        public abstract override string ToString();
    }

    internal sealed record ConstantExpression(double Value) : Expression
    {
        public override string ToString()
        {
            return Value.ToString("G", CultureInfo.InvariantCulture);
        }
    }

    internal sealed record VariableExpression(string Name) : Expression
    {
        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed record NegateExpression(Expression Operand) : Expression
    {
        public override string ToString()
        {
            return $"-{Operand}";
        }
    }

    internal sealed record BinaryExpression(BinaryOperator Operator, Expression Left, Expression Right) : Expression
    {
        public override string ToString()
        {
            string symbol = Operator switch
            {
                BinaryOperator.Add => "+",
                BinaryOperator.Subtract => "-",
                BinaryOperator.Multiply => "*",
                BinaryOperator.Divide => "/",
                BinaryOperator.Power => "^",
                _ => "?"
            };
            return $"({Left} {symbol} {Right})";
        }
    }

    internal sealed record FunctionExpression(FunctionKind Function, Expression Argument) : Expression
    {
        public override string ToString()
        {
            return $"{FunctionName(Function)}({Argument})";
        }

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
                _ => "unknown"
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
        internal static Expression Constant(double value)
        {
            return new ConstantExpression(value);
        }

        internal static Expression Variable(string name)
        {
            return new VariableExpression(name);
        }

        internal static Expression Negate(Expression operand)
        {
            return new NegateExpression(operand);
        }

        internal static Expression Add(Expression left, Expression right)
        {
            return new BinaryExpression(BinaryOperator.Add, left, right);
        }

        internal static Expression Subtract(Expression left, Expression right)
        {
            return new BinaryExpression(BinaryOperator.Subtract, left, right);
        }

        internal static Expression Multiply(Expression left, Expression right)
        {
            return new BinaryExpression(BinaryOperator.Multiply, left, right);
        }

        internal static Expression Divide(Expression left, Expression right)
        {
            return new BinaryExpression(BinaryOperator.Divide, left, right);
        }

        internal static Expression Power(Expression left, Expression right)
        {
            return new BinaryExpression(BinaryOperator.Power, left, right);
        }

        internal static Expression Function(FunctionKind function, Expression argument)
        {
            return new FunctionExpression(function, argument);
        }
    }
}
