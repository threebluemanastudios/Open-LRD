using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal sealed class UnitConversionRequiredException : InvalidOperationException
    {
        public string ValueName { get; }
        public string ProvidedUnitTitle { get; }
        public string RequiredUnitTitle { get; }

        public UnitConversionRequiredException(string valueName, string providedUnitTitle, string requiredUnitTitle)
            : base($"'{valueName}' is in '{providedUnitTitle}', but the selected function requires '{requiredUnitTitle}'. Convert the value before dispatching.")
        {
            ValueName = valueName;
            ProvidedUnitTitle = providedUnitTitle;
            RequiredUnitTitle = requiredUnitTitle;
        }
    }

    internal static class MathFunctionDispatcher
    {
        private sealed record FunctionDescriptor(
            string Key,
            string Name,
            MethodInfo Method,
            Type[] ParameterTypes,
            Type ReturnType,
            IReadOnlyDictionary<int, string> InputUnits,
            string? OutputUnit);

        private static readonly IReadOnlyList<FunctionDescriptor> Functions = BuildFunctionRegistry();
        private static readonly IReadOnlyList<FunctionDescriptor> ScalarFunctions = BuildScalarFunctionRegistry();

        public static FunctionResult Dispatch(FunctionBox functionBox)
        {
            if (functionBox == null)
            {
                throw new ArgumentNullException(nameof(functionBox));
            }

            FunctionDispatchSnapshot snapshot = functionBox.GetDispatchSnapshot();
            FunctionDescriptor descriptor = SelectFunction(
                snapshot.FunctionKey,
                snapshot.Variables,
                snapshot.ExpectedResultType,
                snapshot.ExpectedResultUnitTitle);
            FunctionResult result = Invoke(descriptor, snapshot.Variables);
            if (!snapshot.IsReadOnly)
            {
                functionBox.TryStoreDispatchResult(result, snapshot.DefinitionRevision);
            }
            return result;
        }

        internal static void ValidateDefinition(FunctionBox functionBox)
        {
            ArgumentNullException.ThrowIfNull(functionBox);
            functionBox.ValidateDefinition();
            FunctionDescriptor descriptor = SelectFunction(
                functionBox.FunctionKey,
                functionBox.Variables,
                functionBox.ExpectedResultType,
                functionBox.ExpectedResultUnitTitle);
            FunctionResult result = functionBox.Result!;
            if (result.DataType != descriptor.ReturnType)
            {
                throw new ArgumentException("The stored function result type does not match the selected function signature.", nameof(functionBox));
            }
            if (result.Value != null && !descriptor.ReturnType.IsInstanceOfType(result.Value))
            {
                throw new ArgumentException("The stored function result value does not match the selected function signature.", nameof(functionBox));
            }
            if (!string.Equals(result.UnitTitle, descriptor.OutputUnit, StringComparison.Ordinal))
            {
                throw new ArgumentException("The stored function result unit does not match the selected function signature.", nameof(functionBox));
            }
        }

        /// <summary>Resolves an alias or unqualified function name to its stable registry key.</summary>
        public static string ResolveFunctionKey(
            string? functionKey,
            IReadOnlyList<FunctionVariable> variables,
            Type expectedResultType,
            string? expectedResultUnitTitle = null)
        {
            ArgumentNullException.ThrowIfNull(variables);
            ArgumentNullException.ThrowIfNull(expectedResultType);
            return SelectFunction(functionKey, variables, expectedResultType,
                NormalizeOptionalText(expectedResultUnitTitle)).Key;
        }

        public static FunctionResult Dispatch(
            string functionKey,
            IReadOnlyList<FunctionVariable> variables,
            Type expectedResultType,
            string? expectedResultUnitTitle = null)
        {
            if (string.IsNullOrWhiteSpace(functionKey))
            {
                throw new ArgumentException("A function key is required.", nameof(functionKey));
            }
            if (variables == null)
            {
                throw new ArgumentNullException(nameof(variables));
            }
            if (expectedResultType == null)
            {
                throw new ArgumentNullException(nameof(expectedResultType));
            }

            FunctionDescriptor descriptor = SelectFunction(
                functionKey.Trim(),
                variables,
                expectedResultType,
                NormalizeOptionalText(expectedResultUnitTitle));
            return Invoke(descriptor, variables);
        }

        public static FunctionResult Dispatch(string functionKey, Type expectedResultType, params object?[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            List<FunctionVariable> variables = new List<FunctionVariable>(values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                object? value = values[index];
                if (value == null)
                {
                    throw new ArgumentException("Null arguments require an explicit FunctionVariable data type.", nameof(values));
                }
                variables.Add(new FunctionVariable($"argument{index + 1}", value, value.GetType()));
            }
            return Dispatch(functionKey, variables, expectedResultType);
        }

        public static T Dispatch<T>(string functionKey, params object?[] values)
        {
            FunctionResult result = Dispatch(functionKey, typeof(T), values);
            if (result.Value is T typedResult)
            {
                return typedResult;
            }
            if (result.Value == null && default(T) is null)
            {
                return default!;
            }
            throw new InvalidOperationException($"Function '{functionKey}' did not return a value of type '{typeof(T).FullName}'.");
        }

        private static FunctionDescriptor SelectFunction(
            string? functionKey,
            IReadOnlyList<FunctionVariable> variables,
            Type expectedResultType,
            string? expectedResultUnitTitle)
        {
            if (variables.Any(variable => variable == null))
            {
                throw new ArgumentException("The variable list cannot contain null entries.", nameof(variables));
            }

            IEnumerable<FunctionDescriptor> keyedFunctions = Functions;
            if (!string.IsNullOrWhiteSpace(functionKey))
            {
                string key = functionKey.Trim();
                keyedFunctions = Functions.Where(function =>
                    string.Equals(function.Key, key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(function.Name, key, StringComparison.OrdinalIgnoreCase));
                if (!keyedFunctions.Any())
                {
                    throw new KeyNotFoundException($"No registered math function has key or name '{key}'.");
                }
            }

            List<FunctionDescriptor> signatureMatches = keyedFunctions
                .Where(function => SignatureMatches(function, variables, expectedResultType))
                .ToList();
            if (signatureMatches.Count == 0)
            {
                string target = string.IsNullOrWhiteSpace(functionKey) ? "the supplied signature" : $"function '{functionKey}'";
                throw new ArgumentException($"No overload of {target} matches the supplied argument count, data types, and expected output type.");
            }

            List<FunctionDescriptor> unitMatches = new List<FunctionDescriptor>();
            List<Exception> unitErrors = new List<Exception>();
            foreach (FunctionDescriptor function in signatureMatches)
            {
                try
                {
                    ValidateUnits(function, variables, expectedResultUnitTitle);
                    unitMatches.Add(function);
                }
                catch (ArgumentException exception)
                {
                    unitErrors.Add(exception);
                }
                catch (UnitConversionRequiredException exception)
                {
                    unitErrors.Add(exception);
                }
            }

            if (unitMatches.Count == 1)
            {
                return unitMatches[0];
            }
            if (unitMatches.Count > 1)
            {
                throw CreateAmbiguousFunctionException(functionKey, unitMatches);
            }

            UnitConversionRequiredException[] conversionErrors = unitErrors.OfType<UnitConversionRequiredException>().ToArray();
            if (conversionErrors.Length == 1)
            {
                throw conversionErrors[0];
            }
            if (conversionErrors.Length > 1)
            {
                throw CreateAmbiguousFunctionException(functionKey, signatureMatches);
            }
            if (unitErrors.Count == 1)
            {
                throw unitErrors[0];
            }
            throw new ArgumentException("The supplied units do not match any registered overload.");
        }

        private static bool SignatureMatches(FunctionDescriptor function, IReadOnlyList<FunctionVariable> variables, Type expectedResultType)
        {
            Type expectedValueType = Nullable.GetUnderlyingType(expectedResultType) ?? expectedResultType;
            if (function.ParameterTypes.Length != variables.Count || !expectedValueType.IsAssignableFrom(function.ReturnType))
            {
                return false;
            }

            for (int index = 0; index < variables.Count; index++)
            {
                FunctionVariable variable = variables[index];
                Type parameterType = function.ParameterTypes[index];
                if (variable.DataType != parameterType || !ValueMatchesDeclaredType(variable.Value, variable.DataType))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ValueMatchesDeclaredType(object? value, Type declaredType)
        {
            if (value == null)
            {
                return !declaredType.IsValueType || Nullable.GetUnderlyingType(declaredType) != null;
            }
            Type? nullableType = Nullable.GetUnderlyingType(declaredType);
            return declaredType.IsInstanceOfType(value) || (nullableType != null && nullableType.IsInstanceOfType(value));
        }

        private static void ValidateUnits(
            FunctionDescriptor function,
            IReadOnlyList<FunctionVariable> variables,
            string? expectedResultUnitTitle)
        {
            bool anyInputUnit = variables.Any(variable => variable.UnitTitle != null);
            if (function.InputUnits.Count == 0)
            {
                if (anyInputUnit)
                {
                    throw new ArgumentException($"Function '{function.Key}' has no input-unit metadata and cannot safely accept unit-bearing values.");
                }
            }
            else
            {
                foreach (KeyValuePair<int, string> requirement in function.InputUnits)
                {
                    FunctionVariable variable = variables[requirement.Key];
                    if (variable.UnitTitle == null)
                    {
                        throw new ArgumentException($"Variable '{variable.Name}' must specify unit '{requirement.Value}'.");
                    }
                    ValidateUnit(variable.Name, variable.UnitTitle, requirement.Value);
                }

                for (int index = 0; index < variables.Count; index++)
                {
                    if (variables[index].UnitTitle != null && !function.InputUnits.ContainsKey(index))
                    {
                        throw new ArgumentException($"Function '{function.Key}' has no unit requirement for variable '{variables[index].Name}'.");
                    }
                }
            }

            if (expectedResultUnitTitle == null)
            {
                return;
            }
            if (function.OutputUnit == null)
            {
                throw new ArgumentException($"Function '{function.Key}' has no output-unit metadata to match '{expectedResultUnitTitle}'.");
            }
            ValidateUnit("result", expectedResultUnitTitle, function.OutputUnit);
        }

        private static void ValidateUnit(string valueName, string providedUnitTitle, string requiredUnitTitle)
        {
            if (!UnitCatalog.TryFindUnit(providedUnitTitle, out Unit providedUnit))
            {
                throw new ArgumentException($"Unknown unit title '{providedUnitTitle}' for '{valueName}'.");
            }
            if (!UnitCatalog.TryFindUnit(requiredUnitTitle, out Unit requiredUnit))
            {
                throw new InvalidOperationException($"The function registry contains unknown required unit '{requiredUnitTitle}'.");
            }
            if (string.Equals(providedUnit.Symbol, requiredUnit.Symbol, StringComparison.Ordinal))
            {
                return;
            }
            if (providedUnit.Dimension == requiredUnit.Dimension)
            {
                throw new UnitConversionRequiredException(valueName, providedUnitTitle, requiredUnitTitle);
            }
            throw new ArgumentException($"Unit '{providedUnitTitle}' for '{valueName}' is incompatible with required unit '{requiredUnitTitle}'.");
        }

        private static FunctionResult Invoke(FunctionDescriptor function, IReadOnlyList<FunctionVariable> variables)
        {
            object?[] arguments = variables.Select(variable => variable.Value).ToArray();
            object? value;
            try
            {
                value = function.Method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }

            string? resultUnitTitle = function.OutputUnit;
            return new FunctionResult(value, function.ReturnType, resultUnitTitle);
        }

        internal static bool CanResolveScalarExpressionCall(string nameOrKey, int argumentCount, out string canonicalName)
        {
            canonicalName = string.Empty;
            if (string.IsNullOrWhiteSpace(nameOrKey) || argumentCount <= 0)
            {
                return false;
            }

            string request = nameOrKey.Trim();
            List<FunctionDescriptor> candidates = ScalarFunctions
                .Where(function => function.ParameterTypes.Length == argumentCount)
                .Where(function => MatchesScalarName(function, request))
                .ToList();
            if (candidates.Count == 0)
            {
                return false;
            }

            List<FunctionDescriptor> keyedCandidates = candidates
                .Where(function => string.Equals(function.Key, request, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (keyedCandidates.Count > 0)
            {
                candidates = keyedCandidates;
            }

            FunctionDescriptor selected = candidates
                .OrderBy(function => GetScalarPreferenceScore(function.ParameterTypes))
                .ThenBy(function => function.Key, StringComparer.Ordinal)
                .First();
            canonicalName = selected.Key;
            return true;
        }

        internal static double InvokeScalarExpressionCall(string nameOrKey, IReadOnlyList<double> arguments)
        {
            if (!TryInvokeScalarExpressionCall(nameOrKey, arguments, out double result))
            {
                throw new ArgumentException($"No registered scalar math function named '{nameOrKey}' accepts {arguments.Count} numeric argument(s).", nameof(nameOrKey));
            }
            return result;
        }

        internal static bool TryInvokeScalarExpressionCall(string nameOrKey, IReadOnlyList<double> arguments, out double result)
        {
            result = 0.0;
            if (string.IsNullOrWhiteSpace(nameOrKey))
            {
                return false;
            }
            ArgumentNullException.ThrowIfNull(arguments);

            string request = nameOrKey.Trim();
            List<FunctionDescriptor> candidates = ScalarFunctions
                .Where(function => function.ParameterTypes.Length == arguments.Count)
                .Where(function => MatchesScalarName(function, request))
                .ToList();
            List<FunctionDescriptor> keyedCandidates = candidates
                .Where(function => string.Equals(function.Key, request, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (keyedCandidates.Count > 0)
            {
                candidates = keyedCandidates;
            }

            FunctionDescriptor? best = null;
            object?[]? bestArguments = null;
            int bestScore = int.MaxValue;
            foreach (FunctionDescriptor candidate in candidates)
            {
                if (!TryConvertScalarArguments(candidate.ParameterTypes, arguments, out object?[] converted, out int score) || score >= bestScore)
                {
                    continue;
                }
                best = candidate;
                bestArguments = converted;
                bestScore = score;
            }

            if (best == null || bestArguments == null)
            {
                return false;
            }

            object? value;
            try
            {
                value = best.Method.Invoke(null, bestArguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            if (value == null)
            {
                return false;
            }
            try
            {
                double convertedResult = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (!double.IsFinite(convertedResult))
                {
                    return false;
                }
                result = convertedResult;
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool MatchesScalarName(FunctionDescriptor function, string request)
        {
            return string.Equals(function.Key, request, StringComparison.OrdinalIgnoreCase)
                || string.Equals(function.Name, request, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetScalarPreferenceScore(IReadOnlyList<Type> parameterTypes)
        {
            int score = 0;
            foreach (Type parameterType in parameterTypes)
            {
                score += parameterType == typeof(double) ? 0
                    : parameterType == typeof(int) ? 1
                    : parameterType == typeof(long) ? 2
                    : parameterType == typeof(BigInteger) ? 3
                    : parameterType == typeof(float) ? 4
                    : 5;
            }
            return score;
        }

        private static bool TryConvertScalarArguments(
            IReadOnlyList<Type> parameterTypes,
            IReadOnlyList<double> arguments,
            out object?[] converted,
            out int score)
        {
            converted = new object?[arguments.Count];
            score = 0;
            for (int index = 0; index < arguments.Count; index++)
            {
                double value = arguments[index];
                if (!double.IsFinite(value))
                {
                    return false;
                }
                Type parameterType = parameterTypes[index];
                if (parameterType == typeof(double))
                {
                    converted[index] = value;
                    continue;
                }
                if (parameterType == typeof(float))
                {
                    float floatValue = (float)value;
                    if (!float.IsFinite(floatValue))
                    {
                        return false;
                    }
                    converted[index] = floatValue;
                    score += 4;
                    continue;
                }
                if (parameterType == typeof(decimal))
                {
                    try
                    {
                        converted[index] = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    }
                    catch (OverflowException)
                    {
                        return false;
                    }
                    score += 5;
                    continue;
                }
                if (value != Math.Truncate(value))
                {
                    return false;
                }
                if (parameterType == typeof(int))
                {
                    if (value < int.MinValue || value > int.MaxValue)
                    {
                        return false;
                    }
                    converted[index] = (int)value;
                    score += 1;
                    continue;
                }
                if (parameterType == typeof(long))
                {
                    if (value < long.MinValue || value > long.MaxValue)
                    {
                        return false;
                    }
                    converted[index] = (long)value;
                    score += 2;
                    continue;
                }
                if (parameterType == typeof(BigInteger))
                {
                    if (value < long.MinValue || value > long.MaxValue)
                    {
                        return false;
                    }
                    converted[index] = new BigInteger((long)value);
                    score += 3;
                    continue;
                }
                return false;
            }
            return true;
        }

        private static IReadOnlyList<FunctionDescriptor> BuildScalarFunctionRegistry()
        {
            Type[] scalarTypes =
            {
                typeof(Math_1),
                typeof(Math_2),
                typeof(Math_3),
                typeof(Math_4),
                typeof(Algebra),
                typeof(ProbabilityAndStatistics),
                typeof(DiscreteMath),
                typeof(InformationTheoryAndMLMath),
                typeof(FinancialMath),
                typeof(BasicPhysics),
                typeof(Thermodynamics),
                typeof(FluidMechanics),
                typeof(Electromagnetism),
                typeof(CondensedMatter),
                typeof(CurvedSpacetime)
            };
            List<FunctionDescriptor> functions = new List<FunctionDescriptor>();
            foreach (Type scalarType in scalarTypes)
            {
                MethodInfo[] methods = scalarType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                foreach (MethodInfo method in methods)
                {
                    if (method.IsGenericMethodDefinition
                        || method.IsSpecialName
                        || !IsScalarNumericType(method.ReturnType))
                    {
                        continue;
                    }
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 0
                        || parameters.Any(parameter => parameter.IsOut
                            || parameter.ParameterType.IsByRef
                            || !IsScalarNumericType(parameter.ParameterType)))
                    {
                        continue;
                    }
                    functions.Add(new FunctionDescriptor(
                        $"{scalarType.Name}.{method.Name}",
                        method.Name,
                        method,
                        parameters.Select(parameter => parameter.ParameterType).ToArray(),
                        method.ReturnType,
                        new Dictionary<int, string>(),
                        null));
                }
            }
            return functions.AsReadOnly();
        }

        private static bool IsScalarNumericType(Type type)
        {
            return type == typeof(double)
                || type == typeof(float)
                || type == typeof(int)
                || type == typeof(long)
                || type == typeof(decimal)
                || type == typeof(BigInteger);
        }

        private static IReadOnlyList<FunctionDescriptor> BuildFunctionRegistry()
        {
            Type[] mathTypes = { typeof(Math_1), typeof(Math_2), typeof(Math_3), typeof(Math_4) };
            List<FunctionDescriptor> functions = new List<FunctionDescriptor>();
            foreach (Type mathType in mathTypes)
            {
                MethodInfo[] methods = mathType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                foreach (MethodInfo method in methods)
                {
                    if (method.IsGenericMethodDefinition || method.ReturnType == typeof(void))
                    {
                        continue;
                    }
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Any(parameter => parameter.IsOut || parameter.ParameterType.IsByRef))
                    {
                        continue;
                    }
                    functions.Add(new FunctionDescriptor(
                        $"{mathType.Name}.{method.Name}",
                        method.Name,
                        method,
                        parameters.Select(parameter => parameter.ParameterType).ToArray(),
                        method.ReturnType,
                        new Dictionary<int, string>(),
                        null));
                }
            }

            AddUnitMetadata(functions, "Math_2.CalculateHypotenuse", new[] { typeof(double), typeof(double) }, new Dictionary<int, string> { [0] = "m", [1] = "m" }, "m");
            AddUnitMetadata(functions, "Math_2.CalculateHypotenuse", new[] { typeof(float), typeof(float) }, new Dictionary<int, string> { [0] = "m", [1] = "m" }, "m");
            AddUnitMetadata(functions, "Math_2.CalculateRectangleArea", new[] { typeof(double), typeof(double) }, new Dictionary<int, string> { [0] = "m", [1] = "m" }, "m²");
            AddUnitMetadata(functions, "Math_2.CalculateRectangleArea", new[] { typeof(float), typeof(float) }, new Dictionary<int, string> { [0] = "m", [1] = "m" }, "m²");
            AddUnitMetadata(functions, "Math_2.CalculateCircleArea", new[] { typeof(double) }, new Dictionary<int, string> { [0] = "m" }, "m²");
            AddUnitMetadata(functions, "Math_2.CalculateCircleArea", new[] { typeof(float) }, new Dictionary<int, string> { [0] = "m" }, "m²");
            return functions.AsReadOnly();
        }

        private static void AddUnitMetadata(
            List<FunctionDescriptor> functions,
            string key,
            Type[] parameterTypes,
            IReadOnlyDictionary<int, string> inputUnits,
            string outputUnit)
        {
            int index = functions.FindIndex(function =>
                string.Equals(function.Key, key, StringComparison.Ordinal)
                && function.ParameterTypes.SequenceEqual(parameterTypes));
            if (index < 0)
            {
                throw new InvalidOperationException($"Cannot attach unit metadata to unregistered function '{key}'.");
            }
            functions[index] = functions[index] with { InputUnits = inputUnits, OutputUnit = outputUnit };
        }

        private static InvalidOperationException CreateAmbiguousFunctionException(string? functionKey, IReadOnlyList<FunctionDescriptor> candidates)
        {
            string names = string.Join(", ", candidates.Select(candidate => candidate.Key).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal));
            string request = string.IsNullOrWhiteSpace(functionKey) ? "The supplied signature" : $"Function key '{functionKey}'";
            return new InvalidOperationException($"{request} matches multiple math functions: {names}.");
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
