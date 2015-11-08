using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PascalABCCompiler;
using PascalABCCompiler.SyntaxTree;

namespace SyntaxVisitors
{
    public class ReplaceFormalParametersRefsVisitor : BaseChangeVisitor
    {
        // Для вложенных функций
        private List<HashSet<string>> formalParameters = new List<HashSet<string>>();

        public ReplaceFormalParametersRefsVisitor()
        {
            //this.formalParameters = new HashSet<string>(formalParameters);
        }

        public override void visit(procedure_definition pd)
        {
            if ((object)pd.proc_header.parameters == null)
            {
                base.visit(pd.proc_body);
                return;
            }

            formalParameters.Add(new HashSet<string>());

            foreach (var plist in pd.proc_header.parameters.params_list)
            {
                foreach (var id in plist.idents.idents)
                {
                    formalParameters[formalParameters.Count - 1].Add(id.name);
                }
            }

            base.visit(pd.proc_body);

            formalParameters.RemoveAt(formalParameters.Count - 1);
        }


        public override void visit(ident id)
        {
            if (formalParameters.Select(fp => fp.Contains(id.name)).Contains(true))
            {
                var upper = UpperNode();
                // Подозреваем обращение к параметру метода
                if ((object)upper == null || (object)upper != null && (upper as dot_node) == null)
                {
                    // Это не self.paramName - поле класса и не что-то другое?
                    // Нашли обращение к параметру?

                    var self = new ident("self", id.source_context);

                    // Заменяем
                    var selfId = new dot_node(self, id);

                    Replace(id, selfId);
                }
            }
        }
      
    }
}
