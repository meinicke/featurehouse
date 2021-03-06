/**
 * 
 */
package modification.content.Parseables.CSharp;

import java.io.FileNotFoundException;

import modification.content.InvalidFSTTraversalException;
import modification.content.Parseables.ParseableCodeSnippet;
import cide.gparser.ParseException;
import de.ovgu.cide.fstgen.ast.FSTNode;
import de.ovgu.cide.fstgen.parsers.generated_csharp.CSharpParser;

/**
 * @author Boxleitner Stefan
 * 
 */
public class CSharpMethod extends ParseableCodeSnippet {

    public CSharpMethod(String content) {
	super(content);	
    }

    /*
     * (non-Javadoc)
     * 
     * @see modification.content.Content#getFST()
     */
    @Override
    public FSTNode getFST() throws FileNotFoundException, ParseException,
	    modification.traversalLanguageParser.ParseException,
	    InvalidFSTTraversalException {
	CSharpParser p = new CSharpParser(getCharStream());
	p.class_member_declaration(false);
	return p.getRoot();
    }

}
