using SuperSwitch.Graphics.Shader.StructuredIr;
using SuperSwitch.Graphics.Shader.Translation;

using static SuperSwitch.Graphics.Shader.CodeGen.Glsl.Instructions.InstGenHelper;

namespace SuperSwitch.Graphics.Shader.CodeGen.Glsl.Instructions
{
    static class InstGenShuffle
    {
        public static string Shuffle(CodeGenContext context, AstOperation operation)
        {
            string value = GetSourceExpr(context, operation.GetSource(0), AggregateType.FP32);
            string index = GetSourceExpr(context, operation.GetSource(1), AggregateType.U32);

            if (context.HostCapabilities.SupportsShaderBallot)
            {
                return $"readInvocationARB({value}, {index})";
            }
            else
            {
                return $"subgroupShuffle({value}, {index})";
            }
        }
    }
}
