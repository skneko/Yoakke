// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

namespace Yoakke.Symbols
{
    /// <summary>
    /// Represents a single symbol.
    /// </summary>
    public interface ISymbol
    {
        /// <summary>
        /// The scope that contains this symbol.
        /// </summary>
        public IReadOnlyScope Scope { get; }

        /// <summary>
        /// The name of this symbol.
        /// </summary>
        public string Name { get; }
    }
}
