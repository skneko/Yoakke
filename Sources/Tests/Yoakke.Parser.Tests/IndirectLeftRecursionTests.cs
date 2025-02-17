// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using Xunit;
using Yoakke.Lexer;
using Yoakke.Lexer.Attributes;
using Yoakke.Parser.Attributes;
using IgnoreAttribute = Yoakke.Lexer.Attributes.IgnoreAttribute;

namespace Yoakke.Parser.Tests
{
    public partial class IndirectLeftRecursionTests
    {
        internal enum TokenType
        {
            [End] End,
            [Error] Error,
            [Ignore] [Regex(Regexes.Whitespace)] Whitespace,

            [Regex(Regexes.Identifier)] Identifier,
        }

        [Lexer(typeof(TokenType))]
        internal partial class Lexer
        {
        }

        [Parser(typeof(TokenType))]
        internal partial class Parser
        {
            [Rule("grouping : group_element")]
            private static string Ident(string s) => s;

            [Rule("group_element : grouping Identifier")]
            private static string Group(string group, IToken next) => $"({group}, {next.Text})";

            [Rule("group_element : Identifier")]
            private static string Ident(IToken t) => t.Text;
        }

        private static string Parse(string source) =>
            new Parser(new Lexer(source)).ParseGrouping().Ok.Value;

        [Theory]
        [InlineData("a", "a")]
        [InlineData("(a, b)", "a b")]
        [InlineData("((a, b), c)", "a b c")]
        [InlineData("(((a, b), c), d)", "a b c d")]
        public void Tests(string expected, string input) => Assert.Equal(expected, Parse(input));
    }
}
