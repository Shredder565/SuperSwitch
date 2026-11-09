using SuperSwitch.Graphics.Shader.Translation;
using Spv.Generator;

namespace SuperSwitch.Graphics.Shader.CodeGen.Spirv
{
    readonly struct OperationResult
    {
        public static OperationResult Invalid => new(AggregateType.Invalid, null);

        public AggregateType Type { get; }
        public Instruction Value { get; }

        public OperationResult(AggregateType type, Instruction value)
        {
            Type = type;
            Value = value;
        }
    }
}
