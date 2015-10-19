using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PascalABCCompiler.Errors;
using PascalABCCompiler;
using PascalABCCompiler.SyntaxTree;
using PascalABCCompiler.ParserTools;

using SyntaxVisitors;

namespace ParsePABC1
{
    class Program
    {
        static syntax_tree_node ParseFile(string fname)
        {
            Compiler c = new Compiler();

            var err = new List<Error>();

            var txt = System.IO.File.ReadAllText(fname);

            var cu = c.ParsersController.Compile(fname, txt, err, PascalABCCompiler.Parsers.ParseMode.Normal);

            if (cu == null)
            {
                Console.WriteLine("Не распарсилось");
            }
            return cu;
        }

        static void Main(string[] args)
        {
            var cu = ParseFile(@"C:\Users\Oleg\Documents\Visual Studio 2015\Projects\C#\Compilers\pascalabcnet\_ParsePABC1\yield1.pas");
            if (cu == null)
                return;

            //CodeFormatters.CodeFormatter cf = new CodeFormatters.CodeFormatter(0);
            //txt = cf.FormatTree(txt, cu as compilation_unit, 0, 0);

            cu.visit(new ChangeWhileVisitor());
            cu.visit(new DeleteRedundantBeginEnds());

            /*cu.visit(new CollectUpperNamespacesVisitor());

            var allv = new AllVarsInProcYields();
            cu.visit(allv);
            allv.PrintDict();*/

            /*var cnt = new CountNodesVisitor();
            cu.visit(cnt);
            cnt.PrintSortedByValue();*/

            var ld = new HashSet<string>();
            ld.Add("p1");
            var dld = new DeleteLocalDefs(ld);
            cu.visit(dld);

            cu.visit(new SimplePrettyPrinterVisitor());

            Console.ReadKey();
        }
    }
}
