﻿using System.Collections.Generic;
using Unrealscript.Analysis.Symbols;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class StructComparison : Expression
    {
        public bool IsEqual;
        public Expression LeftOperand;
        public Expression RightOperand;

        public Struct Struct;

        public int Precedence => IsEqual ? 24 : 26;

        public StructComparison(bool isEqual, Expression lhs, Expression rhs, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.InfixOperator, start, end)
        {
            IsEqual = isEqual;
            LeftOperand = lhs;
            RightOperand = rhs;
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.BoolType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return LeftOperand;
                yield return RightOperand;
            }
        }
    }
}