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

    /// <summary>Attribute declaring whether a skill function is deterministic and eligible for trusted promotion.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class FunctionCapabilityAttribute : Attribute
    {
        /// <summary>Gets whether the function produces deterministic results for identical inputs.</summary>
        public bool IsDeterministic { get; }

        /// <summary>Initializes capability metadata for a dispatchable skill method.</summary>
        public FunctionCapabilityAttribute(bool isDeterministic = true)
        {
            IsDeterministic = isDeterministic;
        }
    }

    /// <summary>Tracks dispatch verification state for a FunctionBox result.</summary>
    public enum FunctionResultVerificationState
    {
        /// <summary>Result was stored manually or lacks dispatch verification.</summary>
        Unverified = 0,
        /// <summary>Result was verified by deterministic dispatcher execution.</summary>
        DispatchVerified = 1,
        /// <summary>Result was produced by non-deterministic execution; staged with provenance and cannot become trusted.</summary>
        StagedNonDeterministic = 2
    }

    /// <summary>Immutable audit metadata for a function result produced by dispatcher verification or dispatch.</summary>
    public sealed record FunctionDispatchProvenance(
        string FunctionKey,
        string ArgumentSnapshotHash,
        string ResultHash,
        string ResultType,
        string? ResultUnitTitle,
        long DefinitionRevision,
        DateTimeOffset DispatchedAtUtc,
        string DispatcherVersion,
        bool IsDeterministic = true,
        long? RandomSeed = null,
        string? CallerSource = null,
        string? ArgumentSnapshotSummary = null);

    /// <summary>Immutable snapshot of a function result's dispatch provenance and trust status.</summary>
    public sealed class FunctionDispatchAudit
    {
        /// <summary>Gets the identity of the function box this audit describes.</summary>
        public Guid FunctionId { get; }

        /// <summary>Gets whether the function box is trusted.</summary>
        public bool IsTrusted { get; }

        /// <summary>Gets whether the current result has deterministic dispatch verification.</summary>
        public bool IsResultVerified { get; }

        /// <summary>Gets the detailed verification status of the current function result.</summary>
        public FunctionResultVerificationState VerificationStatus { get; }

        /// <summary>Gets provenance for the current result, when available.</summary>
        public FunctionDispatchProvenance? ActiveProvenance { get; }

        /// <summary>Gets a read-only snapshot of provenance entries retained for this function box.</summary>
        public IReadOnlyList<FunctionDispatchProvenance> ProvenanceHistory { get; }

        /// <summary>Gets any diagnostics or warnings emitted during dispatch or automated reasoning evaluation.</summary>
        public IReadOnlyList<string> DiagnosticsWarnings { get; }

        internal FunctionDispatchAudit(Guid functionId, bool isTrusted, bool isResultVerified,
            FunctionDispatchProvenance? activeProvenance, IEnumerable<FunctionDispatchProvenance> provenanceHistory,
            FunctionResultVerificationState verificationStatus = FunctionResultVerificationState.Unverified,
            IEnumerable<string>? diagnosticsWarnings = null)
        {
            FunctionId = functionId;
            IsTrusted = isTrusted;
            IsResultVerified = isResultVerified;
            VerificationStatus = verificationStatus;
            ActiveProvenance = activeProvenance;
            ProvenanceHistory = new ReadOnlyCollection<FunctionDispatchProvenance>(provenanceHistory.ToList());
            List<string> warnings = diagnosticsWarnings?.ToList() ?? new List<string>();
            if (verificationStatus == FunctionResultVerificationState.StagedNonDeterministic && warnings.Count == 0)
            {
                warnings.Add($"Non-deterministic function '{activeProvenance?.FunctionKey ?? functionId.ToString()}' is staged-only and cannot be promoted to trusted state.");
            }
            DiagnosticsWarnings = new ReadOnlyCollection<string>(warnings);
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
        private FunctionDispatchProvenance? dispatchProvenance;
        private readonly List<FunctionDispatchProvenance> provenanceHistory = new List<FunctionDispatchProvenance>();
        private FunctionResultVerificationState resultVerificationState = FunctionResultVerificationState.Unverified;
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
        internal FunctionDispatchProvenance? DispatchProvenance => GraphSynchronization.Execute(() => dispatchProvenance);
        internal IReadOnlyList<FunctionDispatchProvenance> DispatchProvenanceHistory =>
            GraphSynchronization.Execute<IReadOnlyList<FunctionDispatchProvenance>>(() => new ReadOnlyCollection<FunctionDispatchProvenance>(provenanceHistory.ToList()));
        internal long ResultDefinitionRevision => GraphSynchronization.Execute(() => resultDefinitionRevision ?? 0);
        internal FunctionResultVerificationState ResultVerificationState => GraphSynchronization.Execute(() => resultVerificationState);
        internal bool IsResultDispatchVerified => GraphSynchronization.Execute(() => resultVerificationState == FunctionResultVerificationState.DispatchVerified);
        internal bool IsTrusted => GraphSynchronization.Execute(() => isTrusted);

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
                dispatchProvenance = null;
                provenanceHistory.Clear();
                resultVerificationState = FunctionResultVerificationState.Unverified;
            });
        }

        /// <summary>Stores a function result manually and marks it unverified until dispatch verification occurs.</summary>
        internal void StoreResult(FunctionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            ValidateResult(result);
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                this.result = result;
                resultDefinitionRevision = definitionRevision;
                dispatchProvenance = null;
                provenanceHistory.Clear();
                resultVerificationState = FunctionResultVerificationState.Unverified;
            });
        }

        internal bool TryStoreDispatchResult(FunctionResult result, long expectedDefinitionRevision, FunctionDispatchProvenance provenance)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(provenance);
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
                RecordDispatchProvenance(provenance);
                resultVerificationState = provenance.IsDeterministic
                    ? FunctionResultVerificationState.DispatchVerified
                    : FunctionResultVerificationState.StagedNonDeterministic;
                return true;
            });
        }

        /// <summary>
        /// Stages a non-deterministic function result for reasoning assertion citation without promoting it to trusted state.
        /// Records invocation arguments, optional random seed, caller/source metadata, and UTC timestamp in provenance.
        /// </summary>
        public void StageNonDeterministicResult(
            FunctionResult result,
            long? randomSeed = null,
            string? callerSource = null,
            DateTimeOffset? invokedAtUtc = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            ValidateResult(result);
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                if (string.IsNullOrWhiteSpace(functionKey))
                {
                    throw new InvalidOperationException("A FunctionKey is required to stage a non-deterministic function result.");
                }
                string normalizedKey = functionKey.Trim();
                string argSummary = BuildArgumentSnapshotSummary(variables);
                string argHash = ComputeStagedSnapshotHash(normalizedKey, definitionRevision, argSummary);
                string resultHash = ComputeStagedSnapshotHash(result.DataType.FullName ?? result.DataType.Name, 0, $"{result.Value}:{result.UnitTitle}");
                string resultType = result.DataType.AssemblyQualifiedName ?? result.DataType.FullName ?? result.DataType.Name;
                FunctionDispatchProvenance provenance = new FunctionDispatchProvenance(
                    normalizedKey,
                    argHash,
                    resultHash,
                    resultType,
                    result.UnitTitle,
                    definitionRevision,
                    invokedAtUtc ?? DateTimeOffset.UtcNow,
                    typeof(FunctionBox).Assembly.GetName().Version?.ToString() ?? "0.0.0.0",
                    IsDeterministic: false,
                    RandomSeed: randomSeed,
                    CallerSource: NormalizeOptionalText(callerSource),
                    ArgumentSnapshotSummary: argSummary);
                this.result = result;
                resultDefinitionRevision = definitionRevision;
                RecordDispatchProvenance(provenance);
                resultVerificationState = FunctionResultVerificationState.StagedNonDeterministic;
            });
        }

        /// <summary>
        /// Returns whether this function box has valid staged non-deterministic provenance
        /// (invocation arguments, timestamp, and current definition revision) suitable for reasoning evidence citation.
        /// </summary>
        internal bool HasValidStagedNonDeterministicProvenance => GraphSynchronization.Execute(() =>
            !isTrusted &&
            result != null &&
            resultDefinitionRevision == definitionRevision &&
            resultVerificationState == FunctionResultVerificationState.StagedNonDeterministic &&
            dispatchProvenance != null &&
            !dispatchProvenance.IsDeterministic &&
            dispatchProvenance.DefinitionRevision == definitionRevision &&
            !string.IsNullOrWhiteSpace(dispatchProvenance.FunctionKey) &&
            !string.IsNullOrWhiteSpace(dispatchProvenance.ArgumentSnapshotHash) &&
            !string.IsNullOrWhiteSpace(dispatchProvenance.ArgumentSnapshotSummary) &&
            dispatchProvenance.DispatchedAtUtc != default);

        internal static string BuildArgumentSnapshotSummary(IReadOnlyList<FunctionVariable> vars)
        {
            return string.Join("; ", vars.Select(v =>
                $"{v.Name}:{v.DataType.Name}={FormatVariableValue(v.Value)}{(v.UnitTitle != null ? $" [{v.UnitTitle}]" : string.Empty)}"));
        }

        private static string FormatVariableValue(object? value)
        {
            if (value == null) return "null";
            if (value is double d) return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (value is float f) return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (value is Array arr) return $"[{string.Join(",", arr.Cast<object?>().Select(FormatVariableValue))}]";
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string ComputeStagedSnapshotHash(string key, long revision, string payload)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"{key}|{revision}|{payload}");
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        }

        internal void MarkResultUnverified()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                resultVerificationState = FunctionResultVerificationState.Unverified;
            });
        }

        internal void RecordDispatchProvenance(FunctionDispatchProvenance provenance)
        {
            ArgumentNullException.ThrowIfNull(provenance);
            if (dispatchProvenance != null && !ProvenanceEquivalent(dispatchProvenance, provenance))
            {
                provenanceHistory.Add(dispatchProvenance);
            }
            dispatchProvenance = provenance;
            if (!provenanceHistory.Any(existing => ProvenanceEquivalent(existing, provenance)))
            {
                provenanceHistory.Add(provenance);
            }
        }

        private static bool ProvenanceEquivalent(FunctionDispatchProvenance left, FunctionDispatchProvenance right)
        {
            return string.Equals(left.FunctionKey, right.FunctionKey, StringComparison.Ordinal) &&
                   string.Equals(left.ArgumentSnapshotHash, right.ArgumentSnapshotHash, StringComparison.Ordinal) &&
                   string.Equals(left.ResultHash, right.ResultHash, StringComparison.Ordinal) &&
                   string.Equals(left.ResultType, right.ResultType, StringComparison.Ordinal) &&
                   string.Equals(left.ResultUnitTitle, right.ResultUnitTitle, StringComparison.Ordinal) &&
                   left.DefinitionRevision == right.DefinitionRevision &&
                   left.DispatchedAtUtc == right.DispatchedAtUtc &&
                   string.Equals(left.DispatcherVersion, right.DispatcherVersion, StringComparison.Ordinal) &&
                   left.IsDeterministic == right.IsDeterministic &&
                   left.RandomSeed == right.RandomSeed &&
                   string.Equals(left.CallerSource, right.CallerSource, StringComparison.Ordinal) &&
                   string.Equals(left.ArgumentSnapshotSummary, right.ArgumentSnapshotSummary, StringComparison.Ordinal);
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
                if (resultVerificationState != FunctionResultVerificationState.DispatchVerified || dispatchProvenance == null)
                {
                    throw new InvalidOperationException("A FunctionBox result must be dispatch-verified before it can be trusted.");
                }
                if (!dispatchProvenance.IsDeterministic)
                {
                    throw new InvalidOperationException("Non-deterministic function results cannot become trusted.");
                }
                if (dispatchProvenance.DefinitionRevision != definitionRevision)
                {
                    throw new InvalidOperationException("Dispatch provenance is stale for the current FunctionBox definition.");
                }
            });
        }

        internal void SetDispatchProvenance(FunctionDispatchProvenance? provenance)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                dispatchProvenance = provenance;
                provenanceHistory.Clear();
                if (provenance != null)
                {
                    provenanceHistory.Add(provenance);
                }
                resultVerificationState = provenance == null
                    ? FunctionResultVerificationState.Unverified
                    : provenance.IsDeterministic
                        ? FunctionResultVerificationState.DispatchVerified
                        : FunctionResultVerificationState.StagedNonDeterministic;
            });
        }

        internal void ReplaceDispatchProvenanceHistory(IEnumerable<FunctionDispatchProvenance>? provenanceHistoryEntries, FunctionDispatchProvenance? activeProvenance)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                provenanceHistory.Clear();
                if (provenanceHistoryEntries != null)
                {
                    foreach (FunctionDispatchProvenance entry in provenanceHistoryEntries)
                    {
                        if (entry == null) continue;
                        if (!provenanceHistory.Any(existing => ProvenanceEquivalent(existing, entry)))
                        {
                            provenanceHistory.Add(entry);
                        }
                    }
                }
                dispatchProvenance = activeProvenance;
                if (activeProvenance != null && !provenanceHistory.Any(existing => ProvenanceEquivalent(existing, activeProvenance)))
                {
                    provenanceHistory.Add(activeProvenance);
                }
                resultVerificationState = activeProvenance == null
                    ? FunctionResultVerificationState.Unverified
                    : activeProvenance.IsDeterministic
                        ? FunctionResultVerificationState.DispatchVerified
                        : FunctionResultVerificationState.StagedNonDeterministic;
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
