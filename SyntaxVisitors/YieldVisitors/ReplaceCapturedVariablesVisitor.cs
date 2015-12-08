using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PascalABCCompiler;
using PascalABCCompiler.SyntaxTree;

namespace SyntaxVisitors
{
    public class ReplaceCapturedVariablesVisitor : BaseChangeVisitor
    {
        private ISet<string> CollectedLocals = new HashSet<string>();
        private ISet<string> CollectedFormalParams = new HashSet<string>();
        private ISet<string> CollectedClassFields = new HashSet<string>();
        private ISet<string> CollectedUnitGlobals = new HashSet<string>();

        // Maps :: idName -> capturedName
        private IDictionary<string, string> CapturedLocalsMap = new Dictionary<string, string>();
        private IDictionary<string, string> CapturedFormalParamsMap = new Dictionary<string, string>();

        private bool IsInClassMethod = true;


        public ReplaceCapturedVariablesVisitor(IEnumerable<string> locals,
            IEnumerable<string> formalParams,
            IEnumerable<string> classFields,
            IEnumerable<string> unitGlobals,
            IDictionary<string, string> localsMap,
            IDictionary<string, string> formalParamsMap,
            bool isInClassMethod)
        {
            CollectedLocals = new HashSet<string>(locals);
            CollectedFormalParams = new HashSet<string>(formalParams);
            CollectedClassFields = new HashSet<string>(classFields);
            CollectedUnitGlobals = new HashSet<string>(unitGlobals);

            CapturedLocalsMap = new Dictionary<string, string>(localsMap);
            CapturedFormalParamsMap = new Dictionary<string, string>(formalParamsMap);

            IsInClassMethod = isInClassMethod;
        }

        public override void visit(ident id)
        {
            // Check dot node
            var upper = UpperNode(1);
            if (upper is dot_node)
                return;

            var idName = id.name;
            var idSourceContext = id.source_context;

            // Detect where is id from
            if (CollectedLocals.Contains(idName))
            {
                Replace(id, new ident(CapturedLocalsMap[idName], idSourceContext));
            }
            else if (CollectedFormalParams.Contains(idName))
            {
                Replace(id, new ident(CapturedFormalParamsMap[idName], idSourceContext));
            }
            else if (IsInClassMethod && CollectedClassFields.Contains(idName))
            {
                // Check in globals
                if (CollectedUnitGlobals.Contains(idName))
                {
                    // Not bad!
                    // Name in class fields && in globals -> capture as class field
                    var capturedSelf = new dot_node(new ident("self"), new ident(Consts.Self));
                    var capturedId = new dot_node(capturedSelf, id);
                    Replace(id, capturedId);
                }
                else
                {
                    // Good! 
                    // No name in globals && name in class fields -> capture as class field
                    var capturedSelf = new dot_node(new ident("self"), new ident(Consts.Self));
                    var capturedId = new dot_node(capturedSelf, id);
                    Replace(id, capturedId);
                }
            }
            else if (CollectedUnitGlobals.Contains(idName))
            {
                if (IsInClassMethod && CollectedClassFields.Contains(idName))
                {
                    // Not bad!
                    // Name in class fields && in globals -> capture as class field
                    var capturedSelf = new dot_node(new ident("self"), new ident(Consts.Self));
                    var capturedId = new dot_node(capturedSelf, id);
                    Replace(id, capturedId);
                }
                else if (IsInClassMethod)
                {
                    // BAAAAD
                    // At syntax we don't know if the name is class field or not coz of e.g. base .NET classes
                    // HERE WE SHOULD REPLACE TO yield_unknown_reference -> so decision is passed to semantic 
                }
                else
                {
                    // Not in class method -> just do nothing, that is global! =)
                }
            }
            else
            {
                // Not in globals
                if (IsInClassMethod && ! CollectedClassFields.Contains(idName))
                {
                    // BAAAAD
                    // At syntax we don't know if the name is class field or not coz of e.g. base .NET classes
                    // HERE WE SHOULD REPLACE TO yield_unknown_reference -> so decision is passed to semantic 
                }
            }
        }

        public override void visit(dot_node dn)
        {
            // LEFT self -> captured self (self.captured_self)
            var id = dn.left as ident;
            if ((object)id != null && id.name == "self")
            {
                // Some magic for blocking back-traverse from BaseChangeVisitor redoin' work
                var rid = dn.right as ident;
                if ((object)rid != null && rid.name != Consts.Self)
                {
                    // Change right?
                    var capturedRight = new dot_node(new ident(Consts.Self), dn.right);
                    var newDotNode = new dot_node(dn.left, capturedRight);
                    Replace(dn, newDotNode);
                }
                

                // Some magic for blocking back-traverse from BaseChangeVisitor redoin' work
                //var rid = dn.right as ident;
                //if ((object)rid != null && rid.name != Consts.Self)
               // {
                //    var capturedSelf = new dot_node(new ident("self"), new ident(Consts.Self));
                //    Replace(dn.left, capturedSelf);
               // }
            }
            else
            {
                ProcessNode(dn.left);
            }

            if (dn.right.GetType() != typeof(ident))
                ProcessNode(dn.right);
        }
    }
}
