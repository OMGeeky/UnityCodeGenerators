using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GetComponentGenerator
{
    [Generator]
    public class GetComponentGenerator : ISourceGenerator
    {
        private const string AttributeText = @"
using System;

[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
internal class GetComponentAttribute : Attribute
{
    public enum TargetType
    {
        This = 0,
        Parent = 1,
        Child = 2,
    }

    public GetComponentAttribute(TargetType targetType = TargetType.This)
    {
    }
}
";

        public void Initialize( GeneratorInitializationContext context )
        {
            context.RegisterForPostInitialization( i => i.AddSource( "GetComponentAttribute_g.cs" , AttributeText ) );
            context.RegisterForSyntaxNotifications( () => new SyntaxReceiver() );
        }

        public void Execute( GeneratorExecutionContext context )
        {
            if ( !(context.SyntaxContextReceiver is SyntaxReceiver receiver) )
                return;

            INamedTypeSymbol attributeSymbol = context.Compilation.GetTypeByMetadataName( "GetComponentAttribute" );

            foreach ( IGrouping<INamedTypeSymbol , IFieldSymbol> group in receiver.Fields
                                                                                  .GroupBy<IFieldSymbol , INamedTypeSymbol>( f => f.ContainingType
                                                                                                                           , SymbolEqualityComparer.Default ) )
            {
                var classSource = ProcessClass( group.Key , group , attributeSymbol );
                context.AddSource( $"{group.Key.Name}_Components_g.cs" , SourceText.From( classSource , Encoding.UTF8 ) );
            }
        }

        private string ProcessClass( INamedTypeSymbol classSymbol , IEnumerable<IFieldSymbol> fields , ISymbol attributeSymbol )
        {
            var source = new StringBuilder( $@"

public partial class {classSymbol.Name} 
{{
private void t()
{{
" );

            foreach ( IFieldSymbol fieldSymbol in fields )
            {
                ProcessField( source , fieldSymbol , attributeSymbol );
            }

            source.Append( "}\n\n}" );
            return source.ToString();
        }

        private void ProcessField( StringBuilder source , IFieldSymbol fieldSymbol , ISymbol attributeSymbol )
        {
            var fieldName = fieldSymbol.Name;
            ITypeSymbol fieldType = fieldSymbol.Type;

            AttributeData attributeData = fieldSymbol.GetAttributes()
                                                     .Single( ad =>
                                                                  ad.AttributeClass?.Equals( attributeSymbol , SymbolEqualityComparer.Default ) ?? false );

            var methodType = ProcessAttribute( attributeData );

            source.AppendLine( $@"{fieldName} = {methodType}<{fieldType}>();" );
        }

        private string ProcessAttribute( AttributeData attributeData )
        {
            var stringBuilder = new StringBuilder( "GetComponent" );
            var args = attributeData.ConstructorArguments;
            if ( args.Length > 0 && int.TryParse( args[0].Value?.ToString() , out var enumValue ) )
            {
                switch ( enumValue )
                {
                    case 1:
                        stringBuilder.Append( "InParent" );
                        break;
                    case 2:
                        stringBuilder.Append( "InChildren" );
                        break;
                }
            }

            return stringBuilder.ToString();
        }
    }

    internal class SyntaxReceiver : ISyntaxContextReceiver
    {
        public List<IFieldSymbol> Fields { get; } = new List<IFieldSymbol>();

        public void OnVisitSyntaxNode( GeneratorSyntaxContext context )
        {
            if ( !(context.Node is FieldDeclarationSyntax fieldDeclarationSyntax) || fieldDeclarationSyntax.AttributeLists.Count <= 0 )
                return;

            foreach ( VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables )
            {
                if ( context.SemanticModel.GetDeclaredSymbol( variable ) is IFieldSymbol fieldSymbol
                  && IsDerivedFrom( fieldSymbol.ContainingType.BaseType , "MonoBehaviour" )
                  && IsDerivedFrom( fieldSymbol.Type.BaseType , "Component" )
                  && fieldSymbol.GetAttributes().Any( ad => ad.AttributeClass?.ToDisplayString() == "GetComponentAttribute" ) )
                {
                    Fields.Add( fieldSymbol );
                }
            }
        }

        private static bool IsDerivedFrom( INamedTypeSymbol baseType , string targetType )
        {
            while ( baseType != null )
            {
                if ( baseType.Name == targetType )
                    return true;

                baseType = baseType.BaseType;
            }

            return false;
        }
    }
}
