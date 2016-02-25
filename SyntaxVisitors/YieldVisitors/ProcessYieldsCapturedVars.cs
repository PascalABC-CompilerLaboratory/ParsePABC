using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PascalABCCompiler;
using PascalABCCompiler.SyntaxTree;

using PascalABCCompiler.ParserTools;
using PascalABCCompiler.Errors;


namespace SyntaxVisitors
{
    public static class Consts
    {
        public static string Current = "<>2__current";
        public static string State = "<>1__state";

        public static string Self = "<>4__self";

        public enum ReservedNum { StateField = 1, CurrentField = 2, MethodFormalParam = 3, MethodSelf = 4, MethodLocalVariable = 5 }
    }

    public static class CapturedNamesHelper
    {
        public static int CurrentLocalVariableNum = 0;

        public static string MakeCapturedFormalParameterName(string formalParamName)
        {
            return string.Format("<>{0}__{1}", Consts.ReservedNum.MethodFormalParam, formalParamName);
        }

        public static string MakeCapturedLocalName(string localName)
        {
            return string.Format("<{0}>{1}__{2}", localName, Consts.ReservedNum.MethodLocalVariable, ++CurrentLocalVariableNum);
        }
    }

    public class ProcessYieldCapturedVarsVisitor : BaseChangeVisitor
    {
        int clnum = 0;

        public string newClassName()
        {
            clnum++;
            return "clyield#" + clnum.ToString();
        }

        public FindMainIdentsVisitor mids; // захваченные переменные процедуры по всем её yield 

        public int countNodesVisited;

        public bool hasYields = false;

        public static ProcessYieldCapturedVarsVisitor New
        {
            get { return new ProcessYieldCapturedVarsVisitor(); }
        }

        public ProcessYieldCapturedVarsVisitor()
        {
            //PrintInfo = false; 
        }

        public override void Enter(syntax_tree_node st)
        {
            base.Enter(st);
            countNodesVisited++;

            // сокращение обходимых узлов. Как сделать фильтр по тем узлам, которые необходимо обходить? Например, все операторы (без выражений и описаний), все описания (без операторов)
            if (st is assign || st is var_def_statement || st is procedure_call || st is procedure_header || st is expression)
            {
                visitNode = false; // фильтр - куда не заходить 
            }
        }

        /*public override void visit(class_members cm)
        {
            foreach (var decl in cm.members)
            {
                if (decl is procedure_header || decl is procedure_definition)
                    decl.visit(this);
            }
            base.visit(cm);
        }*/

        type_declarations GenClassesForYield(procedure_definition pd, IEnumerable<var_def_statement> fields,
            IDictionary<string, string> localsMap,
            IDictionary<string, string> formalParamsMap)
        {
            var fh = (pd.proc_header as function_header);
            if (fh == null)
                throw new SyntaxError("Only functions can contain yields", "", pd.proc_header.source_context, pd.proc_header);
            var seqt = fh.return_type as sequence_type;
            if (seqt == null)
                throw new SyntaxError("Functions with yields must return sequences", "", fh.return_type.source_context, fh.return_type);

            // Теперь на месте функции генерируем класс

            // Захваченные переменные
            var cm = class_members.Public;
            var capturedFields = fields.Select(vds =>
                                    {
                                        ident_list ids = new ident_list(vds.vars.idents.Select(id => new ident(localsMap[id.name])).ToArray());
                                        return new var_def_statement(ids, vds.vars_type, vds.inital_value);
                                    });

            foreach (var m in capturedFields)
                cm.Add(m);

            // Параметры функции
            List<ident> lid = new List<ident>();
            var pars = fh.parameters;
            if (pars != null)
                foreach (var ps in pars.params_list)
                {
                    if (ps.param_kind != parametr_kind.none)
                        throw new SyntaxError("Parameters of functions with yields must not have 'var', 'const' or 'params' modifier", "", pars.source_context, pars);
                    if (ps.inital_value != null)
                        throw new SyntaxError("Parameters of functions with yields must not have initial values", "", pars.source_context, pars);
                    //var_def_statement vds = new var_def_statement(ps.idents, ps.vars_type);
                    ident_list ids = new ident_list(ps.idents.list.Select(id => new ident(formalParamsMap[id.name])).ToArray());
                    var_def_statement vds = new var_def_statement(ids, ps.vars_type);
                    cm.Add(vds); // все параметры функции делаем полями класса
                    //lid.AddRange(vds.vars.idents);
                    lid.AddRange(ps.idents.list);
                }

            var stels = seqt.elements_type;

            // frninja 08/18/15 - Для захвата self
            if ((object)GetClassName(pd) != null)
                cm.Add(new var_def_statement(Consts.Self, GetClassName(pd).name));

            // Системные поля и методы для реализации интерфейса IEnumerable
            cm.Add(new var_def_statement(Consts.State, "integer"),
                new var_def_statement(Consts.Current, stels),
                procedure_definition.EmptyDefaultConstructor,
                new procedure_definition("Reset"),
                new procedure_definition("MoveNext", "boolean", pd.proc_body),
                new procedure_definition("get_Current", "object", new assign("Result", Consts.Current)),
                new procedure_definition("GetEnumerator", "System.Collections.IEnumerator", new assign("Result", "Self"))
                );

            
            

            var className = newClassName();
            var classNameHelper = className + "Helper";

            var interfaces = new named_type_reference_list("System.Collections.IEnumerator", "System.Collections.IEnumerable");
            var td = new type_declaration(classNameHelper, SyntaxTreeBuilder.BuildClassDefinition(interfaces, cm));

            // Изменение тела процедуры
            

            var stl = new statement_list(new var_statement("res", new new_expr(className)));
            //stl.AddMany(lid.Select(id => new assign(new dot_node("res", id), id)));
            stl.AddMany(lid.Select(id => new assign(new dot_node("res", new ident(formalParamsMap[id.name])), id)));

            // frninja 08/12/15 - захват self
            if ((object)GetClassName(pd) != null)
                stl.Add(new assign(new dot_node("res", Consts.Self), new ident("self")));

            stl.Add(new assign("Result", "res"));

            // New body
            pd.proc_body = new block(stl);

            if ((object)GetClassName(pd) != null)
            {
                // frninja 10/12/15 - заменить на function_header и перенести описание тела в declarations
                Replace(pd, fh);
                var decls = UpperTo<declarations>();
                if ((object)decls != null)
                {
                    function_header nfh = new function_header();
                    nfh.name = new method_name(fh.name.meth_name.name);
                    // Set name
                    nfh.name.class_name = GetClassName(pd);
                    nfh.parameters = fh.parameters;
                    nfh.proc_attributes = fh.proc_attributes;

                    
                    procedure_definition npd = new procedure_definition(nfh, new block(stl));
                    
                    // Update header
                    //pd.proc_header.name.class_name = GetClassName(pd);
                    // Add to decls
                    decls.Add(npd);
                }
            }

            // Второй класс

            var tpl = new template_param_list(stels);

            var IEnumeratorT = new template_type_reference("System.Collections.Generic.IEnumerator", tpl);

            var cm1 = class_members.Public.Add(
                procedure_definition.EmptyDefaultConstructor,
                new procedure_definition(new function_header("get_Current", stels), new assign("Result", Consts.Current)),
                new procedure_definition(new function_header("GetEnumerator", IEnumeratorT), new assign("Result", "Self")),
                new procedure_definition("Dispose")
            );

            var interfaces1 = new named_type_reference_list(classNameHelper);
            var IEnumerableT = new template_type_reference("System.Collections.Generic.IEnumerable", tpl);

            interfaces1.Add(IEnumerableT).Add(IEnumeratorT);

            var td1 = new type_declaration(className, SyntaxTreeBuilder.BuildClassDefinition(interfaces1, cm1));

            var cct = new type_declarations(td);
            cct.Add(td1);

            return cct;
        }

        private void CollectFormalParams(procedure_definition pd, ISet<var_def_statement> collectedFormalParams)
        {
            if ((object)pd.proc_header.parameters != null)
                collectedFormalParams.UnionWith(pd.proc_header.parameters.params_list.Select(tp => new var_def_statement(tp.idents, tp.vars_type)));
        }

        private void CollectFormalParamsNames(procedure_definition pd, ISet<string> collectedFormalParamsNames)
        {
            if ((object)pd.proc_header.parameters != null)
                collectedFormalParamsNames.UnionWith(pd.proc_header.parameters.params_list.SelectMany(tp => tp.idents.idents).Select(id => id.name));
        }

        private ident GetClassName(procedure_definition pd)
        {
            if ((object)pd.proc_header.name.class_name != null)
            {
                // Объявление вне класса его метода
                return pd.proc_header.name.class_name;
            }
            else
            {
                // Объявление функции в классе?
                var classDef = UpperNode(3) as class_definition;
                if ((object)(UpperNode(3) as class_definition) != null)
                {
                    var td = UpperNode(4) as type_declaration;
                    if ((object)td != null)
                    {
                        return td.type_name;
                    }
                }
            }

            return null;
        }

        private bool IsClassMethod(procedure_definition pd)
        {
            return (object)GetClassName(pd) != null;
        }

        private void CollectClassFieldsNames(procedure_definition pd, ISet<string> collectedFields)
        {
            ident className = GetClassName(pd);

            if ((object)className != null)
            {
                CollectClassFieldsVisitor fieldsVis = new CollectClassFieldsVisitor(className);
                var cu = UpperTo<compilation_unit>();
                if ((object)cu != null)
                {
                    cu.visit(fieldsVis);
                    // Collect
                    collectedFields.UnionWith(fieldsVis.CollectedFields.Select(id => id.name));
                }
            }
        }

        private void CollectClassMethodsNames(procedure_definition pd, ISet<string> collectedMethods)
        {
            ident className = GetClassName(pd);

            if ((object)className != null)
            {
                CollectClassMethodsVisitor methodsVis = new CollectClassMethodsVisitor(className);
                var cu = UpperTo<compilation_unit>();
                if ((object)cu != null)
                {
                    cu.visit(methodsVis);
                    // Collect
                    collectedMethods.UnionWith(methodsVis.CollectedMethods.Select(id => id.name));
                }
            }
        }

        private void CollectClassPropertiesNames(procedure_definition pd, ISet<string> collectedProperties)
        {
            ident className = GetClassName(pd);

            if ((object)className != null)
            {
                CollectClassPropertiesVisitor propertiesVis = new CollectClassPropertiesVisitor(className);
                var cu = UpperTo<compilation_unit>();
                if ((object)cu != null)
                {
                    cu.visit(propertiesVis);
                    // Collect
                    collectedProperties.UnionWith(propertiesVis.CollectedProperties.Select(id => id.name));
                }
            }
        }

        private void CollectUnitGlobalsNames(procedure_definition pd, ISet<string> collectedUnitGlobalsName)
        {
            var cu = UpperTo<compilation_unit>();
            if ((object)cu != null)
            {
                var ugVis = new CollectUnitGlobalsVisitor();
                cu.visit(ugVis);
                // Collect
                collectedUnitGlobalsName.UnionWith(ugVis.CollectedGlobals.Select(id => id.name));
            }
        }

        private void CreateCapturedLocalsNamesMap(ISet<string> localsNames, IDictionary<string, string> capturedLocalsNamesMap)
        {
            foreach (var localName in localsNames)
            {
                capturedLocalsNamesMap.Add(localName, CapturedNamesHelper.MakeCapturedLocalName(localName));
            }
        }

        private void CreateCapturedFormalParamsNamesMap(ISet<string> formalParamsNames, IDictionary<string, string> captueedFormalParamsNamesMap)
        {
            foreach (var formalParamName in formalParamsNames)
            {
                captueedFormalParamsNamesMap.Add(formalParamName, CapturedNamesHelper.MakeCapturedFormalParameterName(formalParamName));
            }
        }

        public override void visit(procedure_definition pd)
        {
            // frninja
            // DEBUG for test 
            // SORRY

            // Classification
            ISet<string> CollectedLocalsNames = new HashSet<string>();
            ISet<string> CollectedFormalParamsNames = new HashSet<string>();
            ISet<string> CollectedClassFieldsNames = new HashSet<string>();
            ISet<string> CollectedClassMethodsNames = new HashSet<string>();
            ISet<string> CollectedClassPropertiesNames = new HashSet<string>();
            ISet<string> CollectedUnitGlobalsNames = new HashSet<string>();

            ISet<var_def_statement> CollectedLocals = new HashSet<var_def_statement>();
            ISet<var_def_statement> CollectedFormalParams = new HashSet<var_def_statement>();

            // Map from ident idName -> captured ident idName
            IDictionary<string, string> CapturedLocalsNamesMap = new Dictionary<string, string>();
            IDictionary<string, string> CapturedFormalParamsNamesMap = new Dictionary<string, string>();


            hasYields = false;
            if (pd.proc_header is function_header)
                mids = new FindMainIdentsVisitor();

            base.visit(pd);

            if (!hasYields) // т.е. мы разобрали функцию и уже выходим. Это значит, что пока yield будет обрабатываться только в функциях. Так это и надо.
                return;

            LoweringVisitor.Accept(pd);

            // frninja 16/11/15: перенес ниже чтобы работал захват для lowered for

            var dld = new DeleteAllLocalDefs(); // mids.vars - все захваченные переменные
            pd.visit(dld); // Удалить в локальных и блочных описаниях этой процедуры все переменные и вынести их в отдельный список var_def_statement

            // frninja 08/12/15
            bool isClassMethod = IsClassMethod(pd);

            // Collect locals
            CollectedLocals.UnionWith(dld.LocalDeletedDefs);
            CollectedLocalsNames.UnionWith(dld.LocalDeletedDefs.SelectMany(vds => vds.vars.idents).Select(id => id.name));
            // Collect formal params
            CollectFormalParams(pd, CollectedFormalParams);
            CollectFormalParamsNames(pd, CollectedFormalParamsNames);
            // Collect class fields
            CollectClassFieldsNames(pd, CollectedClassFieldsNames);
            // Collect class methods
            CollectClassMethodsNames(pd, CollectedClassMethodsNames);
            // Collect class properties
            CollectClassPropertiesNames(pd, CollectedClassPropertiesNames);
            // Collect unit globals
            CollectUnitGlobalsNames(pd, CollectedUnitGlobalsNames);
            

            // Create maps :: idName -> captureName
            CreateCapturedLocalsNamesMap(CollectedLocalsNames, CapturedLocalsNamesMap);
            CreateCapturedFormalParamsNamesMap(CollectedFormalParamsNames, CapturedFormalParamsNamesMap);

            // AHAHA test!
            ReplaceCapturedVariablesVisitor rcapVis = new ReplaceCapturedVariablesVisitor(
                CollectedLocalsNames,
                CollectedFormalParamsNames,
                CollectedClassFieldsNames,
                CollectedClassMethodsNames,
                CollectedClassPropertiesNames,
                CollectedUnitGlobalsNames,
                CapturedLocalsNamesMap,
                CapturedFormalParamsNamesMap,
                isClassMethod
                );
            // Replace
            (pd.proc_body as block).program_code.visit(rcapVis);


            mids.vars.Except(dld.LocalDeletedDefsNames); // параметры остались. Их тоже надо исключать - они и так будут обработаны
            // В результате работы в mids.vars что-то осталось. Это не локальные переменные и с ними непонятно что делать

            // Обработать параметры! 
            // Как? Ищем в mids formal_parametrs, но надо выделить именно обращение к параметрам - не полям класса, не глобальным переменным

            var cfa = new ConstructFiniteAutomata((pd.proc_body as block).program_code);
            cfa.Transform();
            (pd.proc_body as block).program_code = cfa.res;

            // Конструируем определение класса
            var cct = GenClassesForYield(pd, dld.LocalDeletedDefs, CapturedLocalsNamesMap, CapturedFormalParamsNamesMap); // все удаленные описания переменных делаем описанием класса

            //UpperNodeAs<declarations>().InsertBefore(pd, cct);
            if (isClassMethod)
            {
                var cd = UpperTo<class_definition>();
                if ((object)cd != null)
                {
                    var td = UpperTo<type_declarations>();
                    // Insert class predefenition!
                    var iteratorClassPredef = new type_declaration(GetClassName(pd), new class_definition(null));
                    td.types_decl.Insert(0, iteratorClassPredef);

                    foreach (var helperName in cct.types_decl.Select(ttd => ttd.type_name))
                    {
                        var helperPredef = new type_declaration(helperName, new class_definition());
                        td.types_decl.Insert(0, helperPredef);
                    }

                    foreach (var helper in cct.types_decl)
                    {
                        td.types_decl.Add(helper);
                    }

                    //UpperTo<declarations>().InsertAfter(td, cct);
                }
            }
            else 
            {
                UpperTo<declarations>().InsertBefore(pd, cct);
            }

            mids = null; // вдруг мы выйдем из процедуры, не зайдем в другую, а там - оператор! Такого конечно не может быть
        }

        public override void visit(yield_node yn)
        {
            hasYields = true;
            if (mids != null) // если мы - внутри процедуры
                yn.visit(mids);
            else throw new SyntaxError("Yield must be in functions only", "", yn.source_context, yn);
            // mids.vars - надо установить, какие из них - локальные, какие - из этого класса, какие - являются параметрами функции, а какие - глобальные (все остальные)
            // те, которые являются параметрами, надо скопировать в локальные переменные и переименовать использование везде по ходу данной функции 
            // самое сложное - переменные-поля этого класса - они требуют в создаваемом классе, реализующем итератор, хранить Self текущего класса и добавлять это Self везде по ходу алгоритма
            // вначале будем считать, что переменные-поля этого класса и переменные-параметры не захватываются yield
            //base.visit(yn);


        }
    }

    class ConstructFiniteAutomata
    {
        public statement_list res = new statement_list();
        statement_list stl;
        int curState = 0;

        statement_list curStatList;
        statement_list StatListAfterCase = new statement_list();

        case_node cas; // формируемый case

        private Dictionary<labeled_statement, List<int>> _dispatches = new Dictionary<labeled_statement, List<int>>();

        public ConstructFiniteAutomata(statement_list stl)
        {
            this.stl = stl;
        }

        private void AddState(out int stateNumber, out ident resumeLabel)
        {
            stateNumber = curState++;
            resumeLabel = null;
        }

        public void Process(statement st)
        {
            if (!(st is yield_node || st is labeled_statement))
            {
                curStatList.Add(st);
            }
            if (st is yield_node)
            {
                var yn = st as yield_node;
                curState += 1;
                curStatList.AddMany(
                    new assign(Consts.Current, yn.ex),
                    new assign(Consts.State, curState),
                    new assign("Result", true),
                    new procedure_call("exit")
                );

                curStatList = new statement_list();
                case_variant cv = new case_variant(new expression_list(new int32_const(curState)), curStatList);
                cas.conditions.variants.Add(cv);
            }
            if (st is labeled_statement)
            {
                var ls = st as labeled_statement;
                curStatList = StatListAfterCase;
                curStatList.Add(new labeled_statement(ls.label_name));
                Process(ls.to_statement);
            }
        }

        public void Transform()
        {
            cas = new case_node(new ident(Consts.State));

            curStatList = new statement_list();
            case_variant cv = new case_variant(new expression_list(new int32_const(curState)), curStatList);
            cas.conditions.variants.Add(cv);

            foreach (var st in stl.subnodes)
                Process(st);

            stl.subnodes = BaseChangeVisitor.SeqStatements(cas, StatListAfterCase).ToList();
            //statement_list res = new statement_list(cas);
            res = stl;
        }
    }

}
