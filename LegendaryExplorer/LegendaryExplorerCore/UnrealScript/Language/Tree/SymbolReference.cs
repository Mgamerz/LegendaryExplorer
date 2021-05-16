﻿using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class SymbolReference : Expression
    {
        public virtual ASTNode Node { get; }
        public string Name;

        public bool IsGlobal;
        public bool IsSuper;
        public VariableType SuperSpecifier;

        public SymbolReference(ASTNode symbol, string name = "", SourcePosition start = null, SourcePosition end = null) 
            : base(ASTNodeType.SymbolReference, start, end)
        {
            Node = symbol;
            Name = name;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return Node switch
            {
                VariableDeclaration variable => variable.VarType,
                Function func => func.VarType,
                VariableType type => type,
                EnumValue eVal => eVal.Enum,
                _ => (Node as Expression)?.ResolveType()
            };
        }
    }
}