using System.IO;

namespace SuperSwitch.HLE.HOS.Diagnostics.Demangler.Ast
{
    public class GlobalQualifiedName : ParentNode
    {
        public GlobalQualifiedName(BaseNode child) : base(NodeType.GlobalQualifiedName, child) { }

        public override void PrintLeft(TextWriter writer)
        {
            writer.Write("::");
            Child.Print(writer);
        }
    }
}
