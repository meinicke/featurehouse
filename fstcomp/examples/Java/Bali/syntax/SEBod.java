// Automatically generated code.  Edit at your own risk!
// Generated by bali2jak v2002.09.03.



public class SEBod extends SwitchEntryBody {

    final public static int ARG_LENGTH = 2 ;
    final public static int TOK_LENGTH = 1 /* Kludge! */ ;

    public AST_Stmt getAST_Stmt () {
        
        AstNode node = arg[1].arg [0] ;
        return (node != null) ? (AST_Stmt) node : null ;
    }

    public SwitchLabel getSwitchLabel () {
        
        return (SwitchLabel) arg [0] ;
    }

    public boolean[] printorder () {
        
        return new boolean[] {false, false} ;
    }

    public SEBod setParms (SwitchLabel arg0, AstOptNode arg1) {
        
        arg = new AstNode [ARG_LENGTH] ;
        tok = new AstTokenInterface [TOK_LENGTH] ;
        
        arg [0] = arg0 ;            /* SwitchLabel */
        arg [1] = arg1 ;            /* [ AST_Stmt ] */
        
        InitChildren () ;
        return (SEBod) this ;
    }

}