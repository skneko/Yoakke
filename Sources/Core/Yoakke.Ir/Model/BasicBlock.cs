// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yoakke.Ir.Model.Attributes;

namespace Yoakke.Ir.Model
{
    /// <summary>
    /// Represents a continuous sequence of instructions that can only contain branching as their last instruction
    /// and can only be targeted at the first instruction as jump targets. This means that it's always guaranteed
    /// that the contained instructions all run in a sequence, as if this was an atomic operation.
    /// </summary>
    public class BasicBlock : IAttributeTarget
    {
        /// <summary>
        /// A default <see cref="BasicBlock"/> to signal an unset/invalid property.
        /// </summary>
        internal static readonly BasicBlock Invalid = new();

        /// <summary>
        /// The suggested name of the basic block.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// The instructions in the basic block.
        /// </summary>
        public IList<Instruction> Instructions { get; init; } = new List<Instruction>();

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicBlock"/> class.
        /// </summary>
        /// <param name="name">The suggested name of the basic block.</param>
        public BasicBlock(string? name = null)
        {
            this.Name = name;
        }

        #region AttributeTarget

        /// <inheritdoc/>
        public Attributes.AttributeTargets Flag => this.attributeTarget.Flag;

        private readonly AttributeTarget attributeTarget = new(Attributes.AttributeTargets.BasicBlock);

        /// <inheritdoc/>
        public IEnumerable<IAttribute> GetAttributes() => this.attributeTarget.GetAttributes();

        /// <inheritdoc/>
        public IEnumerable<IAttribute> GetAttributes(string name) => this.attributeTarget.GetAttributes(name);

        /// <inheritdoc/>
        public IEnumerable<TAttrib> GetAttributes<TAttrib>()
            where TAttrib : IAttribute => this.attributeTarget.GetAttributes<TAttrib>();

        /// <inheritdoc/>
        public bool TryGetAttribute(string name, [MaybeNullWhen(false)] out IAttribute attribute) =>
            this.attributeTarget.TryGetAttribute(name, out attribute);

        /// <inheritdoc/>
        public bool TryGetAttribute<TAttrib>([MaybeNullWhen(false)] out TAttrib attribute)
            where TAttrib : IAttribute => this.attributeTarget.TryGetAttribute(out attribute);

        /// <inheritdoc/>
        public void AddAttribute(IAttribute attribute) => this.attributeTarget.AddAttribute(attribute);

        #endregion AttributeTarget
    }
}
