﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities
{
    public static class ISymbolExtensions
    {
        public static bool IsType(this ISymbol symbol)
        {
            var typeSymbol = symbol as ITypeSymbol;
            return typeSymbol != null && typeSymbol.IsType;
        }

        public static bool IsAccessorMethod(this ISymbol symbol)
        {
            var accessorSymbol = symbol as IMethodSymbol;
            return accessorSymbol != null &&
                (accessorSymbol.MethodKind == MethodKind.PropertySet || accessorSymbol.MethodKind == MethodKind.PropertyGet ||
                accessorSymbol.MethodKind == MethodKind.EventRemove || accessorSymbol.MethodKind == MethodKind.EventAdd);
        }

        public static bool IsPublic(this ISymbol symbol)
        {
            return symbol.DeclaredAccessibility == Accessibility.Public;
        }

        public static bool IsErrorType(this ISymbol symbol)
        {
            return
                symbol is ITypeSymbol &&
                ((ITypeSymbol)symbol).TypeKind == TypeKind.Error;
        }

        public static bool IsConstructor(this ISymbol symbol)
        {
            return (symbol as IMethodSymbol)?.MethodKind == MethodKind.Constructor;
        }

        public static bool IsDestructor(this ISymbol symbol)
        {
            return (symbol as IMethodSymbol)?.MethodKind == MethodKind.Destructor;
        }

        public static bool IsIndexer(this ISymbol symbol)
        {
            return (symbol as IPropertySymbol)?.IsIndexer == true;
        }

        public static bool IsUserDefinedOperator(this ISymbol symbol)
        {
            return (symbol as IMethodSymbol)?.MethodKind == MethodKind.UserDefinedOperator;
        }

        public static ImmutableArray<IParameterSymbol> GetParameters(this ISymbol symbol)
        {
            return symbol.TypeSwitch(
                (IMethodSymbol m) => m.Parameters,
                (IPropertySymbol p) => p.Parameters,
                _ => ImmutableArray.Create<IParameterSymbol>());
        }

        public static SymbolVisibility GetResultantVisibility(this ISymbol symbol)
        {
            // Start by assuming it's visible.
            var visibility = SymbolVisibility.Public;

            switch (symbol.Kind)
            {
                case SymbolKind.Alias:
                    // Aliases are uber private.  They're only visible in the same file that they
                    // were declared in.
                    return SymbolVisibility.Private;

                case SymbolKind.Parameter:
                    // Parameters are only as visible as their containing symbol
                    return GetResultantVisibility(symbol.ContainingSymbol);

                case SymbolKind.TypeParameter:
                    // Type Parameters are private.
                    return SymbolVisibility.Private;
            }

            while (symbol != null && symbol.Kind != SymbolKind.Namespace)
            {
                switch (symbol.DeclaredAccessibility)
                {
                    // If we see anything private, then the symbol is private.
                    case Accessibility.NotApplicable:
                    case Accessibility.Private:
                        return SymbolVisibility.Private;

                    // If we see anything internal, then knock it down from public to
                    // internal.
                    case Accessibility.Internal:
                    case Accessibility.ProtectedAndInternal:
                        visibility = SymbolVisibility.Internal;
                        break;

                        // For anything else (Public, Protected, ProtectedOrInternal), the
                        // symbol stays at the level we've gotten so far.
                }

                symbol = symbol.ContainingSymbol;
            }

            return visibility;
        }

        public static bool MatchMemberDerived(this ISymbol member, INamedTypeSymbol type, string name)
        {
            return member != null && member.ContainingType.IsDerivedFrom(type) && member.MetadataName == name;
        }

        public static bool MatchMethodDerived(this ISymbol member, INamedTypeSymbol type, string name)
        {
            return member != null && member.Kind == SymbolKind.Method && member.MatchMemberDerived(type, name);
        }

        public static bool MatchMethod(this ISymbol member, INamedTypeSymbol type, string name)
        {
            return member != null && member.Kind == SymbolKind.Method && member.MatchMember(type, name);
        }

        public static bool MatchPropertyDerived(this ISymbol member, INamedTypeSymbol type, string name)
        {
            return member != null && member.Kind == SymbolKind.Property && member.MatchMemberDerived(type, name);
        }

        public static bool MatchFieldDerived(this ISymbol member, INamedTypeSymbol type, string name)
        {
            return member != null && member.Kind == SymbolKind.Field && member.MatchMemberDerived(type, name);
        }

        public static bool MatchMember(this ISymbol member, INamedTypeSymbol type, string name)
        {
            return member != null && member.ContainingType == type && member.MetadataName == name;
        }

        public static bool MatchProperty(this ISymbol member, INamedTypeSymbol type, string name)
        {
            return member != null && member.Kind == SymbolKind.Property && member.MatchMember(type, name);
        }

        public static bool MatchField(this ISymbol member, INamedTypeSymbol type, string name)
        {
            return member != null && member.Kind == SymbolKind.Field && member.MatchMember(type, name);
        }

        public static ITypeSymbol GetVariableSymbolType(this ISymbol symbol)
        {
            if (symbol == null)
            {
                return null;
            }
            SymbolKind kind = symbol.Kind;
            switch (kind)
            {
                case SymbolKind.Field:
                    return ((IFieldSymbol)symbol).Type;
                case SymbolKind.Local:
                    return ((ILocalSymbol)symbol).Type;
                case SymbolKind.Parameter:
                    return ((IParameterSymbol)symbol).Type;
                case SymbolKind.Property:
                    return ((IPropertySymbol)symbol).Type;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Checks if a symbol is visible outside of an assembly.
        /// </summary>
        /// <param name="symbol">The symbol whose access shall be checked.</param>
        /// <returns>true if the symbol is visible outside its assembly; otherwise, false.</returns>
        public static bool IsVisibleOutsideAssembly(this ISymbol symbol)
        {
            if (symbol == null)
            {
                return false;
            }

            for (ISymbol containingType = symbol; containingType != null; containingType = containingType.ContainingType)
            {
                if (DiagnosticHelpers.IsInvisibleOutsideAssemblyAtSymbolLevel(containingType))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public enum SymbolVisibility
    {
        Public,
        Internal,
        Private,
    }
}
