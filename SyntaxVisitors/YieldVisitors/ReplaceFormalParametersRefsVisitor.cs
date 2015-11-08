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

        public HashSet<string> CollectedFormalParameters = new HashSet<string>();

        // Типа-стек соответствий paramName -> <>num__paramName для разных уровней вложенности методов
        private List<Dictionary<string, string>> formalParametersStack = new List<Dictionary<string, string>>();

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

            //formalParameters.Add(new HashSet<string>());
            formalParametersStack.Add(new Dictionary<string, string>());
            int currentLevel = formalParametersStack.Count - 1;

            foreach (var plist in pd.proc_header.parameters.params_list)
            {
                foreach (var id in plist.idents.idents)
                {
                    //formalParameters[formalParameters.Count - 1].Add(id.name);

                    var paramName = id.name;
                    var hoistedParamName = HoistParametersHelper.MakeHoistedFormalParameterName(id.name);

                    formalParametersStack[currentLevel].Add(paramName, hoistedParamName);

                    CollectedFormalParameters.Add(hoistedParamName);

                }
            }

            base.visit(pd.proc_body);

            //formalParameters.RemoveAt(formalParameters.Count - 1);

            formalParametersStack.RemoveAt(currentLevel);
        }


        public override void visit(ident id)
        {
            int? paramNameLevel = null;

            var paramName = id.name;

            // Ищем с какого уровня имя
            for (int level = formalParametersStack.Count - 1; level >= 0; --level)
            {
                if (formalParametersStack[level].ContainsKey(paramName))
                {
                    // Нашли!
                    paramNameLevel = level;
                    break;
                }
            }

            //if (formalParameters.Select(fp => fp.Contains(id.name)).Contains(true))

            if ((object)paramNameLevel != null)
            {
                var upper = UpperNode();
                // Подозреваем обращение к параметру метода
                if ((object)upper == null || (object)upper != null && (upper as dot_node) == null)
                {
                    // Это не self.paramName - поле класса и не что-то другое?
                    // Нашли обращение к параметру?

                    var self = new ident("self", id.source_context);

                    // Заменяем paramName -> self.hoistedParamName: <>num__paramName

                    //var selfId = new dot_node(self, id);

                    var hoistedParamName = new ident(formalParametersStack[(int)paramNameLevel][paramName], id.source_context);

                    var selfId = new dot_node(self, hoistedParamName);

                    Replace(id, selfId);
                }
            }
        }

    }
}
