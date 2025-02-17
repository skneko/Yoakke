// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Yoakke.SourceGenerator.Common.RoslynExtensions
{
    /// <summary>
    /// Extension functionalities for <see cref="ISymbol"/>s.
    /// </summary>
    public static class SymbolExtensions
    {
        /// <summary>
        /// Retrieves the type kind of a <see cref="ISymbol"/> that can be used in codegen.
        /// This can be 'class', 'struct' or 'record'.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to get the kind of.</param>
        /// <returns>The code-friendly name of the type kind.</returns>
        public static string GetTypeKindName(this ITypeSymbol symbol) => symbol.TypeKind switch
        {
            TypeKind.Class => symbol.IsRecord ? "record" : "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => throw new NotSupportedException(),
        };

        /// <summary>
        /// Checks, if a <see cref="ITypeSymbol"/>s declaration is partial.
        /// </summary>
        /// <param name="symbol">The <see cref="ITypeSymbol"/> to check.</param>
        /// <returns>True, if <paramref name="symbol"/> is declared partial.</returns>
        public static bool IsPartial(this ITypeSymbol symbol) => symbol.DeclaringSyntaxReferences
            .Any(syntaxRef => syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl && typeDecl.IsPartial());

        /// <summary>
        /// Checks, if a <see cref="ISymbol"/> is a nested type.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to check.</param>
        /// <returns>True, if <paramref name="symbol"/> is a nested type.</returns>
        public static bool IsNested(this ISymbol symbol) => symbol.ContainingSymbol is ITypeSymbol;

        /// <summary>
        /// Checks, if an <see cref="ISymbol"/> can accept declarations from other source files.
        /// This usually means that the symbol is a namespace or is in partial type definitions.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to check.</param>
        /// <returns>True, if <paramref name="symbol"/> accepts declarations inside it from other sources.</returns>
        public static bool CanDeclareInsideExternally(this ISymbol symbol)
        {
            if (symbol is null || symbol is INamespaceSymbol) return true;
            if (symbol is ITypeSymbol type) return type.IsPartial() && (type.ContainingSymbol?.CanDeclareInsideExternally() ?? true);
            return false;
        }

        /// <summary>
        /// Builds up code that is required to define something inside an <see cref="ISymbol"/>.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to build up the code for.</param>
        /// <returns>A pair of prefix and suffix text, that is enough to write declarations inside <paramref name="symbol"/>.</returns>
        public static (string Prefix, string Suffix) DeclareInsideExternally(this ISymbol symbol)
        {
            var prefixBuilder = new StringBuilder();
            var suffixBuilder = new StringBuilder();

            void DeclareInsideExternallyRec(ISymbol symbol)
            {
                if (symbol is null) return;
                if (symbol is INamedTypeSymbol type)
                {
                    if (!type.IsPartial()) throw new InvalidOperationException("Non-partial type nesting");
                    DeclareInsideExternallyRec(symbol.ContainingSymbol);

                    var (genericTypes, genericConstraints) = type.GetGenericCrud();
                    prefixBuilder!.AppendLine($"partial {type.GetTypeKindName()} {type.Name}{genericTypes} {genericConstraints} {{");
                    suffixBuilder!.AppendLine("}");
                    return;
                }
                if (symbol is INamespaceSymbol ns)
                {
                    prefixBuilder!.AppendLine($"namespace {ns.ToDisplayString()} {{");
                    suffixBuilder!.AppendLine("}");
                    return;
                }
                throw new InvalidOperationException("Unknown symbol to nest in");
            }

            DeclareInsideExternallyRec(symbol);
            return (prefixBuilder.ToString(), suffixBuilder.ToString());
        }

        /// <summary>
        /// Retrieves the generic crud needed for a type definition.
        /// Useful for partial types.
        /// </summary>
        /// <param name="symbol">The type symbol to get the crud for.</param>
        /// <returns>The pair of type parameter list and generic constraints as strings.</returns>
        public static (string TypeParameters, string Constraints) GetGenericCrud(this INamedTypeSymbol symbol)
        {
            if (symbol.TypeParameters.Length == 0) return (string.Empty, string.Empty);

            var typeParams = $"<{string.Join(", ", symbol.TypeParameters.Select(p => p.Name))}>";

            var constraints = new StringBuilder();
            foreach (var param in symbol.TypeParameters)
            {
                // First type constraints
                var constraintList = param.ConstraintTypes.Select(t => t.ToDisplayString()).ToList();

                // Then all other
                if (param.HasConstructorConstraint) constraintList.Add("new()");
                if (param.HasNotNullConstraint) constraintList.Add("notnull");
                if (param.HasReferenceTypeConstraint) constraintList.Add("class");
                if (param.HasUnmanagedTypeConstraint) constraintList.Add("unmanaged");
                if (param.HasValueTypeConstraint) constraintList.Add("struct");

                if (constraintList.Count > 0) constraints.AppendLine($"where {param.Name} : {string.Join(", ", constraintList)}");
            }

            return (typeParams, constraints.ToString());
        }

        /// <summary>
        /// Checks, if a <see cref="ISymbol"/> implements a given interface.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to check the interface for.</param>
        /// <param name="interfaceSymbol">The interface symbol to search for.</param>
        /// <returns>True, if <paramref name="symbol"/> implements <paramref name="interfaceSymbol"/>.</returns>
        public static bool ImplementsInterface(this ITypeSymbol symbol, INamedTypeSymbol interfaceSymbol) =>
               SymbolEqualityComparer.Default.Equals(symbol, interfaceSymbol)
            || symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol));

        /// <summary>
        /// Checks, if a <see cref="ISymbol"/> implements a generic interface.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to check the interface for.</param>
        /// <param name="interfaceSymbol">The generic interface symbol to search for.</param>
        /// <returns>True, if <paramref name="symbol"/> implements <paramref name="interfaceSymbol"/>.</returns>
        public static bool ImplementsGenericInterface(this ITypeSymbol symbol, INamedTypeSymbol interfaceSymbol) =>
            ImplementsGenericInterface(symbol, interfaceSymbol, out _);

        /// <summary>
        /// Checks, if a <see cref="ISymbol"/> implements a generic interface.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to check the interface for.</param>
        /// <param name="interfaceSymbol">The generic interface symbol to search for.</param>
        /// <param name="genericArgs">The passed in type-arguments for <paramref name="interfaceSymbol"/> are written here,
        /// in case <paramref name="symbol"/> implements it.</param>
        /// <returns>True, if <paramref name="symbol"/> implements <paramref name="interfaceSymbol"/>.</returns>
        public static bool ImplementsGenericInterface(
            this ITypeSymbol symbol,
            INamedTypeSymbol interfaceSymbol,
            [MaybeNullWhen(false)] out IReadOnlyList<ITypeSymbol>? genericArgs)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, interfaceSymbol))
            {
                genericArgs = ((INamedTypeSymbol)symbol).TypeArguments;
                return true;
            }
            var sub = symbol.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, interfaceSymbol));
            if (sub is not null)
            {
                genericArgs = sub.TypeArguments;
                return true;
            }
            genericArgs = null;
            return false;
        }

        /// <summary>
        /// Determines if the given property has a backing field or not.
        /// </summary>
        /// <param name="symbol">The <see cref="IPropertySymbol"/> to check.</param>
        /// <returns>True, if <paramref name="symbol"/> has a backing field.</returns>
        public static bool HasBackingField(this IPropertySymbol symbol) => symbol.ContainingType
            .GetMembers()
            .Any(m => m is IFieldSymbol f && SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, symbol));

        /// <summary>
        /// Retrieves all <see cref="AttributeData"/> attached to a <see cref="ISymbol"/> of the same attribute type.
        /// </summary>
        /// <typeparam name="T">The element type of the structure to parse into.</typeparam>
        /// <param name="symbol">The <see cref="ISymbol"/> to search in.</param>
        /// <param name="attributeSymbol">The symbol of the attribute to search for.</param>
        /// <returns>The found <see cref="AttributeData"/> list parsed.</returns>
        public static IReadOnlyList<T> GetAttributes<T>(this ISymbol symbol, INamedTypeSymbol attributeSymbol)
            where T : new() => symbol
            .GetAttributes(attributeSymbol)
            .Select(attr => attr.ParseInto<T>())
            .ToList();

        /// <summary>
        /// Retrieves all <see cref="AttributeData"/> attached to a <see cref="ISymbol"/> of the same attribute type.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to search in.</param>
        /// <param name="attributeSymbol">The symbol of the attribute to search for.</param>
        /// <returns>The found <see cref="AttributeData"/> list.</returns>
        public static IReadOnlyList<AttributeData> GetAttributes(this ISymbol symbol, INamedTypeSymbol attributeSymbol) => symbol
            .GetAttributes()
            .Where(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol))
            .ToList();

        /// <summary>
        /// Checks if a <see cref="ISymbol"/> has a given attribute.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to check.</param>
        /// <param name="attributeSymbol">The attribute symbol to search for.</param>
        /// <returns>True, if <paramref name="symbol"/> has an attribut <paramref name="attributeSymbol"/>.</returns>
        public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol attributeSymbol) =>
            symbol.TryGetAttribute(attributeSymbol, out _);

        /// <summary>
        /// Retrieves a given <see cref="AttributeData"/> attached to a <see cref="ISymbol"/>.
        /// </summary>
        /// <typeparam name="T">The type of the structure to parse into.</typeparam>
        /// <param name="symbol">The <see cref="ISymbol"/> to search in.</param>
        /// <param name="attributeSymbol">The symbol of the attribute to search for.</param>
        /// <returns>The found and parsed <see cref="AttributeData"/>.</returns>
        public static T GetAttribute<T>(this ISymbol symbol, INamedTypeSymbol attributeSymbol)
            where T : new()
        {
            var attrData = symbol.GetAttribute(attributeSymbol);
            return attrData.ParseInto<T>();
        }

        /// <summary>
        /// Retrieves a given <see cref="AttributeData"/> attached to a <see cref="ISymbol"/>.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to search in.</param>
        /// <param name="attributeSymbol">The symbol of the attribute to search for.</param>
        /// <returns>The found <see cref="AttributeData"/>.</returns>
        public static AttributeData GetAttribute(this ISymbol symbol, INamedTypeSymbol attributeSymbol)
        {
            if (!symbol.TryGetAttribute(attributeSymbol, out var result)) throw new InvalidOperationException();
            return result;
        }

        /// <summary>
        /// Tries to retrieve a given <see cref="AttributeData"/> attached to a <see cref="ISymbol"/>.
        /// </summary>
        /// <typeparam name="T">The type of the structure to parse into.</typeparam>
        /// <param name="symbol">The <see cref="ISymbol"/> to search in.</param>
        /// <param name="attributeSymbol">The attribute symbol to search for.</param>
        /// <param name="result">The parsed structure gets written here, an attribute is found.</param>
        /// <returns>True, if the attribute <paramref name="attributeSymbol"/> is found.</returns>
        public static bool TryGetAttribute<T>(
            this ISymbol symbol,
            INamedTypeSymbol attributeSymbol,
            [MaybeNullWhen(false)] out T result)
            where T : new()
        {
            if (symbol.TryGetAttribute(attributeSymbol, out var attributeData))
            {
                result = attributeData.ParseInto<T>();
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// Tries to retrieve a given <see cref="AttributeData"/> attached to a <see cref="ISymbol"/>.
        /// </summary>
        /// <param name="symbol">The <see cref="ISymbol"/> to search in.</param>
        /// <param name="attributeSymbol">The attribute symbol to search for.</param>
        /// <param name="attributeData">The <see cref="AttributeData"/> gets written here, if it's found.</param>
        /// <returns>True, if the attribute <paramref name="attributeSymbol"/> is found.</returns>
        public static bool TryGetAttribute(
            this ISymbol symbol,
            INamedTypeSymbol attributeSymbol,
            [MaybeNullWhen(false)] out AttributeData attributeData)
        {
            attributeData = symbol
                .GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol));
            return attributeData is not null;
        }

        /// <summary>
        /// Retrieves all <see cref="INamedTypeSymbol"/>s defined in the given <see cref="IAssemblySymbol"/>.
        /// </summary>
        /// <param name="assembly">The <see cref="IAssemblySymbol"/> to search for types.</param>
        /// <returns>The sequence of declared types in <paramref name="assembly"/>.</returns>
        public static IEnumerable<INamedTypeSymbol> GetAllDeclaredTypes(this IAssemblySymbol assembly) =>
            assembly.GlobalNamespace.GetAllDeclaredTypes();

        /// <summary>
        /// Retrieves all <see cref="INamedTypeSymbol"/>s defined in the given <see cref="INamespaceSymbol"/>.
        /// </summary>
        /// <param name="namespace">The <see cref="INamespaceSymbol"/> to search for types.</param>
        /// <returns>The sequence of declared types in <paramref name="namespace"/>.</returns>
        public static IEnumerable<INamedTypeSymbol> GetAllDeclaredTypes(this INamespaceSymbol @namespace)
        {
            foreach (var subNs in @namespace.GetNamespaceMembers())
            {
                foreach (var type in subNs.GetAllDeclaredTypes()) yield return type;
            }

            foreach (var type in @namespace.GetTypeMembers())
            {
                yield return type;
                foreach (var subType in type.GetAllDeclaredTypes()) yield return subType;
            }
        }

        /// <summary>
        /// Retrieves all <see cref="INamedTypeSymbol"/>s defined in the given <see cref="INamedTypeSymbol"/>.
        /// </summary>
        /// <param name="type">The <see cref="INamedTypeSymbol"/> to search for types.</param>
        /// <returns>The sequence of declared types in <paramref name="type"/>.</returns>
        public static IEnumerable<INamedTypeSymbol> GetAllDeclaredTypes(this INamedTypeSymbol type)
        {
            foreach (var subType in type.GetTypeMembers())
            {
                yield return subType;
                foreach (var subSubType in subType.GetAllDeclaredTypes()) yield return subSubType;
            }
        }

        /// <summary>
        /// Checks, if a type has no user-defined constructors.
        /// </summary>
        /// <param name="type">The <see cref="INamedTypeSymbol"/> to check.</param>
        /// <returns>True, if <paramref name="type"/> has no user-defined constructors.</returns>
        public static bool HasNoUserDefinedCtors(this INamedTypeSymbol type) =>
               type.InstanceConstructors.Length == 0
            || type.InstanceConstructors.All(c => c.IsImplicitlyDeclared);
    }
}
