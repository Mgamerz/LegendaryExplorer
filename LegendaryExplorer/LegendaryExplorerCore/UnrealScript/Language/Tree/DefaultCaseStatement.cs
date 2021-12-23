﻿using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DefaultCaseStatement : Statement
    {
        public DefaultCaseStatement(SourcePosition start, SourcePosition end)
            : base(ASTNodeType.DefaultStatement, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}