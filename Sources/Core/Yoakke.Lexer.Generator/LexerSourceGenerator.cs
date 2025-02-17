// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using System;
using System.Collections.Generic;
using System.Collections.Generic.Polyfill;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Yoakke.Automata;
using Yoakke.Automata.Dense;
using Yoakke.Collections.Intervals;
using Yoakke.SourceGenerator.Common;
using Yoakke.SourceGenerator.Common.RoslynExtensions;

namespace Yoakke.Lexer.Generator
{
    /// <summary>
    /// Source generator for lexers.
    /// Generates a DFA-driven lexer from annotated token types.
    /// </summary>
    [Generator]
    public class LexerSourceGenerator : GeneratorBase
    {
        private class SyntaxReceiver : ISyntaxReceiver
        {
            public IList<TypeDeclarationSyntax> CandidateTypes { get; } = new List<TypeDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is TypeDeclarationSyntax typeDeclSyntax && typeDeclSyntax.AttributeLists.Count > 0)
                {
                    this.CandidateTypes.Add(typeDeclSyntax);
                }
            }
        }

        private class LexerAttribute
        {
            public INamedTypeSymbol? TokenType { get; set; }
        }

        private class RegexAttribute
        {
            public string Regex { get; set; } = string.Empty;
        }

        private class TokenAttribute
        {
            public string Text { get; set; } = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LexerSourceGenerator"/> class.
        /// </summary>
        public LexerSourceGenerator()
            : base("Yoakke.Lexer.Generator")
        {
        }

        /// <inheritdoc/>
        protected override ISyntaxReceiver CreateSyntaxReceiver(GeneratorInitializationContext context) => new SyntaxReceiver();

        /// <inheritdoc/>
        protected override bool IsOwnSyntaxReceiver(ISyntaxReceiver syntaxReceiver) => syntaxReceiver is SyntaxReceiver;

        /// <inheritdoc/>
        protected override void GenerateCode(ISyntaxReceiver syntaxReceiver)
        {
            var receiver = (SyntaxReceiver)syntaxReceiver;

            this.RequireLibrary("Yoakke.Lexer");

            var lexerAttribute = this.LoadSymbol(TypeNames.LexerAttribute);

            foreach (var syntax in receiver.CandidateTypes)
            {
                var model = this.Context.Compilation.GetSemanticModel(syntax.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(syntax) as INamedTypeSymbol;
                if (symbol is null) continue;
                // Filter classes without the lexer attributes
                if (!symbol.TryGetAttribute(lexerAttribute, out LexerAttribute? attr)) continue;
                // Generate code for it
                var generated = this.GenerateImplementation(symbol!, attr!.TokenType!);
                if (generated is null) continue;
                this.AddSource($"{symbol!.ToDisplayString()}.Generated.cs", generated);
            }
        }

        private string? GenerateImplementation(INamedTypeSymbol lexerClass, INamedTypeSymbol tokenKind)
        {
            if (!this.RequireDeclarableInside(lexerClass)) return null;

            var enumName = tokenKind.ToDisplayString();
            var tokenName = $"{TypeNames.Token}<{enumName}>";
            var className = lexerClass.Name;

            // Extract the lexer from the attributes
            var description = this.ExtractLexerDescription(lexerClass, tokenKind);
            if (description is null) return null;

            // Build the DFA and state -> token associations from the description
            var dfaResult = this.BuildDfa(description);
            if (dfaResult is null) return null;
            var (dfa, dfaStateToToken) = dfaResult.Value;

            // Allocate unique numbers for each DFA state as we have the hierarchical numbers from determinization
            var dfaStateIdents = new Dictionary<StateSet<int>, int>();
            foreach (var state in dfa.States) dfaStateIdents.Add(state, dfaStateIdents.Count);

            // Group the transitions by source and destination states
            var transitionsByState = dfa.Transitions
                .GroupBy(t => t.Source)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .GroupBy(t => t.Destination)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(t => t.Symbol).ToList()));

            // For each state we need to build the transition table
            var transitionTable = new StringBuilder();
            transitionTable.AppendLine("switch (currentState) {");
            foreach (var (state, toMap) in transitionsByState)
            {
                transitionTable.AppendLine($"case {dfaStateIdents[state]}:");
                // For the current state we need transitions to a new state based on the current character
                transitionTable.AppendLine("switch (currentChar) {");
                foreach (var (destState, intervals) in toMap)
                {
                    // In the library it is rational to have an interval like ('a'; 'b'), but in practice
                    // this means that this transition might as well not exist, as these are discrete values,
                    // there can't be anything in between
                    var caseLabels = intervals.Select(MakeCase).OfType<string>();
                    if (!caseLabels.Any()) continue;

                    foreach (var caseLabel in caseLabels) transitionTable.AppendLine($"case {caseLabel}:");

                    transitionTable.AppendLine($"currentState = {dfaStateIdents[destState]};");
                    if (dfaStateToToken.TryGetValue(destState, out var token))
                    {
                        // The destination is an accepting state, save it
                        transitionTable.AppendLine("lastOffset = currentOffset;");
                        if (token.Ignore)
                        {
                            // Ignore means clear out the token type
                            transitionTable.AppendLine("lastTokenType = null;");
                        }
                        else
                        {
                            // Save token type
                            transitionTable.AppendLine($"lastTokenType = {enumName}.{token.Symbol!.Name};");
                        }
                    }

                    transitionTable.AppendLine("break;");
                }
                // Add a default arm to break out of the loop on non-matching character
                transitionTable.AppendLine("default: goto end_loop;");
                transitionTable.AppendLine("}");
                transitionTable.AppendLine("break;");
            }

            // We add blanks to the states not present that simply go to the end state
            var anyBlank = false;
            foreach (var state in dfa.States.Except(transitionsByState.Keys))
            {
                transitionTable.AppendLine($"case {dfaStateIdents[state]}:");
                anyBlank = true;
            }
            if (anyBlank) transitionTable.AppendLine("goto end_loop;");

            // Add a default arm to panic on illegal state
            transitionTable.AppendLine($"default: throw new {TypeNames.InvalidOperationException}();");
            transitionTable.AppendLine("}");

            // TODO: Consuming a single character on error might not be the best strategy
            // Also we might want to report if there was a token type that was being matched, while the error occurred

            var ctors = string.Empty;
            if (lexerClass.HasNoUserDefinedCtors() && description.SourceSymbol is null)
            {
                ctors = $@"
public {TypeNames.ICharStream} CharStream {{ get; }}

public {className}({TypeNames.ICharStream} source) {{ this.CharStream = source; }}
public {className}({TypeNames.TextReader} reader) : this(new {TypeNames.TextReaderCharStream}(reader)) {{ }}
public {className}(string text) : this(new {TypeNames.StringReader}(text)) {{ }}
";
            }

            var sourceField = description.SourceSymbol?.Name ?? "CharStream";
            var (prefix, suffix) = lexerClass.ContainingSymbol.DeclareInsideExternally();
            var (genericTypes, genericConstraints) = lexerClass.GetGenericCrud();
            return $@"
using Yoakke.Streams;
using Yoakke.Lexer;
#pragma warning disable CS0162
{prefix}
partial {lexerClass.GetTypeKindName()} {className}{genericTypes} : {TypeNames.ILexer}<{tokenName}> {genericConstraints}
{{
    public {TypeNames.Position} Position => this.{sourceField}.Position;

    public bool IsEnd {{ get; private set; }}

    {ctors}

    public {tokenName} Next()
    {{
begin:
        if (this.{sourceField}.IsEnd) 
        {{
            this.IsEnd = true;
            return this.{sourceField}.ConsumeToken({enumName}.{description.EndSymbol!.Name}, 0);
        }}

        var currentState = {dfaStateIdents[dfa.InitialState]};
        var currentOffset = 0;

        {enumName}? lastTokenType = null;
        var lastOffset = 0;

        while (true)
        {{
            if (!this.{sourceField}.TryLookAhead(currentOffset, out var currentChar)) break;
            ++currentOffset;
            {transitionTable}
        }}
end_loop:
        if (lastOffset > 0)
        {{
            if (lastTokenType is null) 
            {{
                this.{sourceField}.Consume(lastOffset);
                goto begin;
            }}
            return this.{sourceField}.ConsumeToken(lastTokenType.Value, lastOffset);
        }}
        else
        {{
            return this.{sourceField}.ConsumeToken({enumName}.{description.ErrorSymbol!.Name}, 1);
        }}
    }}
}}
{suffix}
#pragma warning restore CS0162
";
        }

        private (IDenseDfa<StateSet<int>, char> Dfa, Dictionary<StateSet<int>, TokenDescription> StateToToken)?
            BuildDfa(LexerDescription description)
        {
            var stateCount = 0;
            int MakeState() => stateCount++;

            // Store which token corresponds to which end state
            var tokenToNfaState = new Dictionary<TokenDescription, int>();
            // Construct the NFA from the regexes
            var nfa = new DenseNfa<int, char>();
            var initialState = MakeState();
            nfa.InitialStates.Add(initialState);
            var regexParser = new RegExParser();
            foreach (var token in description.Tokens)
            {
                try
                {
                    // Parse the regex
                    var regex = regexParser.Parse(token.Regex!);
                    // Desugar it
                    regex = regex.Desugar();
                    // Construct it into the NFA
                    var (start, end) = regex.ThompsonsConstruct(nfa, MakeState);
                    // Wire the initial state to the start of the construct
                    nfa.AddEpsilonTransition(initialState, start);
                    // Mark the state as accepting
                    nfa.AcceptingStates.Add(end);
                    // Save the final state as a state that accepts this token
                    tokenToNfaState.Add(token, end);
                }
                catch (Exception ex)
                {
                    this.Report(Diagnostics.FailedToParseRegularExpression, token.Symbol!.Locations.First(), ex.Message);
                    return null;
                }
            }

            // Determinize it
            var dfa = nfa.Determinize();
            var minDfa = dfa.Minimize(StateCombiner<int>.DefaultSetCombiner, dfa.AcceptingStates);
            // Now we have to figure out which new accepting states correspond to which token
            var dfaStateToToken = new Dictionary<StateSet<int>, TokenDescription>();
            // We go in the order of each token because this ensures the precedence in which order the tokens were declared
            foreach (var token in description.Tokens)
            {
                var nfaAccepting = tokenToNfaState[token];
                var dfaAccepting = minDfa.AcceptingStates.Where(dfaState => dfaState.Contains(nfaAccepting));
                foreach (var dfaState in dfaAccepting)
                {
                    // This check ensures the unambiguous accepting states
                    if (!dfaStateToToken.ContainsKey(dfaState)) dfaStateToToken.Add(dfaState, token);
                }
            }

            return (minDfa, dfaStateToToken);
        }

        private LexerDescription? ExtractLexerDescription(INamedTypeSymbol lexerClass, INamedTypeSymbol tokenKind)
        {
            var sourceAttr = this.LoadSymbol(TypeNames.CharSourceAttribute);
            var regexAttr = this.LoadSymbol(TypeNames.RegexAttribute);
            var tokenAttr = this.LoadSymbol(TypeNames.TokenAttribute);
            var endAttr = this.LoadSymbol(TypeNames.EndAttribute);
            var errorAttr = this.LoadSymbol(TypeNames.ErrorAttribute);
            var ignoreAttr = this.LoadSymbol(TypeNames.IgnoreAttribute);

            var result = new LexerDescription();

            // Search for the source field in the lexer class
            var sourceField = lexerClass.GetMembers()
                .Where(field => field.HasAttribute(sourceAttr))
                .FirstOrDefault();
            result.SourceSymbol = sourceField;

            // Deal with the enum members
            foreach (var member in tokenKind.GetMembers().OfType<IFieldSymbol>())
            {
                // End token
                if (member.HasAttribute(endAttr))
                {
                    if (result.EndSymbol is null)
                    {
                        result.EndSymbol = member;
                    }
                    else
                    {
                        this.Report(Diagnostics.FundamentalTokenTypeAlreadyDefined, member.Locations.First(), result.EndSymbol.Name, "end");
                        return null;
                    }
                    continue;
                }
                // Error token
                if (member.HasAttribute(errorAttr))
                {
                    if (result.ErrorSymbol is null)
                    {
                        result.ErrorSymbol = member;
                    }
                    else
                    {
                        this.Report(Diagnostics.FundamentalTokenTypeAlreadyDefined, member.Locations.First(), result.ErrorSymbol.Name, "error");
                        return null;
                    }
                    continue;
                }
                // Regular token
                var ignore = member.HasAttribute(ignoreAttr);
                // Ask for all regex and token attributes
                var regexAttribs = member.GetAttributes<RegexAttribute>(regexAttr);
                var tokenAttribs = member.GetAttributes<TokenAttribute>(tokenAttr);
                foreach (var attr in regexAttribs) result.Tokens.Add(new(member, attr.Regex, ignore));
                foreach (var attr in tokenAttribs) result.Tokens.Add(new(member, RegExParser.Escape(attr.Text), ignore));

                if (regexAttribs.Count == 0 && tokenAttribs.Count == 0)
                {
                    // No attribute, warn
                    this.Report(Diagnostics.NoAttributeForTokenType, member.Locations.First(), member.Name);
                }
            }
            // Check if everything has been filled out
            if (result.EndSymbol is null || result.ErrorSymbol is null)
            {
                this.Report(
                    Diagnostics.FundamentalTokenTypeNotDefined,
                    tokenKind.Locations.First(),
                    result.EndSymbol is null ? "end" : "error",
                    result.EndSymbol is null ? "EndAttribute" : "ErrorAttribute");
                return null;
            }

            return result;
        }

        private static string? MakeCase(Interval<char> interval)
        {
            var (lower, upper) = ToInclusive(interval);

            if (lower != null && upper != null && lower.Value > upper.Value) return null;

            return (lower, upper) switch
            {
                (char l, char h) when l == h => $"'{Escape(l)}'",
                (char l, char h) => $">= '{Escape(l)}' and <= '{Escape(h)}'",
                (char l, null) => $">= '{Escape(l)}'",
                (null, char h) => $"<= '{Escape(h)}'",
                (null, null) => "char ch",
            };
        }

        private static (char? Lower, char? Upper) ToInclusive(Interval<char> interval)
        {
            char? lower = interval.Lower switch
            {
                LowerBound<char>.Inclusive i => i.Value,
                LowerBound<char>.Exclusive e => (char)(e.Value + 1),
                LowerBound<char>.Unbounded => null,
                _ => throw new ArgumentOutOfRangeException(),
            };
            char? upper = interval.Upper switch
            {
                UpperBound<char>.Inclusive i => i.Value,
                UpperBound<char>.Exclusive e => (char)(e.Value - 1),
                UpperBound<char>.Unbounded => null,
                _ => throw new ArgumentOutOfRangeException(),
            };
            return (lower, upper);
        }

        private static string Escape(char ch) => ch switch
        {
            '\'' => @"\'",
            '\n' => @"\n",
            '\r' => @"\r",
            '\t' => @"\t",
            '\0' => @"\0",
            '\\' => @"\\",
            _ => ch.ToString(),
        };
    }
}
