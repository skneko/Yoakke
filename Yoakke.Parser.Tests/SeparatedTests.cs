﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yoakke.Lexer;
using Yoakke.Lexer.Attributes;
using Yoakke.Parser.Attributes;
using IgnoreAttribute = Yoakke.Lexer.Attributes.IgnoreAttribute;

namespace Yoakke.Parser.Tests
{
    [Parser(typeof(SeparatedTests.TokenType))]
    partial class ListParser
    {
        [Rule("any0_no_trailing : Lparen (Identifier (',' Identifier)*)? Rparen")]
        private static List<string> ZeroOrMoreNoTrailing(
            IToken _lp,
            Punctuated<Token<SeparatedTests.TokenType>, Token<SeparatedTests.TokenType>> elements,
            IToken _rp) => elements.Values.Select(t => t.Text).ToList();

    }

    [TestClass]
    public class SeparatedTests
    {
        [Lexer("SepLexer")]
        public enum TokenType
        {
            [End] End,
            [Error] Error,
            [Ignore] [Regex(Regex.Space)] Whitespace,

            [Token("(")] Lparen,
            [Token(")")] Rparen,
            [Token(",")] Comma,
            [Regex(Regex.Ident)] Identifier,
        }

        private static List<string> Any0NoTrailing(string source) => new ListParser(new SepLexer(source)).ParseAny0NoTrailing().Ok.Value;

        [TestMethod]
        public void TestEmpty()
        {
            Assert.IsTrue(Any0NoTrailing("()").SequenceEqual(new string[] { }));
        }

        [TestMethod]
        public void TestOne()
        {
            Assert.IsTrue(Any0NoTrailing("(a)").SequenceEqual(new string[] { "a" }));
        }

        [TestMethod]
        public void TestTwo()
        {
            Assert.IsTrue(Any0NoTrailing("(a, b)").SequenceEqual(new string[] { "a", "b" }));
        }

        [TestMethod]
        public void TestMany()
        {
            Assert.IsTrue(Any0NoTrailing("(a, b, c, d, e)").SequenceEqual(new string[] { "a", "b", "c", "d", "e" }));
        }
    }
}