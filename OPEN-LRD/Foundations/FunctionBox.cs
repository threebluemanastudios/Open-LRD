using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OPEN_LRD.Foundations
{
    internal sealed class FunctionVariable
    {
        public string Name { get; }
        public object? Value { get; }
        public Type DataType { get; }
        public string? UnitTitle { get; }

        public FunctionVariable(string name, object? value, Type dataType, string? unitTitle = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A variable name is required.", nameof(name));
            }

            Name = name.Trim();
            DataType = FunctionValueValidation.ValidateType(dataType, nameof(dataType));
            FunctionValueValidation.ValidateValue(value, DataType, nameof(value));
            Value = value;
            UnitTitle = NormalizeOptionalText(unitTitle);
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    internal sealed record FunctionResult
    {
        public object? Value { get; }
        public Type DataType { get; }
        public string? UnitTitle { get; }

        public FunctionResult(object? value, Type dataType, string? unitTitle)
        {
            DataType = FunctionValueValidation.ValidateType(dataType, nameof(dataType));
            FunctionValueValidation.ValidateValue(value, DataType, nameof(value));
            Value = value;
            UnitTitle = string.IsNullOrWhiteSpace(unitTitle) ? null : unitTitle.Trim();
        }
    }

    internal static class FunctionValueValidation
    {
        internal static Type ValidateType(Type? type, string parameterName)
        {
            if (type == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (type == typeof(void) || type.IsByRef || type.IsPointer || type.ContainsGenericParameters)
            {
                throw new ArgumentException("Function value types must be closed, non-void runtime types.", parameterName);
            }
            return type;
        }

        internal static void ValidateValue(object? value, Type declaredType, string parameterName)
        {
            Type? nullableType = Nullable.GetUnderlyingType(declaredType);
            if (value == null)
            {
                if (declaredType.IsValueType && nullableType == null)
                {
                    throw new ArgumentException("Null is only valid for reference or nullable types.", parameterName);
                }
                return;
            }

            Type valueType = nullableType ?? declaredType;
            if (!valueType.IsInstanceOfType(value))
            {
                throw new ArgumentException(
                    $"The value must be an instance of declared type '{declaredType.FullName}'.",
                    parameterName);
            }
        }
    }

    internal sealed class FunctionBox
    {
        /// <summary>Maximum number of input variables allowed per function definition.</summary>
        internal const int MaximumVariableCount = 256;

        private readonly Guid functionID;
        private readonly List<FunctionVariable> variables = new List<FunctionVariable>();
        private string? functionKey;
        private string? expectedResultUnitTitle;
        private FunctionResult? result;
        private bool isTrusted;
        private bool isReadOnly;

        public string? FunctionKey
        {
            get => GraphSynchronization.Execute(() => functionKey);
            set => GraphSynchronization.Execute(() => { EnsureWritable(); functionKey = NormalizeOptionalText(value); });
        }

        public Guid FunctionID => GetFunctionID();
        public IReadOnlyList<FunctionVariable> Variables => GraphSynchronization.Execute<IReadOnlyList<FunctionVariable>>(
            () => new ReadOnlyCollection<FunctionVariable>(variables.ToList()));
        public Type ExpectedResultType { get; }

        public string? ExpectedResultUnitTitle
        {
            get => GraphSynchronization.Execute(() => expectedResultUnitTitle);
            set => GraphSynchronization.Execute(() => { EnsureWritable(); expectedResultUnitTitle = NormalizeOptionalText(value); });
        }

        public FunctionResult? Result => GraphSynchronization.Execute(() => result);
        internal bool IsReadOnly => GraphSynchronization.Execute(() => isReadOnly);

        public FunctionBox(Type expectedResultType, string? functionKey = null, string? expectedResultUnitTitle = null)
            : this(Guid.NewGuid(), expectedResultType, functionKey, expectedResultUnitTitle)
        {
        }

        public FunctionBox(Guid functionId, Type expectedResultType, string? functionKey = null, string? expectedResultUnitTitle = null)
        {
            if (functionId == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", nameof(functionId));
            }
            functionID = functionId;
            ExpectedResultType = FunctionValueValidation.ValidateType(expectedResultType, nameof(expectedResultType));
            this.functionKey = NormalizeOptionalText(functionKey);
            this.expectedResultUnitTitle = NormalizeOptionalText(expectedResultUnitTitle);
        }

        public Guid GetFunctionID()
        {
            return GraphSynchronization.Execute(() => functionID);
        }

        public void AddVariable(FunctionVariable variable)
        {
            ArgumentNullException.ThrowIfNull(variable);
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                if (variables.Count >= MaximumVariableCount)
                {
                    throw new InvalidOperationException($"A function can declare at most {MaximumVariableCount} variables.");
                }
                if (variables.Any(existing => string.Equals(existing.Name, variable.Name, StringComparison.Ordinal)))
                {
                    throw new ArgumentException($"Variable '{variable.Name}' is already declared.", nameof(variable));
                }
                variables.Add(variable);
            });
        }

        public void SetVariable(FunctionVariable variable)
        {
            ArgumentNullException.ThrowIfNull(variable);
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                int index = variables.FindIndex(existing => string.Equals(existing.Name, variable.Name, StringComparison.Ordinal));
                if (index < 0 && variables.Count >= MaximumVariableCount)
                {
                    throw new InvalidOperationException($"A function can declare at most {MaximumVariableCount} variables.");
                }
                if (index < 0)
                {
                    variables.Add(variable);
                }
                else
                {
                    variables[index] = variable;
                }
            });
        }

        public FunctionVariable? GetVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A variable name is required.", nameof(name));
            }
            string normalizedName = name.Trim();
            return GraphSynchronization.Execute(() => variables.FirstOrDefault(variable =>
                string.Equals(variable.Name, normalizedName, StringComparison.Ordinal)));
        }

        public bool RemoveVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A variable name is required.", nameof(name));
            }
            string normalizedName = name.Trim();
            return GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                int index = variables.FindIndex(variable => string.Equals(variable.Name, normalizedName, StringComparison.Ordinal));
                if (index < 0)
                {
                    return false;
                }
                variables.RemoveAt(index);
                return true;
            });
        }

        internal void ClearResult()
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); result = null; });
        }

        internal void StoreResult(FunctionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            ValidateResult(result);
            GraphSynchronization.Execute(() => { EnsureWritable(); this.result = result; });
        }

        internal void ValidateDefinition()
        {
            GraphSynchronization.Execute(() =>
            {
                if (variables.Count > MaximumVariableCount)
                {
                    throw new InvalidOperationException($"A function can declare at most {MaximumVariableCount} variables.");
                }
                HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
                foreach (FunctionVariable variable in variables)
                {
                    if (!names.Add(variable.Name))
                    {
                        throw new InvalidOperationException($"Variable '{variable.Name}' is declared more than once.");
                    }
                }
                if (result == null)
                {
                    throw new InvalidOperationException("A FunctionBox must have a result before it can be trusted.");
                }
                ValidateResult(result);
            });
        }

        private void ValidateResult(FunctionResult functionResult)
        {
            Type expectedValueType = Nullable.GetUnderlyingType(ExpectedResultType) ?? ExpectedResultType;
            if (!ExpectedResultType.IsAssignableFrom(functionResult.DataType) &&
                expectedValueType != functionResult.DataType)
            {
                throw new ArgumentException(
                    $"Result type '{functionResult.DataType.FullName}' is not assignable to expected type '{ExpectedResultType.FullName}'.",
                    nameof(functionResult));
            }
            FunctionValueValidation.ValidateValue(functionResult.Value, ExpectedResultType, nameof(functionResult));
        }

        internal void MarkTrusted()
        {
            ValidateDefinition();
            GraphSynchronization.Execute(() => { isTrusted = true; isReadOnly = true; });
        }

        internal void MarkReadOnly() => MarkTrusted();

        internal void MarkWritable() => GraphSynchronization.Execute(() =>
        {
            if (isTrusted)
            {
                throw new InvalidOperationException("A trusted FunctionBox cannot return to the untrusted staging registry.");
            }
            isReadOnly = false;
        });

        private void EnsureWritable()
        {
            if (isReadOnly) throw new InvalidOperationException("Trusted or disk-paged function definitions are read-only.");
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
