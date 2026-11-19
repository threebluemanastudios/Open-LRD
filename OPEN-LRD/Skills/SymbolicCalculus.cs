using System;
using System.Collections.Generic;
using System.Globalization;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal static class SymbolicCalculus
    {
        private const string SubstitutionVariable = "__u";
        private const int MaxSimplifyPasses = 24;
        private const int MaxIntegrationDepth = 10;
        private const int MaxPolynomialPower = 20;
        private const double Tolerance = 1e-9;

        internal static Expression Simplify(Expression expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            Expression current = expression;
            for (int i = 0; i < MaxSimplifyPasses; i++)
            {
                Expression next = SimplifyOnce(current);
                if (next.Equals(current))
                {
                    return next;
                }
                current = next;
            }
            return current;
        }

        private static Expression SimplifyOnce(Expression expression)
        {
            switch (expression)
            {
                case ConstantExpression:
                case VariableExpression:
                    return expression;
                case NegateExpression negate:
                {
                    Expression operand = SimplifyOnce(negate.Operand);
                    if (operand is ConstantExpression constant)
                    {
                        return ExpressionBuilder.Constant(-constant.Value);
                    }
                    if (operand is NegateExpression inner)
                    {
                        return inner.Operand;
                    }
                    if (IsZero(operand))
                    {
                        return ExpressionBuilder.Constant(0.0);
                    }
                    return ExpressionBuilder.Negate(operand);
                }
                case BinaryExpression binary:
                {
                    Expression left = SimplifyOnce(binary.Left);
                    Expression right = SimplifyOnce(binary.Right);
                    return SimplifyBinary(binary.Operator, left, right);
                }
                case FunctionExpression function:
                {
                    Expression argument = SimplifyOnce(function.Argument);
                    if (TryGetConstant(argument, out double value) && TryEvaluateFunction(function.Function, value, out double result))
                    {
                        return ExpressionBuilder.Constant(result);
                    }
                    return ExpressionBuilder.Function(function.Function, argument);
                }
                default:
                    return expression;
            }
        }

        private static Expression SimplifyBinary(BinaryOperator op, Expression left, Expression right)
        {
            bool leftConstant = TryGetConstant(left, out double leftValue);
            bool rightConstant = TryGetConstant(right, out double rightValue);

            switch (op)
            {
                case BinaryOperator.Add:
                {
                    if (IsZero(left))
                    {
                        return right;
                    }
                    if (IsZero(right))
                    {
                        return left;
                    }
                    if (leftConstant && rightConstant)
                    {
                        return ExpressionBuilder.Constant(leftValue + rightValue);
                    }
                    if (left is NegateExpression leftNegate && leftNegate.Operand.Equals(right))
                    {
                        return ExpressionBuilder.Constant(0.0);
                    }
                    if (right is NegateExpression rightNegate && rightNegate.Operand.Equals(left))
                    {
                        return ExpressionBuilder.Constant(0.0);
                    }
                    if (TryGetCoefficientAndTerm(left, out double leftCoefficient, out Expression leftTerm)
                        && TryGetCoefficientAndTerm(right, out double rightCoefficient, out Expression rightTerm)
                        && leftTerm.Equals(rightTerm))
                    {
                        return CreateTerm(leftCoefficient + rightCoefficient, leftTerm);
                    }
                    return ExpressionBuilder.Add(left, right);
                }
                case BinaryOperator.Subtract:
                {
                    if (IsZero(right))
                    {
                        return left;
                    }
                    if (IsZero(left))
                    {
                        return ExpressionBuilder.Negate(right);
                    }
                    if (leftConstant && rightConstant)
                    {
                        return ExpressionBuilder.Constant(leftValue - rightValue);
                    }
                    if (left.Equals(right))
                    {
                        return ExpressionBuilder.Constant(0.0);
                    }
                    if (TryGetCoefficientAndTerm(left, out double leftCoefficient, out Expression leftTerm)
                        && TryGetCoefficientAndTerm(right, out double rightCoefficient, out Expression rightTerm)
                        && leftTerm.Equals(rightTerm))
                    {
                        return CreateTerm(leftCoefficient - rightCoefficient, leftTerm);
                    }
                    return ExpressionBuilder.Subtract(left, right);
                }
                case BinaryOperator.Multiply:
                {
                    if (IsZero(left) || IsZero(right))
                    {
                        return ExpressionBuilder.Constant(0.0);
                    }
                    if (IsOne(left))
                    {
                        return right;
                    }
                    if (IsOne(right))
                    {
                        return left;
                    }
                    if (leftConstant && rightConstant)
                    {
                        return ExpressionBuilder.Constant(leftValue * rightValue);
                    }
                    if (rightConstant && !leftConstant)
                    {
                        return ExpressionBuilder.Multiply(right, left);
                    }
                    if (leftConstant && right is BinaryExpression { Operator: BinaryOperator.Multiply } rightProduct
                        && TryGetConstant(rightProduct.Left, out double rightFactor))
                    {
                        return ExpressionBuilder.Multiply(
                            ExpressionBuilder.Constant(leftValue * rightFactor),
                            rightProduct.Right);
                    }
                    if (left is BinaryExpression { Operator: BinaryOperator.Multiply } leftProduct
                        && TryGetConstant(leftProduct.Left, out double leftFactor)
                        && rightConstant)
                    {
                        return ExpressionBuilder.Multiply(
                            ExpressionBuilder.Constant(leftFactor * rightValue),
                            leftProduct.Right);
                    }
                    if (left.Equals(right))
                    {
                        return ExpressionBuilder.Power(left, ExpressionBuilder.Constant(2.0));
                    }
                    if (left is BinaryExpression { Operator: BinaryOperator.Power } leftPower
                        && TryGetConstant(leftPower.Right, out double leftExponent)
                        && right is BinaryExpression { Operator: BinaryOperator.Power } rightPower
                        && TryGetConstant(rightPower.Right, out double rightExponent)
                        && leftPower.Left.Equals(rightPower.Left))
                    {
                        return ExpressionBuilder.Power(
                            leftPower.Left,
                            ExpressionBuilder.Constant(leftExponent + rightExponent));
                    }
                    if (left is BinaryExpression { Operator: BinaryOperator.Power } leftPowerOnly
                        && TryGetConstant(leftPowerOnly.Right, out double leftPowerExponent)
                        && leftPowerOnly.Left.Equals(right))
                    {
                        return ExpressionBuilder.Power(
                            leftPowerOnly.Left,
                            ExpressionBuilder.Constant(leftPowerExponent + 1.0));
                    }
                    if (right is BinaryExpression { Operator: BinaryOperator.Power } rightPowerOnly
                        && TryGetConstant(rightPowerOnly.Right, out double rightPowerExponent)
                        && rightPowerOnly.Left.Equals(left))
                    {
                        return ExpressionBuilder.Power(
                            rightPowerOnly.Left,
                            ExpressionBuilder.Constant(rightPowerExponent + 1.0));
                    }
                    return ExpressionBuilder.Multiply(left, right);
                }
                case BinaryOperator.Divide:
                {
                    if (rightConstant && rightValue == 0.0)
                    {
                        throw new DivideByZeroException("Cannot divide by zero.");
                    }
                    if (IsZero(left))
                    {
                        return ExpressionBuilder.Constant(0.0);
                    }
                    if (IsOne(right))
                    {
                        return left;
                    }
                    if (leftConstant && rightConstant)
                    {
                        return ExpressionBuilder.Constant(leftValue / rightValue);
                    }
                    if (left.Equals(right))
                    {
                        return ExpressionBuilder.Constant(1.0);
                    }
                    if (rightConstant)
                    {
                        return ExpressionBuilder.Multiply(
                            ExpressionBuilder.Constant(1.0 / rightValue),
                            left);
                    }
                    return ExpressionBuilder.Divide(left, right);
                }
                case BinaryOperator.Power:
                {
                    if (rightConstant && rightValue == 0.0)
                    {
                        return ExpressionBuilder.Constant(1.0);
                    }
                    if (rightConstant && rightValue == 1.0)
                    {
                        return left;
                    }
                    if (IsOne(left))
                    {
                        return ExpressionBuilder.Constant(1.0);
                    }
                    if (leftConstant && rightConstant)
                    {
                        return ExpressionBuilder.Constant(Math.Pow(leftValue, rightValue));
                    }
                    if (IsZero(left) && rightConstant && rightValue > 0.0)
                    {
                        return ExpressionBuilder.Constant(0.0);
                    }
                    if (left is BinaryExpression { Operator: BinaryOperator.Power } innerPower
                        && TryGetConstant(innerPower.Right, out double innerExponent)
                        && rightConstant)
                    {
                        return ExpressionBuilder.Power(
                            innerPower.Left,
                            ExpressionBuilder.Constant(innerExponent * rightValue));
                    }
                    return ExpressionBuilder.Power(left, right);
                }
                default:
                    return ExpressionBuilder.Add(left, right);
            }
        }

        private static bool TryGetCoefficientAndTerm(Expression expression, out double coefficient, out Expression term)
        {
            if (expression is ConstantExpression constant)
            {
                coefficient = constant.Value;
                term = ExpressionBuilder.Constant(1.0);
                return true;
            }
            if (expression is NegateExpression negate)
            {
                if (TryGetCoefficientAndTerm(negate.Operand, out double innerCoefficient, out Expression innerTerm))
                {
                    coefficient = -innerCoefficient;
                    term = innerTerm;
                    return true;
                }
                coefficient = -1.0;
                term = negate.Operand;
                return true;
            }
            if (expression is BinaryExpression { Operator: BinaryOperator.Multiply } product)
            {
                if (TryGetConstant(product.Left, out double leftCoefficient))
                {
                    coefficient = leftCoefficient;
                    term = product.Right;
                    return true;
                }
                if (TryGetConstant(product.Right, out double rightCoefficient))
                {
                    coefficient = rightCoefficient;
                    term = product.Left;
                    return true;
                }
            }
            coefficient = 1.0;
            term = expression;
            return true;
        }

        private static Expression CreateTerm(double coefficient, Expression term)
        {
            if (coefficient == 0.0)
            {
                return ExpressionBuilder.Constant(0.0);
            }
            if (term is ConstantExpression constant && constant.Value == 1.0)
            {
                return ExpressionBuilder.Constant(coefficient);
            }
            if (coefficient == 1.0)
            {
                return term;
            }
            return ExpressionBuilder.Multiply(ExpressionBuilder.Constant(coefficient), term);
        }

        private static bool TryGetConstant(Expression expression, out double value)
        {
            if (expression is ConstantExpression constant)
            {
                value = constant.Value;
                return true;
            }
            value = 0.0;
            return false;
        }

        private static bool IsZero(Expression expression)
        {
            return expression is ConstantExpression constant && constant.Value == 0.0;
        }

        private static bool IsOne(Expression expression)
        {
            return expression is ConstantExpression constant && constant.Value == 1.0;
        }

        private static bool TryEvaluateFunction(FunctionKind function, double value, out double result)
        {
            switch (function)
            {
                case FunctionKind.Sin:
                    result = Math.Sin(value);
                    return true;
                case FunctionKind.Cos:
                    result = Math.Cos(value);
                    return true;
                case FunctionKind.Tan:
                    result = Math.Tan(value);
                    return true;
                case FunctionKind.Cot:
                    if (Math.Abs(Math.Sin(value)) < 1e-15)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = Math.Cos(value) / Math.Sin(value);
                    return true;
                case FunctionKind.Sec:
                    if (Math.Abs(Math.Cos(value)) < 1e-15)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = 1.0 / Math.Cos(value);
                    return true;
                case FunctionKind.Csc:
                    if (Math.Abs(Math.Sin(value)) < 1e-15)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = 1.0 / Math.Sin(value);
                    return true;
                case FunctionKind.Asin:
                    if (value < -1.0 || value > 1.0)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = Math.Asin(value);
                    return true;
                case FunctionKind.Acos:
                    if (value < -1.0 || value > 1.0)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = Math.Acos(value);
                    return true;
                case FunctionKind.Atan:
                    result = Math.Atan(value);
                    return true;
                case FunctionKind.Sinh:
                    result = Math.Sinh(value);
                    return true;
                case FunctionKind.Cosh:
                    result = Math.Cosh(value);
                    return true;
                case FunctionKind.Tanh:
                    result = Math.Tanh(value);
                    return true;
                case FunctionKind.Asinh:
                    result = Math.Asinh(value);
                    return true;
                case FunctionKind.Acosh:
                    if (value < 1.0)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = Math.Acosh(value);
                    return true;
                case FunctionKind.Atanh:
                    if (value <= -1.0 || value >= 1.0)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = Math.Atanh(value);
                    return true;
                case FunctionKind.Exp:
                    result = Math.Exp(value);
                    return true;
                case FunctionKind.Ln:
                    if (value <= 0.0)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = Math.Log(value);
                    return true;
                case FunctionKind.Log10:
                    if (value <= 0.0)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = Math.Log10(value);
                    return true;
                case FunctionKind.Sqrt:
                    if (value < 0.0)
                    {
                        result = 0.0;
                        return false;
                    }
                    result = Math.Sqrt(value);
                    return true;
                case FunctionKind.Cbrt:
                    result = Math.Cbrt(value);
                    return true;
                case FunctionKind.Abs:
                    result = Math.Abs(value);
                    return true;
                case FunctionKind.Sign:
                    result = Math.Sign(value);
                    return true;
                default:
                    result = 0.0;
                    return false;
            }
        }

        internal static Expression Differentiate(Expression expression, string variable)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentException("The variable name cannot be null or empty.", nameof(variable));
            }
            return Simplify(DifferentiateOnce(expression, variable));
        }

        internal static Expression Differentiate(Expression expression, string variable, int order)
        {
            if (order < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(order), "The derivative order must be at least one.");
            }
            Expression result = expression;
            for (int i = 0; i < order; i++)
            {
                result = Differentiate(result, variable);
            }
            return result;
        }

        private static Expression DifferentiateOnce(Expression expression, string variable)
        {
            switch (expression)
            {
                case ConstantExpression:
                    return ExpressionBuilder.Constant(0.0);
                case VariableExpression variableExpression:
                    return ExpressionBuilder.Constant(variableExpression.Name == variable ? 1.0 : 0.0);
                case NegateExpression negate:
                    return ExpressionBuilder.Negate(DifferentiateOnce(negate.Operand, variable));
                case BinaryExpression binary:
                {
                    Expression left = binary.Left;
                    Expression right = binary.Right;
                    Expression leftDerivative = DifferentiateOnce(left, variable);
                    Expression rightDerivative = DifferentiateOnce(right, variable);
                    switch (binary.Operator)
                    {
                        case BinaryOperator.Add:
                            return ExpressionBuilder.Add(leftDerivative, rightDerivative);
                        case BinaryOperator.Subtract:
                            return ExpressionBuilder.Subtract(leftDerivative, rightDerivative);
                        case BinaryOperator.Multiply:
                            return ExpressionBuilder.Add(
                                ExpressionBuilder.Multiply(leftDerivative, right),
                                ExpressionBuilder.Multiply(left, rightDerivative));
                        case BinaryOperator.Divide:
                            return ExpressionBuilder.Divide(
                                ExpressionBuilder.Subtract(
                                    ExpressionBuilder.Multiply(leftDerivative, right),
                                    ExpressionBuilder.Multiply(left, rightDerivative)),
                                ExpressionBuilder.Power(right, ExpressionBuilder.Constant(2.0)));
                        case BinaryOperator.Power:
                            if (!DependsOn(right, variable))
                            {
                                return ExpressionBuilder.Multiply(
                                    ExpressionBuilder.Multiply(
                                        right,
                                        ExpressionBuilder.Power(
                                            left,
                                            ExpressionBuilder.Subtract(right, ExpressionBuilder.Constant(1.0)))),
                                    leftDerivative);
                            }
                            return ExpressionBuilder.Multiply(
                                ExpressionBuilder.Power(left, right),
                                ExpressionBuilder.Add(
                                    ExpressionBuilder.Multiply(
                                        rightDerivative,
                                        ExpressionBuilder.Function(FunctionKind.Ln, left)),
                                    ExpressionBuilder.Divide(
                                        ExpressionBuilder.Multiply(right, leftDerivative),
                                        left)));
                        default:
                            return ExpressionBuilder.Constant(0.0);
                    }
                }
                case FunctionExpression function:
                {
                    Expression argument = function.Argument;
                    Expression argumentDerivative = DifferentiateOnce(argument, variable);
                    Expression sine = ExpressionBuilder.Function(FunctionKind.Sin, argument);
                    Expression cosine = ExpressionBuilder.Function(FunctionKind.Cos, argument);
                    switch (function.Function)
                    {
                        case FunctionKind.Sin:
                            return ExpressionBuilder.Multiply(cosine, argumentDerivative);
                        case FunctionKind.Cos:
                            return ExpressionBuilder.Negate(ExpressionBuilder.Multiply(sine, argumentDerivative));
                        case FunctionKind.Tan:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Power(cosine, ExpressionBuilder.Constant(2.0)));
                        case FunctionKind.Cot:
                            return ExpressionBuilder.Negate(ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Power(sine, ExpressionBuilder.Constant(2.0))));
                        case FunctionKind.Sec:
                            return ExpressionBuilder.Multiply(
                                ExpressionBuilder.Multiply(
                                    ExpressionBuilder.Function(FunctionKind.Sec, argument),
                                    ExpressionBuilder.Function(FunctionKind.Tan, argument)),
                                argumentDerivative);
                        case FunctionKind.Csc:
                            return ExpressionBuilder.Negate(ExpressionBuilder.Multiply(
                                ExpressionBuilder.Multiply(
                                    ExpressionBuilder.Function(FunctionKind.Csc, argument),
                                    ExpressionBuilder.Function(FunctionKind.Cot, argument)),
                                argumentDerivative));
                        case FunctionKind.Asin:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Function(
                                    FunctionKind.Sqrt,
                                    ExpressionBuilder.Subtract(
                                        ExpressionBuilder.Constant(1.0),
                                        ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0)))));
                        case FunctionKind.Acos:
                            return ExpressionBuilder.Negate(ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Function(
                                    FunctionKind.Sqrt,
                                    ExpressionBuilder.Subtract(
                                        ExpressionBuilder.Constant(1.0),
                                        ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0))))));
                        case FunctionKind.Atan:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Add(
                                    ExpressionBuilder.Constant(1.0),
                                    ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0))));
                        case FunctionKind.Sinh:
                            return ExpressionBuilder.Multiply(
                                ExpressionBuilder.Function(FunctionKind.Cosh, argument),
                                argumentDerivative);
                        case FunctionKind.Cosh:
                            return ExpressionBuilder.Multiply(
                                ExpressionBuilder.Function(FunctionKind.Sinh, argument),
                                argumentDerivative);
                        case FunctionKind.Tanh:
                            return ExpressionBuilder.Multiply(
                                argumentDerivative,
                                ExpressionBuilder.Subtract(
                                    ExpressionBuilder.Constant(1.0),
                                    ExpressionBuilder.Power(
                                        ExpressionBuilder.Function(FunctionKind.Tanh, argument),
                                        ExpressionBuilder.Constant(2.0))));
                        case FunctionKind.Asinh:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Function(
                                    FunctionKind.Sqrt,
                                    ExpressionBuilder.Add(
                                        ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0)),
                                        ExpressionBuilder.Constant(1.0))));
                        case FunctionKind.Acosh:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Function(
                                    FunctionKind.Sqrt,
                                    ExpressionBuilder.Subtract(
                                        ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0)),
                                        ExpressionBuilder.Constant(1.0))));
                        case FunctionKind.Atanh:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Subtract(
                                    ExpressionBuilder.Constant(1.0),
                                    ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0))));
                        case FunctionKind.Exp:
                            return ExpressionBuilder.Multiply(
                                ExpressionBuilder.Function(FunctionKind.Exp, argument),
                                argumentDerivative);
                        case FunctionKind.Ln:
                            return ExpressionBuilder.Divide(argumentDerivative, argument);
                        case FunctionKind.Log10:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Multiply(
                                    argument,
                                    ExpressionBuilder.Constant(Math.Log(10.0))));
                        case FunctionKind.Sqrt:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Multiply(
                                    ExpressionBuilder.Constant(2.0),
                                    ExpressionBuilder.Function(FunctionKind.Sqrt, argument)));
                        case FunctionKind.Cbrt:
                            return ExpressionBuilder.Divide(
                                argumentDerivative,
                                ExpressionBuilder.Multiply(
                                    ExpressionBuilder.Constant(3.0),
                                    ExpressionBuilder.Power(
                                        ExpressionBuilder.Function(FunctionKind.Cbrt, argument),
                                        ExpressionBuilder.Constant(2.0))));
                        case FunctionKind.Abs:
                            return ExpressionBuilder.Multiply(
                                ExpressionBuilder.Function(FunctionKind.Sign, argument),
                                argumentDerivative);
                        case FunctionKind.Sign:
                            return ExpressionBuilder.Constant(0.0);
                        default:
                            return ExpressionBuilder.Constant(0.0);
                    }
                }
                default:
                    return ExpressionBuilder.Constant(0.0);
            }
        }

        internal static bool TryIntegrate(Expression expression, string variable, out Expression? result)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentException("The variable name cannot be null or empty.", nameof(variable));
            }
            return TryIntegrateInternal(expression, variable, 0, out result);
        }

        private static bool TryIntegrateInternal(Expression expression, string variable, int depth, out Expression? result)
        {
            result = null;
            if (depth > MaxIntegrationDepth)
            {
                return false;
            }
            Expression integrand = Simplify(expression);
            if (!DependsOn(integrand, variable))
            {
                result = Simplify(ExpressionBuilder.Multiply(integrand, ExpressionBuilder.Variable(variable)));
                return true;
            }
            if (integrand is VariableExpression variableExpression && variableExpression.Name == variable)
            {
                result = ExpressionBuilder.Divide(
                    ExpressionBuilder.Power(
                        ExpressionBuilder.Variable(variable),
                        ExpressionBuilder.Constant(2.0)),
                    ExpressionBuilder.Constant(2.0));
                return true;
            }
            if (integrand is NegateExpression negate)
            {
                if (TryIntegrateInternal(negate.Operand, variable, depth + 1, out Expression? inner))
                {
                    result = Simplify(ExpressionBuilder.Negate(inner!));
                    return true;
                }
                return false;
            }
            if (integrand is BinaryExpression binary)
            {
                switch (binary.Operator)
                {
                    case BinaryOperator.Add:
                    case BinaryOperator.Subtract:
                    {
                        if (TryIntegrateInternal(binary.Left, variable, depth + 1, out Expression? left)
                            && TryIntegrateInternal(binary.Right, variable, depth + 1, out Expression? right))
                        {
                            result = Simplify(binary.Operator == BinaryOperator.Add
                                ? ExpressionBuilder.Add(left!, right!)
                                : ExpressionBuilder.Subtract(left!, right!));
                            return true;
                        }
                        return false;
                    }
                    case BinaryOperator.Multiply:
                        return TryIntegrateProduct(integrand, variable, depth, out result);
                    case BinaryOperator.Divide:
                        return TryIntegrateQuotient(integrand, variable, depth, out result);
                    case BinaryOperator.Power:
                        return TryIntegratePower(integrand, variable, depth, out result);
                    default:
                        return false;
                }
            }
            if (integrand is FunctionExpression function)
            {
                if (TryGetLinearArgument(function.Argument, variable, out double coefficient, out _)
                    && coefficient != 0.0
                    && TryIntegrateFunction(function.Function, function.Argument, coefficient, variable, out Expression linearResult))
                {
                    result = Simplify(linearResult);
                    return true;
                }
                return TrySubstitution(integrand, variable, depth, out result);
            }
            return false;
        }

        private static bool TryIntegrateProduct(Expression integrand, string variable, int depth, out Expression? result)
        {
            result = null;
            List<Expression> factors = new List<Expression>();
            FlattenProduct(integrand, factors);
            if (factors.Count == 0)
            {
                return false;
            }
            Expression constantFactor = ExpressionBuilder.Constant(1.0);
            List<Expression> dependentFactors = new List<Expression>();
            foreach (Expression factor in factors)
            {
                if (DependsOn(factor, variable))
                {
                    dependentFactors.Add(factor);
                }
                else
                {
                    constantFactor = ExpressionBuilder.Multiply(constantFactor, factor);
                }
            }
            constantFactor = Simplify(constantFactor);
            if (dependentFactors.Count == 0)
            {
                return false;
            }
            Expression dependentProduct = ProductOf(dependentFactors);
            if (!IsOne(constantFactor))
            {
                if (TryIntegrateInternal(dependentProduct, variable, depth + 1, out Expression? inner))
                {
                    result = Simplify(ExpressionBuilder.Multiply(constantFactor, inner!));
                    return true;
                }
            }
            if (TrySubstitution(integrand, variable, depth, out result))
            {
                return true;
            }
            if (TryIntegrateRational(integrand, variable, out result))
            {
                return true;
            }
            if (TryIntegrationByParts(integrand, variable, depth, out result))
            {
                return true;
            }
            return false;
        }

        private static bool TryIntegrateQuotient(Expression integrand, string variable, int depth, out Expression? result)
        {
            result = null;
            if (integrand is not BinaryExpression { Operator: BinaryOperator.Divide } quotient)
            {
                return false;
            }
            Expression numerator = quotient.Left;
            Expression denominator = quotient.Right;
            if (IsZero(numerator))
            {
                result = ExpressionBuilder.Constant(0.0);
                return true;
            }
            if (!DependsOn(denominator, variable))
            {
                if (TryIntegrateInternal(numerator, variable, depth + 1, out Expression? inner))
                {
                    result = Simplify(ExpressionBuilder.Divide(inner!, denominator));
                    return true;
                }
                return false;
            }
            if (!DependsOn(numerator, variable))
            {
                Expression reciprocal = ExpressionBuilder.Divide(ExpressionBuilder.Constant(1.0), denominator);
                if (TryIntegrateInternal(reciprocal, variable, depth + 1, out Expression? inner))
                {
                    result = Simplify(ExpressionBuilder.Multiply(numerator, inner!));
                    return true;
                }
                return false;
            }
            if (TryIntegrateRational(integrand, variable, out result))
            {
                return true;
            }
            Expression derivative = Simplify(Differentiate(denominator, variable));
            if (!IsZero(derivative))
            {
                Expression directRatio = Simplify(ExpressionBuilder.Divide(numerator, derivative));
                if (!DependsOn(directRatio, variable))
                {
                    result = Simplify(ExpressionBuilder.Multiply(
                        directRatio,
                        ExpressionBuilder.Function(
                            FunctionKind.Ln,
                            ExpressionBuilder.Function(FunctionKind.Abs, denominator))));
                    return true;
                }
            }
            if (TrySubstitution(integrand, variable, depth, out result))
            {
                return true;
            }
            return false;
        }

        private static bool TryIntegratePower(Expression integrand, string variable, int depth, out Expression? result)
        {
            result = null;
            if (integrand is not BinaryExpression { Operator: BinaryOperator.Power } power)
            {
                return false;
            }
            Expression baseExpression = power.Left;
            Expression exponent = power.Right;
            if (TryGetConstant(exponent, out double exponentValue))
            {
                Expression derivative = Simplify(Differentiate(baseExpression, variable));
                if (TryGetConstant(derivative, out double derivativeValue) && derivativeValue != 0.0)
                {
                    if (exponentValue == -1.0)
                    {
                        result = Simplify(ExpressionBuilder.Divide(
                            ExpressionBuilder.Function(
                                FunctionKind.Ln,
                                ExpressionBuilder.Function(FunctionKind.Abs, baseExpression)),
                            ExpressionBuilder.Constant(derivativeValue)));
                        return true;
                    }
                    result = Simplify(ExpressionBuilder.Divide(
                        ExpressionBuilder.Power(
                            baseExpression,
                            ExpressionBuilder.Constant(exponentValue + 1.0)),
                        ExpressionBuilder.Constant(derivativeValue * (exponentValue + 1.0))));
                    return true;
                }
            }
            else if (TryGetConstant(baseExpression, out double baseValue) && baseValue > 0.0 && baseValue != 1.0)
            {
                Expression derivative = Simplify(Differentiate(exponent, variable));
                if (TryGetConstant(derivative, out double derivativeValue) && derivativeValue != 0.0)
                {
                    result = Simplify(ExpressionBuilder.Divide(
                        ExpressionBuilder.Power(baseExpression, exponent),
                        ExpressionBuilder.Constant(Math.Log(baseValue) * derivativeValue)));
                    return true;
                }
            }
            if (TrySubstitution(integrand, variable, depth, out result))
            {
                return true;
            }
            return TryIntegrateRational(integrand, variable, out result);
        }

        private static bool TryIntegrateFunction(
            FunctionKind function,
            Expression argument,
            double coefficient,
            string variable,
            out Expression result)
        {
            result = ExpressionBuilder.Constant(0.0);
            Expression absArgument = ExpressionBuilder.Function(FunctionKind.Abs, argument);
            switch (function)
            {
                case FunctionKind.Sin:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Negate(ExpressionBuilder.Function(FunctionKind.Cos, argument)),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Cos:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Function(FunctionKind.Sin, argument),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Tan:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Negate(ExpressionBuilder.Function(
                            FunctionKind.Ln,
                            ExpressionBuilder.Function(FunctionKind.Abs, ExpressionBuilder.Function(FunctionKind.Cos, argument)))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Cot:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Function(
                            FunctionKind.Ln,
                            ExpressionBuilder.Function(FunctionKind.Abs, ExpressionBuilder.Function(FunctionKind.Sin, argument))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Sec:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Function(
                            FunctionKind.Ln,
                            ExpressionBuilder.Function(
                                FunctionKind.Abs,
                                ExpressionBuilder.Add(
                                    ExpressionBuilder.Function(FunctionKind.Sec, argument),
                                    ExpressionBuilder.Function(FunctionKind.Tan, argument)))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Csc:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Negate(ExpressionBuilder.Function(
                            FunctionKind.Ln,
                            ExpressionBuilder.Function(
                                FunctionKind.Abs,
                                ExpressionBuilder.Add(
                                    ExpressionBuilder.Function(FunctionKind.Csc, argument),
                                    ExpressionBuilder.Function(FunctionKind.Cot, argument))))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Asin:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Add(
                            ExpressionBuilder.Multiply(
                                argument,
                                ExpressionBuilder.Function(FunctionKind.Asin, argument)),
                            ExpressionBuilder.Function(
                                FunctionKind.Sqrt,
                                ExpressionBuilder.Subtract(
                                    ExpressionBuilder.Constant(1.0),
                                    ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0))))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Acos:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Subtract(
                            ExpressionBuilder.Multiply(
                                argument,
                                ExpressionBuilder.Function(FunctionKind.Acos, argument)),
                            ExpressionBuilder.Function(
                                FunctionKind.Sqrt,
                                ExpressionBuilder.Subtract(
                                    ExpressionBuilder.Constant(1.0),
                                    ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0))))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Atan:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Subtract(
                            ExpressionBuilder.Multiply(
                                argument,
                                ExpressionBuilder.Function(FunctionKind.Atan, argument)),
                            ExpressionBuilder.Multiply(
                                ExpressionBuilder.Constant(0.5),
                                ExpressionBuilder.Function(
                                    FunctionKind.Ln,
                                    ExpressionBuilder.Add(
                                        ExpressionBuilder.Constant(1.0),
                                        ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0)))))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Sinh:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Function(FunctionKind.Cosh, argument),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Cosh:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Function(FunctionKind.Sinh, argument),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Tanh:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Function(
                            FunctionKind.Ln,
                            ExpressionBuilder.Function(FunctionKind.Cosh, argument)),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Asinh:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Subtract(
                            ExpressionBuilder.Multiply(
                                argument,
                                ExpressionBuilder.Function(FunctionKind.Asinh, argument)),
                            ExpressionBuilder.Function(
                                FunctionKind.Sqrt,
                                ExpressionBuilder.Add(
                                    ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0)),
                                    ExpressionBuilder.Constant(1.0)))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Acosh:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Subtract(
                            ExpressionBuilder.Multiply(
                                argument,
                                ExpressionBuilder.Function(FunctionKind.Acosh, argument)),
                            ExpressionBuilder.Function(
                                FunctionKind.Sqrt,
                                ExpressionBuilder.Subtract(
                                    ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0)),
                                    ExpressionBuilder.Constant(1.0)))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Atanh:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Add(
                            ExpressionBuilder.Multiply(
                                argument,
                                ExpressionBuilder.Function(FunctionKind.Atanh, argument)),
                            ExpressionBuilder.Multiply(
                                ExpressionBuilder.Constant(0.5),
                                ExpressionBuilder.Function(
                                    FunctionKind.Ln,
                                    ExpressionBuilder.Subtract(
                                        ExpressionBuilder.Constant(1.0),
                                        ExpressionBuilder.Power(argument, ExpressionBuilder.Constant(2.0)))))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Exp:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Function(FunctionKind.Exp, argument),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Ln:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Subtract(
                            ExpressionBuilder.Multiply(argument, ExpressionBuilder.Function(FunctionKind.Ln, argument)),
                            argument),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Log10:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Subtract(
                            ExpressionBuilder.Multiply(argument, ExpressionBuilder.Function(FunctionKind.Ln, argument)),
                            argument),
                        ExpressionBuilder.Constant(coefficient * Math.Log(10.0)));
                    return true;
                case FunctionKind.Sqrt:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Multiply(
                            ExpressionBuilder.Constant(2.0 / 3.0),
                            ExpressionBuilder.Power(
                                argument,
                                ExpressionBuilder.Constant(1.5))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Cbrt:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Multiply(
                            ExpressionBuilder.Constant(3.0 / 4.0),
                            ExpressionBuilder.Power(
                                argument,
                                ExpressionBuilder.Constant(4.0 / 3.0))),
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                case FunctionKind.Abs:
                    result = ExpressionBuilder.Divide(
                        ExpressionBuilder.Multiply(
                            argument,
                            ExpressionBuilder.Function(FunctionKind.Abs, argument)),
                        ExpressionBuilder.Constant(2.0 * coefficient));
                    return true;
                case FunctionKind.Sign:
                    result = ExpressionBuilder.Divide(
                        absArgument,
                        ExpressionBuilder.Constant(coefficient));
                    return true;
                default:
                    return false;
            }
        }

        private static bool TrySubstitution(Expression integrand, string variable, int depth, out Expression? result)
        {
            result = null;
            List<Expression> candidates = new List<Expression>();
            CollectCandidates(integrand, variable, candidates);
            foreach (Expression candidate in candidates)
            {
                if (candidate is VariableExpression candidateVariable && candidateVariable.Name == variable)
                {
                    continue;
                }
                Expression candidateDerivative = Simplify(Differentiate(candidate, variable));
                if (IsZero(candidateDerivative))
                {
                    continue;
                }
                Expression quotient = Simplify(ExpressionBuilder.Divide(integrand, candidateDerivative));
                Expression replaced = ReplaceExpression(
                    quotient,
                    candidate,
                    ExpressionBuilder.Variable(SubstitutionVariable));
                if (DependsOn(replaced, variable))
                {
                    continue;
                }
                if (TryIntegrateInternal(replaced, SubstitutionVariable, depth + 1, out Expression? inner))
                {
                    Expression back = ReplaceExpression(
                        inner!,
                        ExpressionBuilder.Variable(SubstitutionVariable),
                        candidate);
                    result = Simplify(back);
                    return true;
                }
            }
            return false;
        }

        private static bool TryIntegrationByParts(Expression integrand, string variable, int depth, out Expression? result)
        {
            result = null;
            if (depth > MaxIntegrationDepth)
            {
                return false;
            }
            List<Expression> factors = new List<Expression>();
            FlattenProduct(integrand, factors);
            if (factors.Count < 2)
            {
                return false;
            }
            List<int> order = new List<int>();
            for (int i = 0; i < factors.Count; i++)
            {
                if (DependsOn(factors[i], variable))
                {
                    order.Add(i);
                }
            }
            order.Sort((leftIndex, rightIndex) => PartsPriority(factors[rightIndex], variable).CompareTo(PartsPriority(factors[leftIndex], variable)));
            foreach (int index in order)
            {
                Expression u = factors[index];
                List<Expression> remainingFactors = new List<Expression>();
                for (int i = 0; i < factors.Count; i++)
                {
                    if (i != index)
                    {
                        remainingFactors.Add(factors[i]);
                    }
                }
                Expression dv = ProductOf(remainingFactors);
                Expression du = Simplify(Differentiate(u, variable));
                if (IsZero(du))
                {
                    continue;
                }
                if (!TryIntegrateInternal(dv, variable, depth + 1, out Expression? v))
                {
                    continue;
                }
                Expression remainingIntegrand = Simplify(ExpressionBuilder.Multiply(v!, du));
                if (IsZero(remainingIntegrand))
                {
                    result = Simplify(ExpressionBuilder.Multiply(u, v!));
                    return true;
                }
                if (!TryIntegrateInternal(remainingIntegrand, variable, depth + 1, out Expression? remainingIntegral))
                {
                    continue;
                }
                result = Simplify(ExpressionBuilder.Subtract(
                    ExpressionBuilder.Multiply(u, v!),
                    remainingIntegral!));
                return true;
            }
            return false;
        }

        private static int PartsPriority(Expression expression, string variable)
        {
            if (expression is FunctionExpression function)
            {
                switch (function.Function)
                {
                    case FunctionKind.Ln:
                    case FunctionKind.Log10:
                    case FunctionKind.Asin:
                    case FunctionKind.Acos:
                    case FunctionKind.Atan:
                    case FunctionKind.Asinh:
                    case FunctionKind.Acosh:
                    case FunctionKind.Atanh:
                        return 5;
                    case FunctionKind.Sin:
                    case FunctionKind.Cos:
                    case FunctionKind.Tan:
                    case FunctionKind.Cot:
                    case FunctionKind.Sec:
                    case FunctionKind.Csc:
                        return 3;
                    case FunctionKind.Sinh:
                    case FunctionKind.Cosh:
                    case FunctionKind.Tanh:
                        return 2;
                    case FunctionKind.Exp:
                        return 1;
                }
            }
            if (expression is VariableExpression variableExpression && variableExpression.Name == variable)
            {
                return 4;
            }
            if (expression is BinaryExpression { Operator: BinaryOperator.Power } power
                && !DependsOn(power.Right, variable))
            {
                return 4;
            }
            return 0;
        }

        private static void FlattenProduct(Expression expression, List<Expression> factors)
        {
            if (expression is BinaryExpression { Operator: BinaryOperator.Multiply } product)
            {
                FlattenProduct(product.Left, factors);
                FlattenProduct(product.Right, factors);
                return;
            }
            factors.Add(expression);
        }

        private static Expression ProductOf(List<Expression> factors)
        {
            if (factors.Count == 0)
            {
                return ExpressionBuilder.Constant(1.0);
            }
            Expression result = factors[0];
            for (int i = 1; i < factors.Count; i++)
            {
                result = ExpressionBuilder.Multiply(result, factors[i]);
            }
            return result;
        }

        private static void CollectCandidates(Expression expression, string variable, List<Expression> candidates)
        {
            switch (expression)
            {
                case ConstantExpression:
                case VariableExpression:
                    return;
                case NegateExpression negate:
                    if (DependsOn(negate, variable) && !ContainsEquivalent(candidates, negate))
                    {
                        candidates.Add(negate);
                    }
                    CollectCandidates(negate.Operand, variable, candidates);
                    return;
                case BinaryExpression binary:
                    if (DependsOn(binary, variable) && !ContainsEquivalent(candidates, binary))
                    {
                        candidates.Add(binary);
                    }
                    CollectCandidates(binary.Left, variable, candidates);
                    CollectCandidates(binary.Right, variable, candidates);
                    return;
                case FunctionExpression function:
                    if (DependsOn(function, variable) && !ContainsEquivalent(candidates, function))
                    {
                        candidates.Add(function);
                    }
                    CollectCandidates(function.Argument, variable, candidates);
                    return;
            }
        }

        private static bool ContainsEquivalent(List<Expression> expressions, Expression target)
        {
            foreach (Expression expression in expressions)
            {
                if (expression.Equals(target))
                {
                    return true;
                }
            }
            return false;
        }

        private static Expression ReplaceExpression(Expression expression, Expression target, Expression replacement)
        {
            if (expression.Equals(target))
            {
                return replacement;
            }
            switch (expression)
            {
                case NegateExpression negate:
                    return ExpressionBuilder.Negate(ReplaceExpression(negate.Operand, target, replacement));
                case BinaryExpression binary:
                    return new BinaryExpression(
                        binary.Operator,
                        ReplaceExpression(binary.Left, target, replacement),
                        ReplaceExpression(binary.Right, target, replacement));
                case FunctionExpression function:
                    return ExpressionBuilder.Function(
                        function.Function,
                        ReplaceExpression(function.Argument, target, replacement));
                default:
                    return expression;
            }
        }

        internal static bool DependsOn(Expression expression, string variable)
        {
            switch (expression)
            {
                case ConstantExpression:
                    return false;
                case VariableExpression variableExpression:
                    return variableExpression.Name == variable;
                case NegateExpression negate:
                    return DependsOn(negate.Operand, variable);
                case BinaryExpression binary:
                    return DependsOn(binary.Left, variable) || DependsOn(binary.Right, variable);
                case FunctionExpression function:
                    return DependsOn(function.Argument, variable);
                default:
                    return false;
            }
        }

        private static bool TryGetLinearArgument(Expression argument, string variable, out double coefficient, out double constant)
        {
            coefficient = 0.0;
            constant = 0.0;
            if (!TryGetPolynomial(argument, variable, out double[] coefficients))
            {
                return false;
            }
            if (coefficients.Length > 2)
            {
                return false;
            }
            constant = coefficients.Length > 0 ? coefficients[0] : 0.0;
            coefficient = coefficients.Length > 1 ? coefficients[1] : 0.0;
            return true;
        }

        internal static bool TryGetPolynomial(Expression expression, string variable, out double[] coefficients)
        {
            switch (expression)
            {
                case ConstantExpression constant:
                    coefficients = new[] { constant.Value };
                    return true;
                case VariableExpression variableExpression:
                    if (variableExpression.Name == variable)
                    {
                        coefficients = new[] { 0.0, 1.0 };
                        return true;
                    }
                    coefficients = Array.Empty<double>();
                    return false;
                case NegateExpression negate:
                {
                    if (!TryGetPolynomial(negate.Operand, variable, out double[] inner))
                    {
                        coefficients = Array.Empty<double>();
                        return false;
                    }
                    coefficients = PolynomialScale(inner, -1.0);
                    return true;
                }
                case BinaryExpression binary:
                {
                    if (!TryGetPolynomial(binary.Left, variable, out double[] left)
                        || !TryGetPolynomial(binary.Right, variable, out double[] right))
                    {
                        coefficients = Array.Empty<double>();
                        return false;
                    }
                    switch (binary.Operator)
                    {
                        case BinaryOperator.Add:
                            coefficients = PolynomialAdd(left, right);
                            return true;
                        case BinaryOperator.Subtract:
                            coefficients = PolynomialSubtract(left, right);
                            return true;
                        case BinaryOperator.Multiply:
                            coefficients = PolynomialMultiply(left, right);
                            return true;
                        case BinaryOperator.Divide:
                            if (right.Length != 1 || right[0] == 0.0)
                            {
                                coefficients = Array.Empty<double>();
                                return false;
                            }
                            coefficients = PolynomialScale(left, 1.0 / right[0]);
                            return true;
                        case BinaryOperator.Power:
                            if (right.Length != 1
                                || right[0] < 0.0
                                || right[0] > MaxPolynomialPower
                                || Math.Abs(right[0] - Math.Round(right[0])) > Tolerance)
                            {
                                coefficients = Array.Empty<double>();
                                return false;
                            }
                            coefficients = PolynomialPower(left, (int)Math.Round(right[0]));
                            return true;
                    }
                    coefficients = Array.Empty<double>();
                    return false;
                }
                default:
                    coefficients = Array.Empty<double>();
                    return false;
            }
        }

        private static bool TryGetRational(Expression expression, string variable, out double[] numerator, out double[] denominator)
        {
            switch (expression)
            {
                case ConstantExpression constant:
                    numerator = new[] { constant.Value };
                    denominator = new[] { 1.0 };
                    return true;
                case VariableExpression variableExpression:
                    if (variableExpression.Name == variable)
                    {
                        numerator = new[] { 0.0, 1.0 };
                        denominator = new[] { 1.0 };
                        return true;
                    }
                    numerator = Array.Empty<double>();
                    denominator = Array.Empty<double>();
                    return false;
                case NegateExpression negate:
                {
                    if (!TryGetRational(negate.Operand, variable, out double[] innerNumerator, out double[] innerDenominator))
                    {
                        numerator = Array.Empty<double>();
                        denominator = Array.Empty<double>();
                        return false;
                    }
                    numerator = PolynomialScale(innerNumerator, -1.0);
                    denominator = innerDenominator;
                    return true;
                }
                case BinaryExpression binary:
                {
                    if (!TryGetRational(binary.Left, variable, out double[] leftNumerator, out double[] leftDenominator)
                        || !TryGetRational(binary.Right, variable, out double[] rightNumerator, out double[] rightDenominator))
                    {
                        numerator = Array.Empty<double>();
                        denominator = Array.Empty<double>();
                        return false;
                    }
                    switch (binary.Operator)
                    {
                        case BinaryOperator.Add:
                            numerator = PolynomialAdd(
                                PolynomialMultiply(leftNumerator, rightDenominator),
                                PolynomialMultiply(rightNumerator, leftDenominator));
                            denominator = PolynomialMultiply(leftDenominator, rightDenominator);
                            return true;
                        case BinaryOperator.Subtract:
                            numerator = PolynomialSubtract(
                                PolynomialMultiply(leftNumerator, rightDenominator),
                                PolynomialMultiply(rightNumerator, leftDenominator));
                            denominator = PolynomialMultiply(leftDenominator, rightDenominator);
                            return true;
                        case BinaryOperator.Multiply:
                            numerator = PolynomialMultiply(leftNumerator, rightNumerator);
                            denominator = PolynomialMultiply(leftDenominator, rightDenominator);
                            return true;
                        case BinaryOperator.Divide:
                            numerator = PolynomialMultiply(leftNumerator, rightDenominator);
                            denominator = PolynomialMultiply(leftDenominator, rightNumerator);
                            return true;
                        case BinaryOperator.Power:
                        {
                            if (rightDenominator.Length != 1
                                || rightDenominator[0] == 0.0
                                || rightNumerator.Length != 1
                                || Math.Abs(rightNumerator[0] - Math.Round(rightNumerator[0])) > Tolerance
                                || Math.Abs(rightNumerator[0]) > MaxPolynomialPower)
                            {
                                numerator = Array.Empty<double>();
                                denominator = Array.Empty<double>();
                                return false;
                            }
                            int power = (int)Math.Round(rightNumerator[0]);
                            if (power >= 0)
                            {
                                numerator = PolynomialPower(leftNumerator, power);
                                denominator = PolynomialPower(leftDenominator, power);
                            }
                            else
                            {
                                numerator = PolynomialPower(leftDenominator, -power);
                                denominator = PolynomialPower(leftNumerator, -power);
                            }
                            return true;
                        }
                    }
                    numerator = Array.Empty<double>();
                    denominator = Array.Empty<double>();
                    return false;
                }
                default:
                    numerator = Array.Empty<double>();
                    denominator = Array.Empty<double>();
                    return false;
            }
        }

        private static bool TryIntegrateRational(Expression integrand, string variable, out Expression? result)
        {
            result = null;
            if (!TryGetRational(integrand, variable, out double[] numerator, out double[] denominator))
            {
                return false;
            }
            numerator = TrimPolynomial(numerator);
            denominator = TrimPolynomial(denominator);
            if (IsZeroPolynomial(denominator))
            {
                return false;
            }
            if (denominator.Length == 1)
            {
                double scale = denominator[0];
                result = Simplify(IntegratePolynomial(PolynomialScale(numerator, 1.0 / scale), variable));
                return true;
            }
            if (!PolynomialDivide(numerator, denominator, out double[] quotient, out double[] remainder))
            {
                return false;
            }
            Expression polynomialPart = IntegratePolynomial(quotient, variable);
            if (IsZeroPolynomial(remainder))
            {
                result = Simplify(polynomialPart);
                return true;
            }
            if (!TryFactorPolynomial(denominator, out List<RationalFactor> factors))
            {
                return false;
            }
            if (!TryPartialFractions(remainder, denominator, factors, variable, out Expression? fractionPart))
            {
                return false;
            }
            result = Simplify(ExpressionBuilder.Add(polynomialPart, fractionPart!));
            return true;
        }

        private sealed class RationalFactor
        {
            internal RationalFactor(double[] polynomial, int multiplicity)
            {
                Polynomial = polynomial;
                Multiplicity = multiplicity;
            }

            internal double[] Polynomial { get; }

            internal int Multiplicity { get; }
        }

        private sealed class PartialFractionTerm
        {
            internal PartialFractionTerm(RationalFactor factor, int power, bool isQuadratic, int coefficientIndex, int constantIndex)
            {
                Factor = factor;
                Power = power;
                IsQuadratic = isQuadratic;
                CoefficientIndex = coefficientIndex;
                ConstantIndex = constantIndex;
            }

            internal RationalFactor Factor { get; }

            internal int Power { get; }

            internal bool IsQuadratic { get; }

            internal int CoefficientIndex { get; }

            internal int ConstantIndex { get; }
        }

        private static bool TryFactorPolynomial(double[] polynomial, out List<RationalFactor> factors)
        {
            factors = new List<RationalFactor>();
            return TryFactorPolynomialRecursive(TrimPolynomial(polynomial), factors);
        }

        private static bool TryFactorPolynomialRecursive(double[] polynomial, List<RationalFactor> factors)
        {
            polynomial = TrimPolynomial(polynomial);
            int degree = polynomial.Length - 1;
            if (degree <= 0)
            {
                return true;
            }
            if (degree == 1)
            {
                factors.Add(new RationalFactor(polynomial, 1));
                return true;
            }
            if (degree == 2)
            {
                double a = polynomial[2];
                double b = polynomial[1];
                double c = polynomial[0];
                double discriminant = (b * b) - (4.0 * a * c);
                if (discriminant > 1e-8)
                {
                    double root = Math.Sqrt(discriminant);
                    double root1 = (-b + root) / (2.0 * a);
                    double root2 = (-b - root) / (2.0 * a);
                    factors.Add(new RationalFactor(new[] { -root1, 1.0 }, 1));
                    factors.Add(new RationalFactor(new[] { -root2, 1.0 }, 1));
                    return true;
                }
                if (discriminant >= -1e-8)
                {
                    double root = -b / (2.0 * a);
                    factors.Add(new RationalFactor(new[] { -root, 1.0 }, 2));
                    return true;
                }
                factors.Add(new RationalFactor(polynomial, 1));
                return true;
            }
            if (!TryFindRationalRoot(polynomial, out double rationalRoot))
            {
                return false;
            }
            if (!PolynomialDivideByLinear(polynomial, rationalRoot, out double[] quotient))
            {
                return false;
            }
            int multiplicity = 1;
            while (quotient.Length > 1 && PolynomialDivideByLinear(quotient, rationalRoot, out double[] nextQuotient))
            {
                quotient = nextQuotient;
                multiplicity++;
            }
            factors.Add(new RationalFactor(new[] { -rationalRoot, 1.0 }, multiplicity));
            return TryFactorPolynomialRecursive(quotient, factors);
        }

        private static bool TryFindRationalRoot(double[] polynomial, out double root)
        {
            root = 0.0;
            foreach (double coefficient in polynomial)
            {
                if (Math.Abs(coefficient - Math.Round(coefficient)) > 1e-7)
                {
                    return false;
                }
            }
            long constantTerm = (long)Math.Round(Math.Abs(polynomial[0]));
            long leadingTerm = (long)Math.Round(Math.Abs(polynomial[polynomial.Length - 1]));
            if (constantTerm == 0L)
            {
                root = 0.0;
                return true;
            }
            if (constantTerm > 100000L || leadingTerm > 100000L)
            {
                return false;
            }
            List<long> numeratorDivisors = GetDivisors(constantTerm);
            List<long> denominatorDivisors = GetDivisors(leadingTerm);
            foreach (long numeratorDivisor in numeratorDivisors)
            {
                foreach (long denominatorDivisor in denominatorDivisors)
                {
                    double candidate = (double)numeratorDivisor / denominatorDivisor;
                    if (Math.Abs(PolynomialEvaluate(polynomial, candidate)) < 1e-8)
                    {
                        root = candidate;
                        return true;
                    }
                    if (Math.Abs(PolynomialEvaluate(polynomial, -candidate)) < 1e-8)
                    {
                        root = -candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        private static List<long> GetDivisors(long value)
        {
            List<long> divisors = new List<long>();
            if (value <= 0L)
            {
                return divisors;
            }
            for (long divisor = 1L; divisor * divisor <= value; divisor++)
            {
                if (value % divisor == 0L)
                {
                    divisors.Add(divisor);
                    if (divisor != value / divisor)
                    {
                        divisors.Add(value / divisor);
                    }
                }
            }
            return divisors;
        }

        private static bool TryPartialFractions(
            double[] numerator,
            double[] denominator,
            List<RationalFactor> factors,
            string variable,
            out Expression? result)
        {
            result = null;
            List<PartialFractionTerm> terms = new List<PartialFractionTerm>();
            int unknownCount = 0;
            foreach (RationalFactor factor in factors)
            {
                if (factor.Polynomial.Length == 2)
                {
                    for (int power = 1; power <= factor.Multiplicity; power++)
                    {
                        terms.Add(new PartialFractionTerm(factor, power, false, unknownCount, -1));
                        unknownCount++;
                    }
                }
                else if (factor.Polynomial.Length == 3)
                {
                    for (int power = 1; power <= factor.Multiplicity; power++)
                    {
                        terms.Add(new PartialFractionTerm(factor, power, true, unknownCount, unknownCount + 1));
                        unknownCount += 2;
                    }
                }
                else
                {
                    return false;
                }
            }
            int equationCount = denominator.Length - 1;
            if (unknownCount != equationCount)
            {
                return false;
            }
            double[,] matrix = new double[equationCount, unknownCount];
            double[] rightHandSide = new double[equationCount];
            for (int i = 0; i < equationCount; i++)
            {
                rightHandSide[i] = i < numerator.Length ? numerator[i] : 0.0;
            }
            foreach (PartialFractionTerm term in terms)
            {
                double[] factorPower = PolynomialPower(term.Factor.Polynomial, term.Power);
                if (!PolynomialDivide(denominator, factorPower, out double[] quotient, out double[] remainder))
                {
                    return false;
                }
                if (!IsZeroPolynomial(remainder))
                {
                    return false;
                }
                double[] basis = term.IsQuadratic ? new[] { 0.0, 1.0 } : new[] { 1.0 };
                double[] termPolynomial = PolynomialMultiply(quotient, basis);
                for (int i = 0; i < termPolynomial.Length; i++)
                {
                    if (i < equationCount)
                    {
                        matrix[i, term.CoefficientIndex] += termPolynomial[i];
                    }
                    else if (Math.Abs(termPolynomial[i]) > 1e-8)
                    {
                        return false;
                    }
                }
                if (term.IsQuadratic)
                {
                    double[] constantBasis = new[] { 1.0 };
                    double[] constantPolynomial = PolynomialMultiply(quotient, constantBasis);
                    for (int i = 0; i < constantPolynomial.Length; i++)
                    {
                        if (i < equationCount)
                        {
                            matrix[i, term.ConstantIndex] += constantPolynomial[i];
                        }
                        else if (Math.Abs(constantPolynomial[i]) > 1e-8)
                        {
                            return false;
                        }
                    }
                }
            }
            if (!SolveLinearSystem(matrix, rightHandSide, out double[] solution))
            {
                return false;
            }
            Expression total = ExpressionBuilder.Constant(0.0);
            foreach (PartialFractionTerm term in terms)
            {
                if (term.IsQuadratic)
                {
                    if (!TryIntegrateQuadraticFactor(
                        term.Factor.Polynomial,
                        term.Power,
                        solution[term.CoefficientIndex],
                        solution[term.ConstantIndex],
                        variable,
                        out Expression? termResult))
                    {
                        return false;
                    }
                    total = ExpressionBuilder.Add(total, termResult!);
                }
                else
                {
                    total = ExpressionBuilder.Add(
                        total,
                        IntegrateLinearFactor(
                            term.Factor.Polynomial,
                            term.Power,
                            solution[term.CoefficientIndex],
                            variable));
                }
            }
            result = Simplify(total);
            return true;
        }

        private static Expression IntegrateLinearFactor(double[] factor, int power, double coefficient, string variable)
        {
            double a = factor[1];
            double b = factor[0];
            Expression linear = ExpressionBuilder.Add(
                ExpressionBuilder.Multiply(ExpressionBuilder.Constant(a), ExpressionBuilder.Variable(variable)),
                ExpressionBuilder.Constant(b));
            if (coefficient == 0.0 || a == 0.0)
            {
                return ExpressionBuilder.Constant(0.0);
            }
            if (power == 1)
            {
                return ExpressionBuilder.Multiply(
                    ExpressionBuilder.Constant(coefficient / a),
                    ExpressionBuilder.Function(
                        FunctionKind.Ln,
                        ExpressionBuilder.Function(FunctionKind.Abs, linear)));
            }
            return ExpressionBuilder.Multiply(
                ExpressionBuilder.Constant(coefficient / (a * (1.0 - power))),
                ExpressionBuilder.Power(linear, ExpressionBuilder.Constant(1.0 - power)));
        }

        private static bool TryIntegrateQuadraticFactor(
            double[] factor,
            int power,
            double coefficientX,
            double coefficientConstant,
            string variable,
            out Expression? result)
        {
            result = null;
            double c = factor[0];
            double b = factor[1];
            double a = factor[2];
            if (a == 0.0)
            {
                return false;
            }
            double discriminant = (4.0 * a * c) - (b * b);
            if (discriminant <= 0.0)
            {
                return false;
            }
            Expression quadratic = ExpressionBuilder.Add(
                ExpressionBuilder.Add(
                    ExpressionBuilder.Multiply(
                        ExpressionBuilder.Constant(a),
                        ExpressionBuilder.Power(ExpressionBuilder.Variable(variable), ExpressionBuilder.Constant(2.0))),
                    ExpressionBuilder.Multiply(
                        ExpressionBuilder.Constant(b),
                        ExpressionBuilder.Variable(variable))),
                ExpressionBuilder.Constant(c));
            double derivativeCoefficient = coefficientX / (2.0 * a);
            double constantCoefficient = coefficientConstant - (coefficientX * b / (2.0 * a));
            Expression derivativePart;
            if (power == 1)
            {
                derivativePart = ExpressionBuilder.Multiply(
                    ExpressionBuilder.Constant(derivativeCoefficient),
                    ExpressionBuilder.Function(
                        FunctionKind.Ln,
                        ExpressionBuilder.Function(FunctionKind.Abs, quadratic)));
            }
            else
            {
                derivativePart = ExpressionBuilder.Multiply(
                    ExpressionBuilder.Constant(derivativeCoefficient / (1.0 - power)),
                    ExpressionBuilder.Power(quadratic, ExpressionBuilder.Constant(1.0 - power)));
            }
            Expression quadraticReciprocal = BuildQuadraticReciprocalIntegral(a, b, c, power, variable);
            Expression constantPart = ExpressionBuilder.Multiply(
                ExpressionBuilder.Constant(constantCoefficient),
                quadraticReciprocal);
            result = ExpressionBuilder.Add(derivativePart, constantPart);
            return true;
        }

        private static Expression BuildQuadraticReciprocalIntegral(double a, double b, double c, int power, string variable)
        {
            double q = (c / a) - ((b * b) / (4.0 * a * a));
            Expression u = ExpressionBuilder.Add(
                ExpressionBuilder.Variable(variable),
                ExpressionBuilder.Constant(b / (2.0 * a)));
            Expression raw = BuildQuadraticReciprocalRaw(q, power, u);
            return ExpressionBuilder.Multiply(
                ExpressionBuilder.Constant(1.0 / Math.Pow(a, power)),
                raw);
        }

        private static Expression BuildQuadraticReciprocalRaw(double q, int power, Expression u)
        {
            double rootQ = Math.Sqrt(q);
            if (power == 1)
            {
                return ExpressionBuilder.Multiply(
                    ExpressionBuilder.Constant(1.0 / rootQ),
                    ExpressionBuilder.Function(
                        FunctionKind.Atan,
                        ExpressionBuilder.Divide(u, ExpressionBuilder.Constant(rootQ))));
            }
            Expression uSquaredPlusQ = ExpressionBuilder.Add(
                ExpressionBuilder.Power(u, ExpressionBuilder.Constant(2.0)),
                ExpressionBuilder.Constant(q));
            Expression firstTerm = ExpressionBuilder.Divide(
                u,
                ExpressionBuilder.Multiply(
                    ExpressionBuilder.Constant(2.0 * q * (power - 1)),
                    ExpressionBuilder.Power(uSquaredPlusQ, ExpressionBuilder.Constant(power - 1))));
            Expression secondTerm = ExpressionBuilder.Multiply(
                ExpressionBuilder.Constant((2.0 * power - 3.0) / (2.0 * q * (power - 1))),
                BuildQuadraticReciprocalRaw(q, power - 1, u));
            return ExpressionBuilder.Add(firstTerm, secondTerm);
        }

        private static bool SolveLinearSystem(double[,] matrix, double[] rightHandSide, out double[] solution)
        {
            int size = rightHandSide.Length;
            solution = new double[size];
            double[,] working = (double[,])matrix.Clone();
            double[] rhs = (double[])rightHandSide.Clone();
            for (int column = 0; column < size; column++)
            {
                int pivotRow = column;
                double pivotValue = Math.Abs(working[column, column]);
                for (int row = column + 1; row < size; row++)
                {
                    double candidate = Math.Abs(working[row, column]);
                    if (candidate > pivotValue)
                    {
                        pivotValue = candidate;
                        pivotRow = row;
                    }
                }
                if (pivotValue < 1e-10)
                {
                    return false;
                }
                if (pivotRow != column)
                {
                    for (int j = 0; j < size; j++)
                    {
                        (working[column, j], working[pivotRow, j]) = (working[pivotRow, j], working[column, j]);
                    }
                    (rhs[column], rhs[pivotRow]) = (rhs[pivotRow], rhs[column]);
                }
                double divisor = working[column, column];
                for (int row = column + 1; row < size; row++)
                {
                    double factor = working[row, column] / divisor;
                    if (factor == 0.0)
                    {
                        continue;
                    }
                    for (int j = column; j < size; j++)
                    {
                        working[row, j] -= factor * working[column, j];
                    }
                    rhs[row] -= factor * rhs[column];
                }
            }
            for (int row = size - 1; row >= 0; row--)
            {
                double sum = rhs[row];
                for (int column = row + 1; column < size; column++)
                {
                    sum -= working[row, column] * solution[column];
                }
                if (Math.Abs(working[row, row]) < 1e-12)
                {
                    return false;
                }
                solution[row] = sum / working[row, row];
            }
            return true;
        }

        private static Expression IntegratePolynomial(double[] coefficients, string variable)
        {
            coefficients = TrimPolynomial(coefficients);
            Expression result = ExpressionBuilder.Constant(0.0);
            for (int i = 0; i < coefficients.Length; i++)
            {
                if (coefficients[i] == 0.0)
                {
                    continue;
                }
                Expression term = ExpressionBuilder.Multiply(
                    ExpressionBuilder.Constant(coefficients[i] / (i + 1.0)),
                    ExpressionBuilder.Power(
                        ExpressionBuilder.Variable(variable),
                        ExpressionBuilder.Constant(i + 1.0)));
                result = ExpressionBuilder.Add(result, term);
            }
            return result;
        }

        private static bool PolynomialDivide(double[] dividend, double[] divisor, out double[] quotient, out double[] remainder)
        {
            dividend = TrimPolynomial(dividend);
            divisor = TrimPolynomial(divisor);
            if (IsZeroPolynomial(divisor))
            {
                quotient = new[] { 0.0 };
                remainder = new[] { 0.0 };
                return false;
            }
            if (dividend.Length < divisor.Length)
            {
                quotient = new[] { 0.0 };
                remainder = dividend;
                return true;
            }
            double[] working = (double[])dividend.Clone();
            double[] quot = new double[dividend.Length - divisor.Length + 1];
            double leading = divisor[divisor.Length - 1];
            for (int i = working.Length - divisor.Length; i >= 0; i--)
            {
                double coefficient = working[i + divisor.Length - 1] / leading;
                quot[i] = coefficient;
                if (coefficient == 0.0)
                {
                    continue;
                }
                for (int j = 0; j < divisor.Length; j++)
                {
                    working[i + j] -= coefficient * divisor[j];
                }
            }
            quotient = TrimPolynomial(quot);
            remainder = TrimPolynomial(working);
            return true;
        }

        private static bool PolynomialDivideByLinear(double[] polynomial, double root, out double[] quotient)
        {
            polynomial = TrimPolynomial(polynomial);
            if (polynomial.Length <= 1)
            {
                quotient = new[] { 0.0 };
                return Math.Abs(polynomial[0]) < 1e-8;
            }
            int degree = polynomial.Length - 1;
            double[] quot = new double[degree];
            double carry = 0.0;
            for (int i = degree; i >= 1; i--)
            {
                carry = polynomial[i] + (carry * root);
                quot[i - 1] = carry;
            }
            double remainder = polynomial[0] + (carry * root);
            quotient = TrimPolynomial(quot);
            return Math.Abs(remainder) < 1e-7;
        }

        private static double PolynomialEvaluate(double[] coefficients, double value)
        {
            return Algebra.PolynomialEvaluate(coefficients, value);
        }

        private static double[] PolynomialAdd(double[] left, double[] right)
        {
            int length = Math.Max(left.Length, right.Length);
            double[] result = new double[length];
            for (int i = 0; i < length; i++)
            {
                double leftValue = i < left.Length ? left[i] : 0.0;
                double rightValue = i < right.Length ? right[i] : 0.0;
                result[i] = leftValue + rightValue;
            }
            return TrimPolynomial(result);
        }

        private static double[] PolynomialSubtract(double[] left, double[] right)
        {
            int length = Math.Max(left.Length, right.Length);
            double[] result = new double[length];
            for (int i = 0; i < length; i++)
            {
                double leftValue = i < left.Length ? left[i] : 0.0;
                double rightValue = i < right.Length ? right[i] : 0.0;
                result[i] = leftValue - rightValue;
            }
            return TrimPolynomial(result);
        }

        private static double[] PolynomialMultiply(double[] left, double[] right)
        {
            double[] result = new double[left.Length + right.Length - 1];
            for (int i = 0; i < left.Length; i++)
            {
                for (int j = 0; j < right.Length; j++)
                {
                    result[i + j] += left[i] * right[j];
                }
            }
            return TrimPolynomial(result);
        }

        private static double[] PolynomialScale(double[] polynomial, double scale)
        {
            double[] result = new double[polynomial.Length];
            for (int i = 0; i < polynomial.Length; i++)
            {
                result[i] = polynomial[i] * scale;
            }
            return TrimPolynomial(result);
        }

        private static double[] PolynomialPower(double[] polynomial, int power)
        {
            double[] result = new[] { 1.0 };
            for (int i = 0; i < power; i++)
            {
                result = PolynomialMultiply(result, polynomial);
            }
            return result;
        }

        private static double[] TrimPolynomial(double[] coefficients)
        {
            if (coefficients.Length == 0)
            {
                return new[] { 0.0 };
            }
            int last = coefficients.Length - 1;
            while (last >= 0 && Math.Abs(coefficients[last]) < 1e-12)
            {
                last--;
            }
            if (last < 0)
            {
                return new[] { 0.0 };
            }
            double[] result = new double[last + 1];
            Array.Copy(coefficients, result, last + 1);
            return result;
        }

        private static bool IsZeroPolynomial(double[] polynomial)
        {
            return polynomial.Length == 1 && Math.Abs(polynomial[0]) < 1e-12;
        }

        internal static double Evaluate(Expression expression, string variable, double value)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            return EvaluateInternal(expression, name =>
            {
                if (name == variable)
                {
                    return value;
                }
                throw new ArgumentException($"No value was provided for variable '{name}'.");
            });
        }

        internal static double Evaluate(Expression expression, IReadOnlyDictionary<string, double> variables)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (variables == null)
            {
                throw new ArgumentNullException(nameof(variables));
            }
            return EvaluateInternal(expression, name =>
            {
                if (variables.TryGetValue(name, out double value))
                {
                    return value;
                }
                throw new ArgumentException($"No value was provided for variable '{name}'.");
            });
        }

        private static double EvaluateInternal(Expression expression, Func<string, double> resolve)
        {
            switch (expression)
            {
                case ConstantExpression constant:
                    return constant.Value;
                case VariableExpression variable:
                    return resolve(variable.Name);
                case NegateExpression negate:
                    return -EvaluateInternal(negate.Operand, resolve);
                case BinaryExpression binary:
                {
                    double left = EvaluateInternal(binary.Left, resolve);
                    double right = EvaluateInternal(binary.Right, resolve);
                    switch (binary.Operator)
                    {
                        case BinaryOperator.Add:
                            return left + right;
                        case BinaryOperator.Subtract:
                            return left - right;
                        case BinaryOperator.Multiply:
                            return left * right;
                        case BinaryOperator.Divide:
                            if (right == 0.0)
                            {
                                throw new DivideByZeroException("Cannot divide by zero.");
                            }
                            return left / right;
                        case BinaryOperator.Power:
                            return Math.Pow(left, right);
                        default:
                            return 0.0;
                    }
                }
                case FunctionExpression function:
                {
                    double argument = EvaluateInternal(function.Argument, resolve);
                    switch (function.Function)
                    {
                        case FunctionKind.Sin:
                            return Math.Sin(argument);
                        case FunctionKind.Cos:
                            return Math.Cos(argument);
                        case FunctionKind.Tan:
                            return Math.Tan(argument);
                        case FunctionKind.Cot:
                        {
                            double sine = Math.Sin(argument);
                            if (Math.Abs(sine) < 1e-15)
                            {
                                throw new DivideByZeroException("The cotangent is undefined where the sine is zero.");
                            }
                            return Math.Cos(argument) / sine;
                        }
                        case FunctionKind.Sec:
                        {
                            double cosine = Math.Cos(argument);
                            if (Math.Abs(cosine) < 1e-15)
                            {
                                throw new DivideByZeroException("The secant is undefined where the cosine is zero.");
                            }
                            return 1.0 / cosine;
                        }
                        case FunctionKind.Csc:
                        {
                            double sine = Math.Sin(argument);
                            if (Math.Abs(sine) < 1e-15)
                            {
                                throw new DivideByZeroException("The cosecant is undefined where the sine is zero.");
                            }
                            return 1.0 / sine;
                        }
                        case FunctionKind.Asin:
                            if (argument < -1.0 || argument > 1.0)
                            {
                                throw new ArgumentOutOfRangeException(nameof(argument), "The argument of asin must be between -1 and 1.");
                            }
                            return Math.Asin(argument);
                        case FunctionKind.Acos:
                            if (argument < -1.0 || argument > 1.0)
                            {
                                throw new ArgumentOutOfRangeException(nameof(argument), "The argument of acos must be between -1 and 1.");
                            }
                            return Math.Acos(argument);
                        case FunctionKind.Atan:
                            return Math.Atan(argument);
                        case FunctionKind.Sinh:
                            return Math.Sinh(argument);
                        case FunctionKind.Cosh:
                            return Math.Cosh(argument);
                        case FunctionKind.Tanh:
                            return Math.Tanh(argument);
                        case FunctionKind.Asinh:
                            return Math.Asinh(argument);
                        case FunctionKind.Acosh:
                            if (argument < 1.0)
                            {
                                throw new ArgumentOutOfRangeException(nameof(argument), "The argument of acosh must be at least 1.");
                            }
                            return Math.Acosh(argument);
                        case FunctionKind.Atanh:
                            if (argument <= -1.0 || argument >= 1.0)
                            {
                                throw new ArgumentOutOfRangeException(nameof(argument), "The argument of atanh must be between -1 and 1.");
                            }
                            return Math.Atanh(argument);
                        case FunctionKind.Exp:
                            return Math.Exp(argument);
                        case FunctionKind.Ln:
                            if (argument <= 0.0)
                            {
                                throw new ArgumentOutOfRangeException(nameof(argument), "The argument of ln must be positive.");
                            }
                            return Math.Log(argument);
                        case FunctionKind.Log10:
                            if (argument <= 0.0)
                            {
                                throw new ArgumentOutOfRangeException(nameof(argument), "The argument of log10 must be positive.");
                            }
                            return Math.Log10(argument);
                        case FunctionKind.Sqrt:
                            if (argument < 0.0)
                            {
                                throw new ArgumentOutOfRangeException(nameof(argument), "The argument of sqrt cannot be negative.");
                            }
                            return Math.Sqrt(argument);
                        case FunctionKind.Cbrt:
                            return Math.Cbrt(argument);
                        case FunctionKind.Abs:
                            return Math.Abs(argument);
                        case FunctionKind.Sign:
                            return Math.Sign(argument);
                        default:
                            return 0.0;
                    }
                }
                default:
                    return 0.0;
            }
        }
    }
}
