using System.IO;

namespace SuperSwitch.HLE.HOS.Diagnostics.Demangler.Ast
{
    public class ConversionOperatorType : ParentNode
    {
        public ConversionOperatorType(BaseNode child) : base(NodeType.ConversionOperatorType, child) { }

        public override void PrintLeft(TextWriter writer)
        {
            writer.Write("operator ");
            Child.Print(writer);
        }
    }
}
