// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

namespace Yoakke.Platform.X86
{
    /// <summary>
    /// A single label to be able to address the place by name.
    /// </summary>
    public class Label : ICodeElement
    {
        /// <summary>
        /// The name of this <see cref="Label"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Label"/> class.
        /// </summary>
        /// <param name="name">The name of the label.</param>
        public Label(string name)
        {
            this.Name = name;
        }
    }
}
