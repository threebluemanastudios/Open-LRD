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

    internal readonly record struct FunctionDispatchSnapshot(
        long DefinitionRevision,
        string? FunctionKey,
        IReadOnlyList<FunctionVariable> Variables,
        Type ExpectedResultType,
        string? ExpectedResultUnitTitle,
        bool IsReadOnly);

    internal sealed class FunctionBox
    {
        /// <summary>Maximum number of input variables allowed per function definition.</summary>
        internal const int MaximumVariableCount = 256;

        private readonly Guid functionID;
        private readonly List<FunctionVariable> variables = new List<FunctionVariable>();
        private string? functionKey;
        private string? expectedResultUnitTitle;
        private FunctionResult? result;
        private long? resultDefinitionRevision;
        private bool isTrusted;
        private bool isReadOnly;
        private long definitionRevision;

        public string? FunctionKey
        {
            get => GraphSynchronization.Execute(() => functionKey);
            set => GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                string? normalized = NormalizeOptionalText(value);
                if (!string.Equals(functionKey, normalized, StringComparison.Ordinal))
                {
                    long nextRevision = GetNextDefinitionRevision();
                    functionKey = normalized;
                    definitionRevision = nextRevision;
                }
            });
        }

        public Guid FunctionID => GetFunctionID();
        public IReadOnlyList<FunctionVariable> Variables => GraphSynchronization.Execute<IReadOnlyList<FunctionVariable>>(
            () => new ReadOnlyCollection<FunctionVariable>(variables.ToList()));
        public Type ExpectedResultType { get; }

        public string? ExpectedResultUnitTitle
        {
            get => GraphSynchronization.Execute(() => expectedResultUnitTitle);
            set => GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                string? normalized = NormalizeOptionalText(value);
                if (!string.Equals(expectedResultUnitTitle, normalized, StringComparison.Ordinal))
                {
                    long nextRevision = GetNextDefinitionRevision();
                    expectedResultUnitTitle = normalized;
                    definitionRevision = nextRevision;
                }
            });
        }

        public FunctionResult? Result => GraphSynchronization.Execute(() => result);

        public FunctionBox(Type expectedResultType, string? functionKey = null, string? expectedResultUnitTitle = null)
            : this(LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Function), expectedResultType, functionKey, expectedResultUnitTitle, allowRegisteredTrustedId: true)
        {
        }

        public FunctionBox(Guid functionId, Type expectedResultType, string? functionKey = null, string? expectedResultUnitTitle = null)
            : this(functionId, expectedResultType, functionKey, expectedResultUnitTitle, allowRegisteredTrustedId: false)
        {
        }

        internal FunctionBox(Guid functionId, Type expectedResultType, string? functionKey, string? expectedResultUnitTitle, bool allowRegisteredTrustedId)
        {
            if (allowRegisteredTrustedId)
            {
                if (functionId == Guid.Empty)
                {
                    throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", nameof(functionId));
                }
            }
            else
            {
                LRD_Core.Instance.ValidateNewUntrustedId(NodeTargetKind.Function, functionId, nameof(functionId));
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
                long nextRevision = GetNextDefinitionRevision();
                variables.Add(variable);
                definitionRevision = nextRevision;
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
                long nextRevision = GetNextDefinitionRevision();
                if (index < 0)
                {
                    variables.Add(variable);
                }
                else
                {
                    variables[index] = variable;
                }
                definitionRevision = nextRevision;
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
                long nextRevision = GetNextDefinitionRevision();
                variables.RemoveAt(index);
                definitionRevision = nextRevision;
                return true;
            });
        }

        internal FunctionDispatchSnapshot GetDispatchSnapshot()
        {
            return GraphSynchronization.Execute(() => new FunctionDispatchSnapshot(
                definitionRevision,
                functionKey,
                new ReadOnlyCollection<FunctionVariable>(variables.ToList()),
                ExpectedResultType,
                expectedResultUnitTitle,
                isReadOnly));
        }

        internal void ClearResult()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                result = null;
                resultDefinitionRevision = null;
            });
        }

        internal void StoreResult(FunctionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            ValidateResult(result);
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                this.result = result;
                resultDefinitionRevision = definitionRevision;
            });
        }

        internal bool TryStoreDispatchResult(FunctionResult result, long expectedDefinitionRevision)
        {
            ArgumentNullException.ThrowIfNull(result);
            ValidateResult(result);
            return GraphSynchronization.Execute(() =>
            {
                if (definitionRevision != expectedDefinitionRevision)
                {
                    throw new InvalidOperationException("The FunctionBox definition changed during dispatch; the result was not stored.");
                }
                if (isReadOnly)
                {
                    return false;
                }
                this.result = result;
                resultDefinitionRevision = definitionRevision;
                return true;
            });
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
                if (resultDefinitionRevision != definitionRevision)
                {
                    throw new InvalidOperationException("The FunctionBox result is stale; dispatch the current definition before trusting it.");
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

        private long GetNextDefinitionRevision() => checked(definitionRevision + 1);

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
