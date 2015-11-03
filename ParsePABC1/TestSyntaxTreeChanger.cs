using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PascalABCCompiler;
using PascalABCCompiler.SyntaxTree;

using SyntaxVisitors;

namespace ParsePABC1
{
    public class TestSyntaxTreeChanger : ISyntaxTreeChanger
    {
        public void Change(syntax_tree_node sn)
        {
            //sn.visit(new CalcConstExprs());
            sn.visit(new LoweringVisitor());
            sn.visit(new ProcessYieldCapturedVarsVisitor());
            
        }

    }
}
