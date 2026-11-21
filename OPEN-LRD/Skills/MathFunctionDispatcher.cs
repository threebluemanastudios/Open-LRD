using System;
using System.Collections.Generic;
using System.Linq;
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

        public static FunctionResult Dispatch(FunctionBox functionBox)
        {
            if (functionBox == null)
            {
                throw new ArgumentNullException(nameof(functionBox));
            }

            functionBox.ClearResult();
            FunctionDescriptor descriptor = SelectFunction(
                functionBox.FunctionKey,
                functionBox.Variables,
                functionBox.ExpectedResultType,
                functionBox.ExpectedResultUnitTitle);
            FunctionResult result = Invoke(descriptor, functionBox.Variables);
            functionBox.StoreResult(result);
            return result;
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
            if (function.ParameterTypes.Length != variables.Count || !expectedResultType.IsAssignableFrom(function.ReturnType))
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
