using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace SuperSwitch.Horizon.Generators.Hipc
{
    class CommandInterface
    {
        public ClassDeclarationSyntax ClassDeclarationSyntax { get; }
        public List<MethodDeclarationSyntax> CommandImplementations { get; }

        public CommandInterface(ClassDeclarationSyntax classDeclarationSyntax)
        {
            ClassDeclarationSyntax = classDeclarationSyntax;
            CommandImplementations = [];
        }
    }
}
