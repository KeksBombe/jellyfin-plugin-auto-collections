using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.AutoCollections.Configuration
{
    // Types of criteria for collections
    public enum CriteriaType
    {
        Title = 0,     // Match by title
        Genre = 1,     // Match by genre
        Studio = 2,    // Match by studio
        Actor = 3,     // Match by actor
        Director = 4,  // Match by director
        Movie = 5,     // Match only movies
        Show = 6       // Match only TV shows
    }

    // Token types for expression parsing
    public enum TokenType
    {
        Criteria,   // TITLE, GENRE, STUDIO, ACTOR, DIRECTOR
        String,     // "string value"
        And,        // AND
        Or,         // OR
        Not,        // NOT
        OpenParen,  // (
        CloseParen, // )
        EOF         // End of expression
    }

    // A token in the expression
    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }
        public CriteriaType? CriteriaType { get; set; }

        public Token(TokenType type, string value = null)
        {
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            if (Type == TokenType.String)
                return $"\"{Value}\"";
            else if (Type == TokenType.Criteria)
                return CriteriaType.ToString().ToUpper();
            else
                return Type.ToString().ToUpper();
        }
    }

    // A node in the expression tree
    public abstract class ExpressionNode
    {
        public abstract bool Evaluate(Func<CriteriaType, string, bool> criteriaMatchFunc);
        public abstract override string ToString();
    }

    // A criteria node (like TITLE "Avengers")
    public class CriteriaNode : ExpressionNode
    {
        public CriteriaType CriteriaType { get; set; }
        public string Value { get; set; }

        public CriteriaNode(CriteriaType criteriaType, string value)
        {
            CriteriaType = criteriaType;
            Value = value;
        }

        public override bool Evaluate(Func<CriteriaType, string, bool> criteriaMatchFunc)
        {
            return criteriaMatchFunc(CriteriaType, Value);
        }

        public override string ToString()
        {
            // For media type criteria, don't include the value part
            if (CriteriaType == CriteriaType.Movie || CriteriaType == CriteriaType.Show)
            {
                return $"{CriteriaType.ToString().ToUpper()}";
            }
            return $"{CriteriaType.ToString().ToUpper()} \"{Value}\"";
        }
    }

    // An AND node
    public class AndNode : ExpressionNode
    {
        public ExpressionNode Left { get; set; }
        public ExpressionNode Right { get; set; }

        public AndNode(ExpressionNode left, ExpressionNode right)
        {
            Left = left;
            Right = right;
        }

        public override bool Evaluate(Func<CriteriaType, string, bool> criteriaMatchFunc)
        {
            return Left.Evaluate(criteriaMatchFunc) && Right.Evaluate(criteriaMatchFunc);
        }

        public override string ToString()
        {
            return $"({Left} AND {Right})";
        }
    }

    // An OR node
    public class OrNode : ExpressionNode
    {
        public ExpressionNode Left { get; set; }
        public ExpressionNode Right { get; set; }

        public OrNode(ExpressionNode left, ExpressionNode right)
        {
            Left = left;
            Right = right;
        }

        public override bool Evaluate(Func<CriteriaType, string, bool> criteriaMatchFunc)
        {
            return Left.Evaluate(criteriaMatchFunc) || Right.Evaluate(criteriaMatchFunc);
        }

        public override string ToString()
        {
            return $"({Left} OR {Right})";
        }
    }

    // A NOT node
    public class NotNode : ExpressionNode
    {
        public ExpressionNode Child { get; set; }

        public NotNode(ExpressionNode child)
        {
            Child = child;
        }

        public override bool Evaluate(Func<CriteriaType, string, bool> criteriaMatchFunc)
        {
            return !Child.Evaluate(criteriaMatchFunc);
        }

        public override string ToString()
        {
            return $"NOT {Child}";
        }
    }    // The main collection class that uses expressions
    public class ExpressionCollection
    {
        public string CollectionName { get; set; }
        public string Expression { get; set; }
        public bool CaseSensitive { get; set; }
        
        // Make ParsedExpression and ParseErrors non-serializable
        [System.Xml.Serialization.XmlIgnore]
        public ExpressionNode ParsedExpression { get; set; }
        
        [System.Xml.Serialization.XmlIgnore]
        public List<string> ParseErrors { get; set; }

        // Add parameterless constructor for XML serialization
        public ExpressionCollection()
        {
            CollectionName = "Auto Collection";
            Expression = string.Empty;
            CaseSensitive = false;
            ParseErrors = new List<string>();
        }

        public ExpressionCollection(string collectionName, string expression, bool caseSensitive = false)
        {
            CollectionName = collectionName;
            Expression = expression;
            CaseSensitive = caseSensitive;
            ParseErrors = new List<string>();
            
            // Parse expression when created
            ParseExpression();
        }

        // Parse the expression string into an expression tree
        public bool ParseExpression()
        {
            ParseErrors.Clear();
            if (string.IsNullOrWhiteSpace(Expression))
            {
                ParseErrors.Add("Expression cannot be empty");
                return false;
            }

            try
            {
                var tokens = TokenizeExpression(Expression);
                var position = 0;
                ParsedExpression = ParseExpressionTree(tokens, ref position);
                return true;
            }
            catch (Exception ex)
            {
                ParseErrors.Add($"Error parsing expression: {ex.Message}");
                return false;
            }
        }

        // Tokenize the expression string
        private List<Token> TokenizeExpression(string expression)
        {
            List<Token> tokens = new();
            int position = 0;

            while (position < expression.Length)
            {
                // Skip whitespace
                while (position < expression.Length && char.IsWhiteSpace(expression[position]))
                    position++;

                if (position >= expression.Length)
                    break;

                // Check for operators
                if (TryMatchOperator(expression, ref position, "AND", out var andToken))
                {
                    tokens.Add(andToken);
                    continue;
                }
                
                if (TryMatchOperator(expression, ref position, "OR", out var orToken))
                {
                    tokens.Add(orToken);
                    continue;
                }
                
                if (TryMatchOperator(expression, ref position, "NOT", out var notToken))
                {
                    tokens.Add(notToken);
                    continue;
                }

                // Check for criteria types
                if (TryMatchCriteria(expression, ref position, "TITLE", out var titleToken))
                {
                    tokens.Add(titleToken);
                    continue;
                }
                
                if (TryMatchCriteria(expression, ref position, "GENRE", out var genreToken))
                {
                    tokens.Add(genreToken);
                    continue;
                }
                
                if (TryMatchCriteria(expression, ref position, "STUDIO", out var studioToken))
                {
                    tokens.Add(studioToken);
                    continue;
                }
                
                if (TryMatchCriteria(expression, ref position, "ACTOR", out var actorToken))
                {
                    tokens.Add(actorToken);
                    continue;
                }
                
                if (TryMatchCriteria(expression, ref position, "DIRECTOR", out var directorToken))
                {
                    tokens.Add(directorToken);
                    continue;
                }
                
                if (TryMatchCriteria(expression, ref position, "MOVIE", out var movieToken))
                {
                    tokens.Add(movieToken);
                    continue;
                }
                
                if (TryMatchCriteria(expression, ref position, "SHOW", out var showToken))
                {
                    tokens.Add(showToken);
                    continue;
                }

                // Check for parentheses
                if (expression[position] == '(')
                {
                    tokens.Add(new Token(TokenType.OpenParen));
                    position++;
                    continue;
                }
                
                if (expression[position] == ')')
                {
                    tokens.Add(new Token(TokenType.CloseParen));
                    position++;
                    continue;
                }

                // Check for string literals
                if (expression[position] == '"')
                {
                    position++; // Skip opening quote
                    int startPos = position;
                    
                    // Find the closing quote
                    while (position < expression.Length && expression[position] != '"')
                        position++;
                        
                    if (position >= expression.Length)
                        throw new Exception("Unterminated string literal");
                        
                    string value = expression.Substring(startPos, position - startPos);
                    tokens.Add(new Token(TokenType.String, value));
                    
                    position++; // Skip closing quote
                    continue;
                }

                // If we get here, we have an invalid token
                throw new Exception($"Unexpected character at position {position}: {expression[position]}");
            }

            // Add EOF token
            tokens.Add(new Token(TokenType.EOF));
            return tokens;
        }

        private bool TryMatchOperator(string expression, ref int position, string op, out Token token)
        {
            token = null;
            
            if (position + op.Length <= expression.Length &&
                expression.Substring(position, op.Length).Equals(op, StringComparison.OrdinalIgnoreCase))
            {
                // Check if it's a whole word (followed by whitespace or end of string)
                if (position + op.Length == expression.Length || 
                    char.IsWhiteSpace(expression[position + op.Length]) ||
                    expression[position + op.Length] == '(' ||
                    expression[position + op.Length] == ')')
                {
                    position += op.Length;
                    
                    switch (op.ToUpper())
                    {
                        case "AND":
                            token = new Token(TokenType.And);
                            break;
                        case "OR":
                            token = new Token(TokenType.Or);
                            break;
                        case "NOT":
                            token = new Token(TokenType.Not);
                            break;
                    }
                    
                    return true;
                }
            }
            
            return false;
        }        private bool TryMatchCriteria(string expression, ref int position, string criteria, out Token token)
        {
            token = null;
            
            if (position + criteria.Length <= expression.Length &&
                expression.Substring(position, criteria.Length).Equals(criteria, StringComparison.OrdinalIgnoreCase))
            {
                // Check if it's a whole word (followed by whitespace, parenthesis, or end of string)
                if (position + criteria.Length == expression.Length || 
                    char.IsWhiteSpace(expression[position + criteria.Length]) ||
                    expression[position + criteria.Length] == '(' ||
                    expression[position + criteria.Length] == ')')
                {
                    position += criteria.Length;
                    
                    token = new Token(TokenType.Criteria);
                    
                    switch (criteria.ToUpper())
                    {
                        case "TITLE":
                            token.CriteriaType = Configuration.CriteriaType.Title;
                            break;
                        case "GENRE":
                            token.CriteriaType = Configuration.CriteriaType.Genre;
                            break;
                        case "STUDIO":
                            token.CriteriaType = Configuration.CriteriaType.Studio;
                            break;
                        case "ACTOR":
                            token.CriteriaType = Configuration.CriteriaType.Actor;
                            break;
                        case "DIRECTOR":
                            token.CriteriaType = Configuration.CriteriaType.Director;
                            break;
                        case "MOVIE":
                            token.CriteriaType = Configuration.CriteriaType.Movie;
                            break;
                        case "SHOW":
                            token.CriteriaType = Configuration.CriteriaType.Show;
                            break;
                    }
                    
                    return true;
                }
            }
            
            return false;
        }

        // Recursive descent parser to build the expression tree
        private ExpressionNode ParseExpressionTree(List<Token> tokens, ref int position)
        {
            return ParseOrExpression(tokens, ref position);
        }

        private ExpressionNode ParseOrExpression(List<Token> tokens, ref int position)
        {
            var left = ParseAndExpression(tokens, ref position);
            
            while (position < tokens.Count && tokens[position].Type == TokenType.Or)
            {
                position++; // Skip OR
                var right = ParseAndExpression(tokens, ref position);
                left = new OrNode(left, right);
            }
            
            return left;
        }

        private ExpressionNode ParseAndExpression(List<Token> tokens, ref int position)
        {
            var left = ParseNotExpression(tokens, ref position);
            
            while (position < tokens.Count && tokens[position].Type == TokenType.And)
            {
                position++; // Skip AND
                var right = ParseNotExpression(tokens, ref position);
                left = new AndNode(left, right);
            }
            
            return left;
        }

        private ExpressionNode ParseNotExpression(List<Token> tokens, ref int position)
        {
            if (position < tokens.Count && tokens[position].Type == TokenType.Not)
            {
                position++; // Skip NOT
                var child = ParsePrimaryExpression(tokens, ref position);
                return new NotNode(child);
            }
            
            return ParsePrimaryExpression(tokens, ref position);
        }

        private ExpressionNode ParsePrimaryExpression(List<Token> tokens, ref int position)
        {
            if (position >= tokens.Count)
                throw new Exception("Unexpected end of expression");
                
            // Handle parenthesized expressions
            if (tokens[position].Type == TokenType.OpenParen)
            {
                position++; // Skip (
                var node = ParseExpressionTree(tokens, ref position);
                
                if (position >= tokens.Count || tokens[position].Type != TokenType.CloseParen)
                    throw new Exception("Missing closing parenthesis");
                    
                position++; // Skip )
                return node;
            }
            
            // Handle criteria expressions
            if (tokens[position].Type == TokenType.Criteria)
            {
                var criteriaToken = tokens[position++];
                
                // Special handling for MOVIE and SHOW criteria that don't require string values
                if (criteriaToken.CriteriaType == CriteriaType.Movie || criteriaToken.CriteriaType == CriteriaType.Show)
                {
                    return new CriteriaNode(criteriaToken.CriteriaType.Value, string.Empty);
                }
                
                if (position >= tokens.Count || tokens[position].Type != TokenType.String)
                    throw new Exception($"Expected string after {criteriaToken}");
                    
                var stringToken = tokens[position++];
                return new CriteriaNode(criteriaToken.CriteriaType.Value, stringToken.Value);
            }
            
            throw new Exception($"Unexpected token: {tokens[position].Type}");
        }
    }
}
