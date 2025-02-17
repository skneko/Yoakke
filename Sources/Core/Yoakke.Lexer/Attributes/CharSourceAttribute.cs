// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using System;

namespace Yoakke.Lexer.Attributes
{
    /// <summary>
    /// An attribute to annotate the source character stream for the generated lexer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class CharSourceAttribute : Attribute
    {
    }
}
