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
            Value = value;
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            UnitTitle = NormalizeOptionalText(unitTitle);
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    internal sealed record FunctionResult(object? Value, Type DataType, string? UnitTitle);

    internal sealed class FunctionBox
    {
        private readonly Guid functionID;
        private readonly List<FunctionVariable> variables = new List<FunctionVariable>();
        private string? functionKey;
        private string? expectedResultUnitTitle;
        private FunctionResult? result;
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
            ExpectedResultType = expectedResultType ?? throw new ArgumentNullException(nameof(expectedResultType));
            this.functionKey = NormalizeOptionalText(functionKey);
            this.expectedResultUnitTitle = NormalizeOptionalText(expectedResultUnitTitle);
        }

        public Guid GetFunctionID()
        {
            return GraphSynchronization.Execute(() => functionID);
        }

        public void AddVariable(FunctionVariable variable)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }
            GraphSynchronization.Execute(() => { EnsureWritable(); variables.Add(variable); });
        }

        internal void ClearResult()
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); result = null; });
        }

        internal void StoreResult(FunctionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            GraphSynchronization.Execute(() => { EnsureWritable(); this.result = result; });
        }

        internal void MarkReadOnly() => GraphSynchronization.Execute(() => isReadOnly = true);

        internal void MarkWritable() => GraphSynchronization.Execute(() => isReadOnly = false);

        private void EnsureWritable()
        {
            if (isReadOnly) throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
