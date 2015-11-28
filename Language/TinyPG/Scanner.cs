// Generated by TinyPG v1.3 available at www.codeproject.com

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Drawing;

using TinyPG;

namespace TinyPG
{
    #region Scanner

    public partial class Scanner
    {
        public string Input;
        public int StartPos = 0;
        public int EndPos = 0;
        public string CurrentFile;
        public int CurrentLine;
        public int CurrentColumn;
        public int CurrentPosition;
        public List<Token> Skipped = new List<Token>(); // tokens that were skipped
        public List<Token> SkippedGlobal = new List<Token>(); 
        public List<Token> RecognizedTokens 
        {
            get
            {
                return _recognizedTokens;
            }
        }
        
        private List<Token> _recognizedTokens = new List<Token>();

        private Token LookAheadToken = null;
        private readonly TokenType FileAndLine = default(TokenType);

        public static Dictionary<TokenType, Regex> Patterns;
        private static List<TokenType> Tokens;
        private static List<TokenType> SkipList; // tokens to be skipped

        static Scanner()
        {
            Regex regex;
            Patterns = new Dictionary<TokenType, Regex>();
            Tokens = new List<TokenType>();

            SkipList = new List<TokenType>();
            SkipList.Add(TokenType.WHITESPACE);
            SkipList.Add(TokenType.COMMENT);

            regex = new Regex(@"global", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.GLOBAL, regex);
            Tokens.Add(TokenType.GLOBAL);

            regex = new Regex(@"end", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.END, regex);
            Tokens.Add(TokenType.END);

            regex = new Regex(@"return", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.RETURN, regex);
            Tokens.Add(TokenType.RETURN);

            regex = new Regex(@"=>", RegexOptions.Compiled);
            Patterns.Add(TokenType.ARROW, regex);
            Tokens.Add(TokenType.ARROW);

            regex = new Regex(@"if", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.IF, regex);
            Tokens.Add(TokenType.IF);

            regex = new Regex(@"else", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.ELSE, regex);
            Tokens.Add(TokenType.ELSE);

            regex = new Regex(@"for", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.FOR, regex);
            Tokens.Add(TokenType.FOR);

            regex = new Regex(@"to", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.TO, regex);
            Tokens.Add(TokenType.TO);

            regex = new Regex(@"incby", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.INCBY, regex);
            Tokens.Add(TokenType.INCBY);

            regex = new Regex(@"while", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.WHILE, regex);
            Tokens.Add(TokenType.WHILE);

            regex = new Regex(@"do", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.DO, regex);
            Tokens.Add(TokenType.DO);

            regex = new Regex(@"or", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.OR, regex);
            Tokens.Add(TokenType.OR);

            regex = new Regex(@"and", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.AND, regex);
            Tokens.Add(TokenType.AND);

            regex = new Regex(@"not", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.NOT, regex);
            Tokens.Add(TokenType.NOT);

            regex = new Regex(@"write", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.OPER, regex);
            Tokens.Add(TokenType.OPER);

            regex = new Regex(@"\+|-", RegexOptions.Compiled);
            Patterns.Add(TokenType.PLUSMINUS, regex);
            Tokens.Add(TokenType.PLUSMINUS);

            regex = new Regex(@"\*|/|\%\%|\%/", RegexOptions.Compiled);
            Patterns.Add(TokenType.MULTDIV, regex);
            Tokens.Add(TokenType.MULTDIV);

            regex = new Regex(@"=|\!=|\<\=|\<|\>=|\>", RegexOptions.Compiled);
            Patterns.Add(TokenType.COMP, regex);
            Tokens.Add(TokenType.COMP);

            regex = new Regex(@"\^", RegexOptions.Compiled);
            Patterns.Add(TokenType.POW, regex);
            Tokens.Add(TokenType.POW);

            regex = new Regex(@"\+\+|--|\+|-", RegexOptions.Compiled);
            Patterns.Add(TokenType.UNARYOP, regex);
            Tokens.Add(TokenType.UNARYOP);

            regex = new Regex(@":", RegexOptions.Compiled);
            Patterns.Add(TokenType.COLON, regex);
            Tokens.Add(TokenType.COLON);

            regex = new Regex(@"\?", RegexOptions.Compiled);
            Patterns.Add(TokenType.QUESTION, regex);
            Tokens.Add(TokenType.QUESTION);

            regex = new Regex(@",", RegexOptions.Compiled);
            Patterns.Add(TokenType.COMMA, regex);
            Tokens.Add(TokenType.COMMA);

            regex = new Regex(@"\=", RegexOptions.Compiled);
            Patterns.Add(TokenType.ASSIGN, regex);
            Tokens.Add(TokenType.ASSIGN);

            regex = new Regex(@"\(", RegexOptions.Compiled);
            Patterns.Add(TokenType.BROPEN, regex);
            Tokens.Add(TokenType.BROPEN);

            regex = new Regex(@"\)", RegexOptions.Compiled);
            Patterns.Add(TokenType.BRCLOSE, regex);
            Tokens.Add(TokenType.BRCLOSE);

            regex = new Regex(@"\[", RegexOptions.Compiled);
            Patterns.Add(TokenType.SQOPEN, regex);
            Tokens.Add(TokenType.SQOPEN);

            regex = new Regex(@"\]", RegexOptions.Compiled);
            Patterns.Add(TokenType.SQCLOSE, regex);
            Tokens.Add(TokenType.SQCLOSE);

            regex = new Regex(@"@?\""(\""\""|[^\""])*\""", RegexOptions.Compiled);
            Patterns.Add(TokenType.STRING, regex);
            Tokens.Add(TokenType.STRING);

            regex = new Regex(@"[0-9]+", RegexOptions.Compiled);
            Patterns.Add(TokenType.INTEGER, regex);
            Tokens.Add(TokenType.INTEGER);

            regex = new Regex(@"[0-9]*\.[0-9]+", RegexOptions.Compiled);
            Patterns.Add(TokenType.DOUBLE, regex);
            Tokens.Add(TokenType.DOUBLE);

            regex = new Regex(@"true|false", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.BOOL, regex);
            Tokens.Add(TokenType.BOOL);

            regex = new Regex(@"readnum|readstr", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.READFUNC, regex);
            Tokens.Add(TokenType.READFUNC);

            regex = new Regex(@"[a-zA-Z_][a-zA-Z0-9_]*(?<!(^)(end|else|do|while|for|true|false|return|to|incby|global|or|and|not))(?!\w)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Patterns.Add(TokenType.IDENTIFIER, regex);
            Tokens.Add(TokenType.IDENTIFIER);

            regex = new Regex(@"\s+", RegexOptions.Compiled);
            Patterns.Add(TokenType.NEWLINE, regex);
            Tokens.Add(TokenType.NEWLINE);

            regex = new Regex(@"^$", RegexOptions.Compiled);
            Patterns.Add(TokenType.EOF, regex);
            Tokens.Add(TokenType.EOF);

            regex = new Regex(@"\s+", RegexOptions.Compiled);
            Patterns.Add(TokenType.WHITESPACE, regex);
            Tokens.Add(TokenType.WHITESPACE);

            regex = new Regex(@"//[^\n]*\n?", RegexOptions.Compiled);
            Patterns.Add(TokenType.COMMENT, regex);
            Tokens.Add(TokenType.COMMENT);


        }

        public void Init(string input)
        {
            Init(input, "");
        }

        public void Init(string input, string fileName)
        {
            this.Input = input;
            StartPos = 0;
            EndPos = 0;
            CurrentFile = fileName;
            CurrentLine = 1;
            CurrentColumn = 1;
            CurrentPosition = 0;
            LookAheadToken = null;
        }

        public Token GetToken(TokenType type)
        {
            Token t = new Token(this.StartPos, this.EndPos);
            t.Type = type;
            return t;
        }

         /// <summary>
        /// executes a lookahead of the next token
        /// and will advance the scan on the input string
        /// </summary>
        /// <returns></returns>
        public Token Scan(params TokenType[] expectedtokens)
        {
            Token tok = LookAhead(expectedtokens); // temporarely retrieve the lookahead
            LookAheadToken = null; // reset lookahead token, so scanning will continue
            StartPos = tok.EndPos;
            EndPos = tok.EndPos; // set the tokenizer to the new scan position
            CurrentLine = tok.Line + (tok.Text.Length - tok.Text.Replace("\n", "").Length);
            CurrentFile = tok.File;

            _recognizedTokens.Add(tok);

            return tok;
        }

        /// <summary>
        /// returns token with longest best match
        /// </summary>
        /// <returns></returns>
        public Token LookAhead(params TokenType[] expectedtokens)
        {
            int i;
            int startpos = StartPos;
            int endpos = EndPos;
            int currentline = CurrentLine;
            string currentFile = CurrentFile;
            Token tok = null;
            List<TokenType> scantokens;


            // this prevents double scanning and matching
            // increased performance
            if (LookAheadToken != null 
                && LookAheadToken.Type != TokenType._UNDETERMINED_ 
                && LookAheadToken.Type != TokenType._NONE_) return LookAheadToken;

            // if no scantokens specified, then scan for all of them (= backward compatible)
            if (expectedtokens.Length == 0)
                scantokens = Tokens;
            else
            {
                scantokens = new List<TokenType>(expectedtokens);
                scantokens.AddRange(SkipList);
            }

            do
            {

                int len = -1;
                TokenType index = (TokenType)int.MaxValue;
                string input = Input.Substring(startpos);

                tok = new Token(startpos, endpos);

                for (i = 0; i < scantokens.Count; i++)
                {
                    Regex r = Patterns[scantokens[i]];
                    Match m = r.Match(input);
                    if (m.Success && m.Index == 0 && ((m.Length > len) || (scantokens[i] < index && m.Length == len )))
                    {
                        len = m.Length;
                        index = scantokens[i];  
                    }
                }

                if (index >= 0 && len >= 0)
                {
                    tok.EndPos = startpos + len;
                    tok.Text = Input.Substring(tok.StartPos, len);
                    tok.Type = index;
                }
                else if (tok.StartPos == tok.EndPos)
                {
                    if (tok.StartPos < Input.Length)
                        tok.Text = Input.Substring(tok.StartPos, 1);
                    else
                        tok.Text = "EOF";
                }

                // Update the line and column count for error reporting.
                tok.File = currentFile;
                tok.Line = currentline;
                if (tok.StartPos < Input.Length)
                    tok.Column = tok.StartPos - Input.LastIndexOf('\n', tok.StartPos);

                if (SkipList.Contains(tok.Type))
                {
                    startpos = tok.EndPos;
                    endpos = tok.EndPos;
                    currentline = tok.Line + (tok.Text.Length - tok.Text.Replace("\n", "").Length);
                    currentFile = tok.File;
                    Skipped.Add(tok);
                    SkippedGlobal.Add(tok);
                }
                else
                {
                    // only assign to non-skipped tokens
                    tok.Skipped = Skipped; // assign prior skips to this token
                    Skipped = new List<Token>(); //reset skips
                }

                // Check to see if the parsed token wants to 
                // alter the file and line number.
                if (tok.Type == FileAndLine)
                {
                    var match = Patterns[tok.Type].Match(tok.Text);
                    var fileMatch = match.Groups["File"];
                    if (fileMatch.Success)
                        currentFile = fileMatch.Value.Replace("\\\\", "\\");
                    var lineMatch = match.Groups["Line"];
                    if (lineMatch.Success)
                        currentline = int.Parse(lineMatch.Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
            }
            while (SkipList.Contains(tok.Type));

            LookAheadToken = tok;
            return tok;
        }

        public static Dictionary<TokenType, string> Styles = new Dictionary<TokenType, string>
        {
        
{TokenType.GLOBAL, "boldBlue"},
{TokenType.END, "boldBlue"},
{TokenType.RETURN, "boldBlue"},
{TokenType.ARROW, "boldBlue"},
{TokenType.IF, "boldBlue"},
{TokenType.ELSE, "boldBlue"},
{TokenType.FOR, "boldBlue"},
{TokenType.TO, "boldBlue"},
{TokenType.INCBY, "boldBlue"},
{TokenType.WHILE, "boldBlue"},
{TokenType.DO, "boldBlue"},
{TokenType.OR, "bold"},
{TokenType.AND, "bold"},
{TokenType.NOT, "bold"},
{TokenType.OPER, "boldBlue"},
{TokenType.PLUSMINUS, "bold"},
{TokenType.MULTDIV, "bold"},
{TokenType.COMP, "bold"},
{TokenType.POW, "bold"},
{TokenType.UNARYOP, "bold"},
{TokenType.COLON, "bold"},
{TokenType.QUESTION, "bold"},
{TokenType.COMMA, "bold"},
{TokenType.ASSIGN, "bold"},
{TokenType.BROPEN, "bold"},
{TokenType.BRCLOSE, "bold"},
{TokenType.SQOPEN, "bold"},
{TokenType.SQCLOSE, "bold"},
{TokenType.STRING, "string"},
{TokenType.READFUNC, "boldBlue"},
{TokenType.IDENTIFIER, "fade"},
{TokenType.COMMENT, "comment"},
        };
    }

    #endregion

    #region Token

    public enum TokenType
    {

            //Non terminal tokens:
            _NONE_  = 0,
            _UNDETERMINED_= 1,

            //Non terminal tokens:
            Start   = 2,
            Program = 3,
            Member  = 4,
            Globalvar= 5,
            Function= 6,
            Parameters= 7,
            Statements= 8,
            Statement= 9,
            IfStm   = 10,
            WhileStm= 11,
            DoStm   = 12,
            ForStm  = 13,
            ReturnStm= 14,
            OperStm = 15,
            CallOrAssign= 16,
            Assign  = 17,
            Variable= 18,
            Array   = 19,
            Call    = 20,
            Arguments= 21,
            Literal = 22,
            Expr    = 23,
            OrExpr  = 24,
            AndExpr = 25,
            NotExpr = 26,
            CompExpr= 27,
            AddExpr = 28,
            MultExpr= 29,
            PowExpr = 30,
            UnaryExpr= 31,
            Atom    = 32,

            //Terminal tokens:
            GLOBAL  = 33,
            END     = 34,
            RETURN  = 35,
            ARROW   = 36,
            IF      = 37,
            ELSE    = 38,
            FOR     = 39,
            TO      = 40,
            INCBY   = 41,
            WHILE   = 42,
            DO      = 43,
            OR      = 44,
            AND     = 45,
            NOT     = 46,
            OPER    = 47,
            PLUSMINUS= 48,
            MULTDIV = 49,
            COMP    = 50,
            POW     = 51,
            UNARYOP = 52,
            COLON   = 53,
            QUESTION= 54,
            COMMA   = 55,
            ASSIGN  = 56,
            BROPEN  = 57,
            BRCLOSE = 58,
            SQOPEN  = 59,
            SQCLOSE = 60,
            STRING  = 61,
            INTEGER = 62,
            DOUBLE  = 63,
            BOOL    = 64,
            READFUNC= 65,
            IDENTIFIER= 66,
            NEWLINE = 67,
            EOF     = 68,
            WHITESPACE= 69,
            COMMENT = 70
    }

    public class Token
    {
        private string file;
        private int line;
        private int column;
        private int startpos;
        private int endpos;
        private string text;
        private object value;

        // contains all prior skipped symbols
        private List<Token> skipped;

        public string File { 
            get { return file; } 
            set { file = value; }
        }

        public int Line
        { 
            get { return line != 10000?line:0; } 
            set { line = value; }
        }
       
        public int Column
        {
            get { return column != 10000?column:0; } 
            set { column = value; }
        }

       
        public int StartPos
        { 
            get { return startpos;} 
            set { startpos = value; }
        }

        public int Length { 
            get { return endpos - startpos;} 
        }

        public int EndPos { 
            get { return endpos;} 
            set { endpos = value; }
        }

        public string Text { 
            get { return text;} 
            set { text = value; }
        }

        public List<Token> Skipped { 
            get { return skipped;} 
            set { skipped = value; }
        }
        public object Value { 
            get { return value;} 
            set { this.value = value; }
        }

        [XmlAttribute]
        public TokenType Type;

        public Token()
            : this(0, 0)
        {
        }

        public Token(int start, int end)
        {
            Type = TokenType._UNDETERMINED_;
            startpos = start;
            endpos = end;
            line = 10000;
            column = 10000;
            Text = ""; // must initialize with empty string, may cause null reference exceptions otherwise
            Value = null;
        }

        public void UpdateRange(Token token)
        {
            if (token.StartPos < startpos) startpos = token.StartPos;
            if (token.EndPos > endpos) endpos = token.EndPos;
            if (token.Line < line) line = token.Line;
            if (token.Column < column) column = token.Column;
    }

        public override string ToString()
        {
            if (Text != null)
                return Type.ToString() + " '" + Text + "'";
            else
                return Type.ToString();
        }
    }

    #endregion
}
