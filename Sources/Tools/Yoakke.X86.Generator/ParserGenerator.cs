// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yoakke.X86.Generator.Model;

namespace Yoakke.X86.Generator
{
    /// <summary>
    /// The generator that generates the x86 instruction parser.
    /// </summary>
    public class ParserGenerator
    {
        /// <summary>
        /// Generates the code for the <see cref="Instruction"/> parser.
        /// </summary>
        /// <param name="instructionSet">The ISA to generate the parser for.</param>
        /// <param name="withClasses">The <see cref="Instruction"/>s that have a class generated.
        /// The ones that don't, won't generate parser code.</param>
        /// <returns>The code for the generated parser.</returns>
        public static string Generate(InstructionSet instructionSet, ISet<Instruction> withClasses)
        {
            var generator = new ParserGenerator(instructionSet, withClasses);
            generator.BuildParserTree();
            generator.GenerateParser();
            var code = generator.resultBuilder.ToString();
            code = CleanUpParserCode(code);
            return code;
        }

        private static string CleanUpParserCode(string code)
        {
            var result = new StringBuilder();
            var reader = new StringReader(code);
            var ignore = false;
            while (true)
            {
                var line = reader.ReadLine();
                if (line is null) break;

                var trimmedLine = line.TrimStart();

                if (trimmedLine.StartsWith("}")) ignore = false;
                if (ignore) continue;
                ignore = line.TrimStart().StartsWith("return");
                result.AppendLine(line);
            }
            return result.ToString();
        }

        private readonly InstructionSet instructionSet;
        private readonly ISet<Instruction> withClasses;
        private readonly StringBuilder resultBuilder;
        private ParseNode parseTree = new(MatchType.None);
        private int indent = 0;
        private string? parsedModrmName;

        private ParserGenerator(InstructionSet instructionSet, ISet<Instruction> withClasses)
        {
            this.instructionSet = instructionSet;
            this.withClasses = withClasses;
            this.resultBuilder = new();
        }

        private void GenerateParser() => this.GenerateNode(this.parseTree);

        private void GenerateNode(ParseNode node)
        {
            var subnodesByType = node.Subnodes.GroupBy(s => s.Value.Type);

            var byOpcode = subnodesByType.FirstOrDefault(g => g.Key == MatchType.Opcode);
            var byModRmReg = subnodesByType.FirstOrDefault(g => g.Key == MatchType.ModRmReg);
            var byPrefix = subnodesByType.FirstOrDefault(g => g.Key == MatchType.Prefix);

            this.GenerateOpcodeMatches(byOpcode);
            this.GenerateModRmRegMatches(byModRmReg);
            this.GeneratePrefixMatches(byPrefix);

            foreach (var encoding in node.Encodings) this.GenerateLeaf(encoding);
        }

        private void GenerateOpcodeMatches(IEnumerable<KeyValuePair<byte, ParseNode>>? nodes)
        {
            if (nodes is null) return;

            // Read in a byte
            this.Indented().AppendLine($"var byte{this.indent} = this.ParseByte();");
            // Switch on alternatives
            this.Indented().AppendLine($"switch (byte{this.indent})");
            this.Indented().AppendLine("{");

            // The different cases
            foreach (var (nextByte, subnode) in nodes)
            {
                this.Indented().AppendLine($"case 0x{nextByte:x2}:");
                this.Indented().AppendLine("{");
                this.Indent();

                this.GenerateNode(subnode);

                this.Indented().AppendLine("break;");
                this.Unindent();
                this.Indented().AppendLine("}");
            }

            this.Indented().AppendLine("}");
            // We didn't use the byte, un-eat it
            this.Indented().AppendLine("this.UnparseByte();");
        }

        private void GenerateModRmRegMatches(IEnumerable<KeyValuePair<byte, ParseNode>>? nodes)
        {
            if (nodes is null) return;

            Debug.Assert(nodes.Count() <= 8, "There must be at most 8 encodings for ModRM extensions");

            // Read in the ModRM byte
            // NOTE: We tag modrm byte with indentation to avoid name collision
            this.parsedModrmName = $"modrm{this.indent}";
            this.Indented().AppendLine($"var {this.parsedModrmName} = this.ParseByte();");
            // Switch on alternatives for the register
            this.Indented().AppendLine($"switch (({this.parsedModrmName} >> 3) & 0b111)");
            this.Indented().AppendLine("{");

            // The different cases
            foreach (var (regByte, subnode) in nodes)
            {
                this.Indented().AppendLine($"case 0x{regByte:x2}:");
                this.Indented().AppendLine("{");
                this.Indent();

                this.GenerateNode(subnode);

                this.Indented().AppendLine("break;");
                this.Unindent();
                this.Indented().AppendLine("}");
            }

            this.Indented().AppendLine("}");
            // We didn't use the byte, un-eat it
            this.Indented().AppendLine("this.UnparseByte();");
            this.parsedModrmName = null;
        }

        private void GeneratePrefixMatches(IEnumerable<KeyValuePair<byte, ParseNode>>? nodes)
        {
            if (nodes is null) return;

            foreach (var (prefixByte, subnode) in nodes)
            {
                this.Indented().AppendLine($"if (HasPrefix(0x{prefixByte:x2}))");
                this.Indented().AppendLine("{");
                this.Indent();

                this.GenerateNode(subnode);

                this.Unindent();
                this.Indented().AppendLine("}");
            }
        }

        private void GenerateLeaf(Model.Encoding encoding)
        {
            var operands = encoding.Form.Operands;
            var args = new string[encoding.Form.Operands.Count];

            /* Prefixes are already matched in the tree are already eaten */

            /* Opcodes are already eaten */

            // ModRM
            if (encoding.ModRM is not null)
            {
                var modrm = encoding.ModRM;
                // TODO: Check if we can safely skip this?
                // We handle this with the regular ModRM
                if (modrm.Mode == "11") return;

                var alreadyConsumedModRm = this.parsedModrmName is not null;
                // Consume ModRM, if we haven't already
                if (this.parsedModrmName is null)
                {
                    // NOTE: We tag modrm byte with indentation to avoid name collision
                    this.parsedModrmName = $"modrm{this.indent}";
                    this.Indented().AppendLine($"var {this.parsedModrmName} = this.ParseByte();");
                }

                if (modrm.Reg.StartsWith('#'))
                {
                    // Regular ModRM, not opcode extension
                    // We can just convert the reg
                    var regOperandIndex = int.Parse(modrm.Reg.Substring(1));
                    var size = GetDataWidthForOperand(encoding.Form.Operands[regOperandIndex]);
                    args[regOperandIndex] = $"FromRegisterIndex(({this.parsedModrmName} >> 3) & 0b111, {size})";
                }

                // Mode and RM
                Debug.Assert(modrm.Mode == "11" || modrm.Mode == modrm.Rm, "Mode and RM have to reference the same argument");
                var rmOperandIndex = int.Parse(modrm.Rm.Substring(1));
                var rmOperandSize = GetDataWidthForOperand(encoding.Form.Operands[rmOperandIndex]);
                // NOTE: We tag RM with indentation to avoid name collision
                this.Indented().AppendLine($"var rm{this.indent} = this.ParseRM({this.parsedModrmName}, {rmOperandSize});");
                args[rmOperandIndex] = $"rm{this.indent}";

                if (!alreadyConsumedModRm) this.parsedModrmName = null;
                // NOTE: We don't un-parse here, we assume this has to work for now
            }

            // Postbyte
            if (encoding.Postbyte is not null)
            {
                this.Indented().AppendLine($"// TODO: Missing encoding for {encoding.Form.Instruction.Name} (POSTBYTE)");
                return;
            }

            // Immediates
            for (var i = 0; i < encoding.Immediates.Count; ++i)
            {
                var immediate = encoding.Immediates[i];
                // NOTE: We tag immediates with indentation to avoid name collision
                this.Indented().AppendLine($"var imm{i}_{this.indent} = this.ParseImmediate({GetDataWidthForSize(immediate.Size)});");
                args[immediate.OperandNumber] = $"imm{i}_{this.indent}";
            }

            // Code offsets
            for (var i = 0; i < encoding.CodeOffsets.Count; ++i)
            {
                var immediate = encoding.CodeOffsets[i];
                // NOTE: We tag immediates with indentation to avoid name collision
                this.Indented().AppendLine($"var rel{i}_{this.indent} = this.ParseCodeOffset({GetDataWidthForSize(immediate.Size)});");
                args[immediate.OperandNumber] = $"rel{i}_{this.indent}";
            }

            // If it's a last 3 bit encoding, do that here
            for (var i = 0; i < encoding.Opcodes.Count; ++i)
            {
                var last3 = encoding.Opcodes[i].Last3BitsEncodedOperand;
                if (last3 is not null)
                {
                    var size = GetDataWidthForOperand(encoding.Form.Operands[last3.Value]);
                    args[last3.Value] = $"FromRegisterIndex(byte{i} & 0b111, {size})";
                }
            }

            // Deduce constant operands
            for (var i = 0; i < operands.Count; ++i)
            {
                // We don't care about already deduced args
                if (args[i] is not null) continue;

                var op = GenerateParserConstantOperand(operands[i]);
                if (op is not null) args[i] = op;
            }

            // We actually support everything
            var argsStr = string.Join(", ", args);
            var name = Capitalize(encoding.Form.Instruction.Name);
            this.Indented().AppendLine("length = this.Commit();");
            this.Indented().AppendLine($"return new Instructions.{name}({argsStr});");
        }

        private void BuildParserTree()
        {
            // Build the tree for the parser
            var root = new ParseNode(MatchType.None);
            foreach (var instruction in this.instructionSet.Instructions)
            {
                // We skip it if it doesn't have a generated class
                if (!this.withClasses.Contains(instruction)) continue;

                foreach (var form in instruction.Forms)
                {
                    // Skip forms that have an unsupported operand
                    if (!form.Operands.All(SupportsOperand)) continue;

                    foreach (var encoding in form.Encodings)
                    {
                        // For safety we don't include anything unsupported
                        if (encoding.HasUnsupportedElement) continue;

                        // Add the element to the tree
                        root.AddEncoding(encoding);
                    }
                }
            }

            // Store as the new tree
            this.parseTree = root;
        }

        private StringBuilder Indented() => this.resultBuilder.Append(' ', this.indent * 4);

        private void Indent() => ++this.indent;

        private void Unindent() => --this.indent;

        private static bool SupportsOperand(Operand operand) => operand.Type switch
        {
               "1" or "3"
            or "al" or "cl" or "ax" or "eax" or "rax"
            or "r8" or "r16" or "r32" or "r64"
            or "m"
            or "m8" or "m16" or "m32" or "m64" or "m128" or "m256" or "m512"
            or "imm8" or "imm16" or "imm32" or "imm64" => true,
            _ => false,
        };

        // Returns a not null string for operands that are constants and don't require byte-parsing
        private static string? GenerateParserConstantOperand(Operand operand) => operand.Type switch
        {
            "1" or "3" => $"new Constant({operand.Type})",
            "al" => $"Registers.Al",
            "cl" => $"Registers.Cl",
            "ax" => $"Registers.Ax",
            "eax" => $"Registers.Eax",
            "rax" => $"Registers.Rax",
            _ => null,
        };

        private static string GetDataWidthForOperand(Operand operand) => operand.Type switch
        {
            "m" => "null",
            "r8" or "m8" => "DataWidth.Byte",
            "r16" or "m16" => "DataWidth.Word",
            "r32" or "m32" => "DataWidth.Dword",
            "r64" or "m64" => "DataWidth.Qword",
            _ => throw new NotSupportedException(),
        };

        private static string GetDataWidthForSize(int size) => size switch
        {
            1 => "DataWidth.Byte",
            2 => "DataWidth.Word",
            4 => "DataWidth.Dword",
            8 => "DataWidth.Qword",
            _ => throw new NotSupportedException(),
        };

        private static string Capitalize(string name) => $"{char.ToUpper(name[0])}{name.Substring(1).ToLower()}";
    }
}