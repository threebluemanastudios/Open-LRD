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

        private static readonly Type[] DispatchSkillTypes =
        {
            typeof(Math_1), typeof(Math_2), typeof(Math_3), typeof(Math_4),
            typeof(Algebra), typeof(ProbabilityAndStatistics), typeof(DiscreteMath),
            typeof(InformationTheoryAndMLMath), typeof(FinancialMath), typeof(BasicPhysics),
            typeof(Thermodynamics), typeof(FluidMechanics), typeof(Electromagnetism),
            typeof(CondensedMatter), typeof(CurvedSpacetime), typeof(LinearAlgebraAdvanced),
            typeof(NumericalMethods), typeof(Optimization), typeof(Geometry3DAndGraphics),
            typeof(TransformsAndSignals), typeof(TimeAndDate), typeof(UnitsAndMeasurement),
            typeof(ManyBodyInteractions), typeof(ExtremeScalars)
        };

        private static readonly Type[] DispatchValueTypes =
        {
            typeof(string), typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double),
            typeof(decimal), typeof(char), typeof(Guid), typeof(DateTime), typeof(DateTimeOffset),
            typeof(TimeSpan), typeof(BigInteger), typeof(Fraction), typeof(OPEN_LRD.Foundations.Vector2), typeof(OPEN_LRD.Foundations.Vector3),
            typeof(Complex), typeof(double[]), typeof(int[])
        };

        private static readonly List<string> DiscoveryRejections = new List<string>();
        private static readonly IReadOnlyList<FunctionDescriptor> Functions = BuildFunctionRegistry();
        private static readonly IReadOnlyList<FunctionDescriptor> ScalarFunctions = BuildScalarFunctionRegistry();

        /// <summary>Returns stable diagnostics for allowlisted methods excluded from function dispatch.</summary>
        public static IReadOnlyList<string> GetDiscoveryDiagnostics() => DiscoveryRejections.AsReadOnly();

        /// <summary>Dispatches a FunctionBox definition and stores the successful result if its revision is unchanged.</summary>
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
            FunctionDispatchProvenance provenance = CreateDispatchProvenance(descriptor, snapshot, result);
            if (!snapshot.IsReadOnly)
            {
                functionBox.TryStoreDispatchResult(result, snapshot.DefinitionRevision, provenance);
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
            FunctionResult dispatched = Invoke(descriptor, functionBox.Variables);
            if (!ResultsEquivalent(result, dispatched))
            {
                throw new ArgumentException("The stored result was not produced by dispatch for the current function definition and variable values.", nameof(functionBox));
            }
        }

        private static bool ResultsEquivalent(FunctionResult stored, FunctionResult dispatched)
        {
            if (stored.DataType != dispatched.DataType ||
                !string.Equals(stored.UnitTitle, dispatched.UnitTitle, StringComparison.Ordinal))
            {
                return false;
            }
            if (stored.Value is double storedDouble && dispatched.Value is double dispatchedDouble)
            {
                return double.IsFinite(storedDouble) && double.IsFinite(dispatchedDouble) &&
                       Math.Abs(storedDouble - dispatchedDouble) <= 1e-12 * Math.Max(1.0, Math.Max(Math.Abs(storedDouble), Math.Abs(dispatchedDouble)));
            }
            if (stored.Value is float storedFloat && dispatched.Value is float dispatchedFloat)
            {
                return float.IsFinite(storedFloat) && float.IsFinite(dispatchedFloat) &&
                       Math.Abs(storedFloat - dispatchedFloat) <= 1e-6f * Math.Max(1.0f, Math.Max(Math.Abs(storedFloat), Math.Abs(dispatchedFloat)));
            }
            if (stored.Value is Array storedArray && dispatched.Value is Array dispatchedArray)
            {
                return storedArray.GetType() == dispatchedArray.GetType() && storedArray.Length == dispatchedArray.Length &&
                       storedArray.Cast<object?>().SequenceEqual(dispatchedArray.Cast<object?>());
            }
            return Equals(stored.Value, dispatched.Value);
        }

        private static FunctionDispatchProvenance CreateDispatchProvenance(FunctionDescriptor descriptor, FunctionDispatchSnapshot snapshot, FunctionResult result)
        {
            string argumentSnapshotHash = ComputeHash(SerializeDispatchPayload(writer =>
            {
                writer.Write(descriptor.Key);
                writer.Write(snapshot.DefinitionRevision);
                writer.Write(snapshot.Variables.Count);
                foreach (FunctionVariable variable in snapshot.Variables)
                {
                    writer.Write(variable.Name);
                    writer.Write(variable.DataType.AssemblyQualifiedName ?? variable.DataType.FullName ?? variable.DataType.Name);
                    writer.Write(variable.UnitTitle ?? string.Empty);
                    WriteHashValue(writer, variable.Value, variable.DataType);
                }
            }));
            string resultHash = ComputeHash(SerializeDispatchPayload(writer => WriteHashValue(writer, result.Value, result.DataType)));
            return new FunctionDispatchProvenance(
                descriptor.Key,
                argumentSnapshotHash,
                resultHash,
                result.DataType.AssemblyQualifiedName ?? result.DataType.FullName ?? result.DataType.Name,
                result.UnitTitle,
                snapshot.DefinitionRevision,
                DateTimeOffset.UtcNow,
                typeof(MathFunctionDispatcher).Assembly.GetName().Version?.ToString() ?? "0.0.0.0");
        }

        private static byte[] SerializeDispatchPayload(Action<BinaryWriter> write)
        {
            using MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
            return stream.ToArray();
        }

        private static string ComputeHash(byte[] payload) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));

        private static void WriteHashValue(BinaryWriter writer, object? value, Type declaredType)
        {
            writer.Write(declaredType.AssemblyQualifiedName ?? declaredType.FullName ?? declaredType.Name);
            if (value == null) { writer.Write((byte)0); return; }
            writer.Write((byte)1);
            switch (value)
            {
                case string s: writer.Write((byte)1); writer.Write(s); break;
                case bool b: writer.Write((byte)2); writer.Write(b); break;
                case byte b: writer.Write((byte)3); writer.Write(b); break;
                case sbyte b: writer.Write((byte)4); writer.Write(b); break;
                case short i: writer.Write((byte)5); writer.Write(i); break;
                case ushort i: writer.Write((byte)6); writer.Write(i); break;
                case int i: writer.Write((byte)7); writer.Write(i); break;
                case uint i: writer.Write((byte)8); writer.Write(i); break;
                case long i: writer.Write((byte)9); writer.Write(i); break;
                case ulong i: writer.Write((byte)10); writer.Write(i); break;
                case float f: writer.Write((byte)11); writer.Write(f); break;
                case double d: writer.Write((byte)12); writer.Write(double.IsNaN(d) ? "NaN" : double.IsPositiveInfinity(d) ? "+Inf" : double.IsNegativeInfinity(d) ? "-Inf" : d.ToString("R", CultureInfo.InvariantCulture)); break;
                case decimal m: writer.Write((byte)13); writer.Write(m); break;
                case char c: writer.Write((byte)14); writer.Write(c); break;
                case Guid g: writer.Write((byte)15); writer.Write(g.ToByteArray()); break;
                case DateTime dt: writer.Write((byte)16); writer.Write(dt.Ticks); writer.Write((int)dt.Kind); break;
                case DateTimeOffset dto: writer.Write((byte)17); writer.Write(dto.UtcTicks); writer.Write((int)dto.Offset.TotalMinutes); break;
                case TimeSpan ts: writer.Write((byte)18); writer.Write(ts.Ticks); break;
                case BigInteger bi: writer.Write((byte)19); writer.Write(bi.ToString(CultureInfo.InvariantCulture)); break;
                case Fraction fr: writer.Write((byte)20); writer.Write(fr.Numerator.ToString(CultureInfo.InvariantCulture)); writer.Write(fr.Denominator.ToString(CultureInfo.InvariantCulture)); break;
                case OPEN_LRD.Foundations.Vector2 v2: writer.Write((byte)21); writer.Write(v2.X); writer.Write(v2.Y); break;
                case OPEN_LRD.Foundations.Vector3 v3: writer.Write((byte)22); writer.Write(v3.X); writer.Write(v3.Y); writer.Write(v3.Z); break;
                case Complex c: writer.Write((byte)23); writer.Write(c.Real); writer.Write(c.Imaginary); break;
                case double[] arr: writer.Write((byte)24); writer.Write(arr.Length); foreach (double item in arr) { writer.Write(double.IsNaN(item) ? "NaN" : double.IsPositiveInfinity(item) ? "+Inf" : double.IsNegativeInfinity(item) ? "-Inf" : item.ToString("R", CultureInfo.InvariantCulture)); } break;
                case int[] arr: writer.Write((byte)25); writer.Write(arr.Length); foreach (int item in arr) writer.Write(item); break;
                default: writer.Write((byte)255); writer.Write(value.ToString() ?? string.Empty); break;
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

        /// <summary>Dispatches the best matching registered overload for typed arguments and expected output.</summary>
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

        /// <summary>Dispatches a named overload using runtime argument types; null arguments require typed variables.</summary>
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

        /// <summary>Dispatches a named overload and returns its value as <typeparamref name="T"/>.</summary>
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
            List<FunctionDescriptor> functions = new List<FunctionDescriptor>();
            foreach (Type scalarType in DispatchSkillTypes.OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                MethodInfo[] methods = scalarType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .OrderBy(method => method.Name, StringComparer.Ordinal)
                    .ThenBy(method => string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName)), StringComparer.Ordinal)
                    .ThenBy(method => method.ReturnType.FullName, StringComparer.Ordinal)
                    .ToArray();
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
            List<FunctionDescriptor> functions = new List<FunctionDescriptor>();
            foreach (Type mathType in DispatchSkillTypes.OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                MethodInfo[] methods = mathType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .OrderBy(method => method.Name, StringComparer.Ordinal)
                    .ThenBy(method => string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName)), StringComparer.Ordinal)
                    .ThenBy(method => method.ReturnType.FullName, StringComparer.Ordinal)
                    .ToArray();
                foreach (MethodInfo method in methods)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    bool unsupported = method.IsGenericMethod || method.IsSpecialName || method.ReturnType == typeof(void) ||
                        method.ReturnType.IsByRef || method.ReturnType.IsByRefLike || !IsSupportedDispatchType(method.ReturnType) ||
                        parameters.Any(parameter => parameter.IsOut || parameter.ParameterType.IsByRef ||
                            parameter.ParameterType.IsByRefLike || parameter.IsOptional || parameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length != 0 ||
                            !IsSupportedDispatchType(parameter.ParameterType));
                    if (unsupported)
                    {
                        DiscoveryRejections.Add($"{mathType.FullName}.{method.Name}: unsupported or non-persistable signature excluded.");
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

            functions = functions.OrderBy(function => function.Method.DeclaringType?.FullName, StringComparer.Ordinal)
                .ThenBy(function => function.Name, StringComparer.Ordinal)
                .ThenBy(function => string.Join("|", function.ParameterTypes.Select(type => type.FullName)), StringComparer.Ordinal)
                .ThenBy(function => function.ReturnType.FullName, StringComparer.Ordinal)
                .ToList();

            AddUnitMetadata(functions, "Math_2.CalculateHypotenuse", new[] { typeof(double), typeof(double) }, new Dictionary<int, string> { [0] = "m", [1] = "m" }, "m");
            AddUnitMetadata(functions, "Math_2.CalculateHypotenuse", new[] { typeof(float), typeof(float) }, new Dictionary<int, string> { [0] = "m", [1] = "m" }, "m");
            AddUnitMetadata(functions, "Math_2.CalculateRectangleArea", new[] { typeof(double), typeof(double) }, new Dictionary<int, string> { [0] = "m", [1] = "m" }, "m²");
            AddUnitMetadata(functions, "Math_2.CalculateRectangleArea", new[] { typeof(float), typeof(float) }, new Dictionary<int, string> { [0] = "m", [1] = "m" }, "m²");
            AddUnitMetadata(functions, "Math_2.CalculateCircleArea", new[] { typeof(double) }, new Dictionary<int, string> { [0] = "m" }, "m²");
            AddUnitMetadata(functions, "Math_2.CalculateCircleArea", new[] { typeof(float) }, new Dictionary<int, string> { [0] = "m" }, "m²");

            AddUnits(functions, "BasicPhysics.Force", new[] { typeof(double), typeof(double) }, new[] { "kg", "m/s²" }, "N");
            AddUnits(functions, "BasicPhysics.Force", new[] { typeof(double), typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "kg", "m/s²" }, "N");
            AddUnits(functions, "BasicPhysics.MassFromForce", new[] { typeof(double), typeof(double) }, new[] { "N", "m/s²" }, "kg");
            AddUnits(functions, "BasicPhysics.Acceleration", new[] { typeof(double), typeof(double) }, new[] { "N", "kg" }, "m/s²");
            AddUnits(functions, "BasicPhysics.Weight", new[] { typeof(double) }, new[] { "kg" }, "N");
            AddUnits(functions, "BasicPhysics.Distance", new[] { typeof(double), typeof(double) }, new[] { "m/s", "s" }, "m");
            AddUnits(functions, "BasicPhysics.Distance", new[] { typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "m" }, "m");
            AddUnits(functions, "BasicPhysics.TravelTime", new[] { typeof(double), typeof(double) }, new[] { "m", "m/s" }, "s");
            AddUnits(functions, "BasicPhysics.KineticEnergy", new[] { typeof(double), typeof(double) }, new[] { "kg", "m/s" }, "J");
            AddUnits(functions, "BasicPhysics.GravitationalPotentialEnergy", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "kg", "m/s²", "m" }, "J");
            AddUnits(functions, "BasicPhysics.Momentum", new[] { typeof(double), typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "kg", "m/s" }, "kg·m/s");
            AddUnits(functions, "BasicPhysics.Impulse", new[] { typeof(OPEN_LRD.Foundations.Vector3), typeof(double) }, new[] { "N", "s" }, "N·s");
            AddUnits(functions, "BasicPhysics.Work", new[] { typeof(OPEN_LRD.Foundations.Vector3), typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "N", "m" }, "J");
            AddUnits(functions, "BasicPhysics.Power", new[] { typeof(double), typeof(double) }, new[] { "J", "s" }, "W");
            AddUnits(functions, "BasicPhysics.Power", new[] { typeof(OPEN_LRD.Foundations.Vector3), typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "N", "m/s" }, "W");
            AddUnits(functions, "BasicPhysics.Torque", new[] { typeof(OPEN_LRD.Foundations.Vector3), typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "m", "N" }, "N·m");
            AddUnits(functions, "BasicPhysics.RotationalPower", new[] { typeof(OPEN_LRD.Foundations.Vector3), typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "N·m", "rad/s" }, "W");
            AddUnits(functions, "Thermodynamics.IdealGasPressure", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "mol", "m³", "K" }, "Pa");
            AddUnits(functions, "Thermodynamics.IdealGasVolume", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "mol", "Pa", "K" }, "m³");
            AddUnits(functions, "Thermodynamics.IdealGasTemperature", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "mol", "Pa", "m³" }, "K");
            AddUnits(functions, "Thermodynamics.IdealGasMoles", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "Pa", "m³", "K" }, "mol");
            AddUnits(functions, "Thermodynamics.HeatTransfer", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "kg", "J/(kg·K)", "K" }, "J");
            AddUnits(functions, "Thermodynamics.LatentHeat", new[] { typeof(double), typeof(double) }, new[] { "kg", "J/kg" }, "J");
            AddUnits(functions, "Thermodynamics.InternalEnergyChange", new[] { typeof(double), typeof(double) }, new[] { "J", "J" }, "J");
            AddUnits(functions, "Thermodynamics.ThermalExpansion", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "m", "1/K", "K" }, "m");
            AddUnits(functions, "Thermodynamics.EntropyChange", new[] { typeof(double), typeof(double) }, new[] { "J", "K" }, "J/K");
            AddUnits(functions, "Thermodynamics.IdealGasIsothermalEntropyChange", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "mol", "m³", "m³" }, "J/K");
            AddUnits(functions, "Thermodynamics.IdealGasConstantVolumeEntropyChange", new[] { typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "mol", "K", "K", "J/(mol·K)" }, "J/K");
            AddUnits(functions, "Thermodynamics.AdiabaticFinalTemperature", new[] { typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "K", "m³", "m³", "1" }, "K");
            AddUnits(functions, "FluidMechanics.HydrostaticPressure", new[] { typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "Pa", "kg/m³", "m/s²", "m" }, "Pa");
            AddUnits(functions, "FluidMechanics.BuoyantForce", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "kg/m³", "m/s²", "m³" }, "N");
            AddUnits(functions, "FluidMechanics.MassFlowRate", new[] { typeof(double), typeof(double) }, new[] { "kg/m³", "m³/s" }, "kg/s");
            AddUnits(functions, "FluidMechanics.ContinuityVelocity", new[] { typeof(double), typeof(double), typeof(double) }, new[] { "m²", "m/s", "m²" }, "m/s");
            AddUnits(functions, "FluidMechanics.BernoulliPressure", new[] { typeof(double), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "Pa", "kg/m³", "m/s", "m/s", "m", "m/s²" }, "Pa");
            AddUnits(functions, "FluidMechanics.ReynoldsNumber", new[] { typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "kg/m³", "m/s", "m", "Pa·s" }, null);
            AddUnits(functions, "FluidMechanics.PoiseuilleFlowRate", new[] { typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "m", "Pa", "m", "Pa·s" }, "m³/s");
            AddUnits(functions, "FluidMechanics.DragForce", new[] { typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "1", "kg/m³", "m/s", "m²" }, "N");
            AddUnits(functions, "FluidMechanics.DarcyWeisbachPressureDrop", new[] { typeof(double), typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "1", "kg/m³", "m/s", "m", "m" }, "Pa");
            AddUnits(functions, "Electromagnetism.CoulombForce", new[] { typeof(double), typeof(double), typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "C", "C", "m" }, "N");
            AddUnits(functions, "Electromagnetism.ElectricFieldPointCharge", new[] { typeof(double), typeof(OPEN_LRD.Foundations.Vector3) }, new[] { "C", "m" }, "N/C");
            AddUnits(functions, "Electromagnetism.ElectricPotentialPointCharge", new[] { typeof(double), typeof(double) }, new[] { "C", "m" }, "V");
            AddUnits(functions, "Electromagnetism.OhmsLawCurrent", new[] { typeof(double), typeof(double) }, new[] { "V", "Ω" }, "A");
            AddUnits(functions, "Electromagnetism.OhmsLawVoltage", new[] { typeof(double), typeof(double) }, new[] { "A", "Ω" }, "V");
            AddUnits(functions, "Electromagnetism.OhmsLawResistance", new[] { typeof(double), typeof(double) }, new[] { "V", "A" }, "Ω");
            AddUnits(functions, "Electromagnetism.CapacitanceCharge", new[] { typeof(double), typeof(double) }, new[] { "F", "V" }, "C");
            AddUnits(functions, "Electromagnetism.CapacitorEnergy", new[] { typeof(double), typeof(double) }, new[] { "F", "V" }, "J");
            AddUnits(functions, "Electromagnetism.RcVoltage", new[] { typeof(double), typeof(double), typeof(double), typeof(double), typeof(double) }, new[] { "V", "V", "s", "Ω", "F" }, "V");
            AddUnits(functions, "Electromagnetism.MagneticFieldLongStraightWire", new[] { typeof(double), typeof(double) }, new[] { "A", "m" }, "T");
            return functions.AsReadOnly();
        }

        private static void AddUnits(List<FunctionDescriptor> functions, string key, Type[] parameterTypes,
            IReadOnlyList<string> inputUnits, string? outputUnit)
        {
            Dictionary<int, string> units = new Dictionary<int, string>();
            for (int index = 0; index < inputUnits.Count; index++) units.Add(index, inputUnits[index]);
            AddUnitMetadata(functions, key, parameterTypes, units, outputUnit);
        }

        private static bool IsSupportedDispatchType(Type type)
        {
            return type == typeof(double[]) || type == typeof(int[]) ||
                   (!type.IsArray && DispatchValueTypes.Contains(type));
        }

        private static void AddUnitMetadata(
            List<FunctionDescriptor> functions,
            string key,
            Type[] parameterTypes,
            IReadOnlyDictionary<int, string> inputUnits,
            string? outputUnit)
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
