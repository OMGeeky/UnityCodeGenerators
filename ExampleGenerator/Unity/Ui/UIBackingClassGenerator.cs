using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using System.Text;

using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace ExampleGenerator.Unity.Ui
{
    public static class Helpers
    {
        public const string UxmlTraitAttribute = "UxmlTraitAttribute";
        public const string UiElementAttribute = "UiElementAttribute";

        internal static bool IsDerivedFrom( INamedTypeSymbol baseType , string targetType )
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

    [Generator]
    public class UiBackingClassGenerator : ISourceGenerator
    {
        private static readonly string UxmlTraitAttributeText = $@"// <auto-generated/>
using System;
/// Helper attribute for UXML generation that generates the 
/// UxmlTrait definitions needed for the UIElements. 
/// <rbr/>
/// Works on properties and fields
/// <remarks>
/// When applied to a Property the uxml-fields only work and 
/// save if the property has a backing field. If its an auto 
/// property it won't save the changes in the UI-Builder and probably some other locations too.
/// </remarks> 
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
internal class {Helpers.UxmlTraitAttribute} : Attribute
{{
    public {Helpers.UxmlTraitAttribute}(string name, object defaultValue) {{ }}
}}
";

        private static readonly string UiElementAttributeText = $@"// <auto-generated/>
using System;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
internal class {Helpers.UiElementAttribute} : Attribute
{{
    public {Helpers.UiElementAttribute}(string name) {{ }}
}}
";

    #region Implementation of ISourceGenerator

        public void Initialize( GeneratorInitializationContext context )
        {
            context.RegisterForPostInitialization( i =>
            {
                i.AddSource( $"{Helpers.UxmlTraitAttribute}_g.cs"
                           , SourceText.From( UxmlTraitAttributeText , Encoding.UTF8 ) );

                i.AddSource( $"{Helpers.UiElementAttribute}_g.cs"
                           , SourceText.From( UiElementAttributeText , Encoding.UTF8 ) );
            } );

            context.RegisterForSyntaxNotifications( () => new SyntaxReceiver() );
        }

        public void Execute( GeneratorExecutionContext context )
        {
            if ( !(context.SyntaxContextReceiver is SyntaxReceiver receiver) )
                return;

            INamedTypeSymbol uxmlTraitAttributeSymbol = context.Compilation.GetTypeByMetadataName( Helpers.UxmlTraitAttribute );
            INamedTypeSymbol uiElementAttributeSymbol = context.Compilation.GetTypeByMetadataName( Helpers.UiElementAttribute );
            foreach ( IGrouping<INamedTypeSymbol , ISymbol> group in receiver.Fields
                                                                             .GroupBy<ISymbol , INamedTypeSymbol>( f => f.ContainingType
                                                                                , SymbolEqualityComparer.Default ) )
            {
                var classSource = ProcessClass( group.Key , group , uxmlTraitAttributeSymbol , uiElementAttributeSymbol );
                if ( classSource == null )
                    continue;

                context.AddSource( $"{group.Key.Name}_ui_g.cs" , SourceText.From( classSource , Encoding.UTF8 ) );
            }
        }

        private string ProcessClass( INamedTypeSymbol     classSymbol
                                   , IEnumerable<ISymbol> fields
                                   , INamedTypeSymbol     uxmlTraitAttributeSymbol
                                   , INamedTypeSymbol     uiElementAttributeSymbol )
        {
            var fieldsList = fields.ToList();
            if ( !fieldsList.Any() )
                return null;

            List<ISymbol> elementFields = fieldsList.Where( f => GetUiElementAttributeData( f , uiElementAttributeSymbol ) != null ).ToList();

            foreach ( var VARIABLE in elementFields )
            {
                //
            }

            var uxmlTraitFields = fieldsList.Where( f => GetUxmlTraitAttributeData( f , uxmlTraitAttributeSymbol ) != null ).ToList();
            var source = new StringBuilder( $@"// <auto-generated/>

using UnityEngine.UIElements;
namespace {classSymbol.ContainingNamespace}
{{
public partial class {classSymbol.Name} 
{{
    public new class UxmlFactory : UxmlFactory<{classSymbol.Name}, UxmlTraits> {{ }}
    public new class UxmlTraits : VisualElement.UxmlTraits
    {{
" );

            // throw new NotImplementedException( $"elements: {elementFields.Count} uxmlTraits: {uxmlTraitFields.Count}" );
            foreach ( ISymbol fieldSymbol in uxmlTraitFields )
            {
                source.AppendLine( GetAttributeDescription( fieldSymbol , uxmlTraitAttributeSymbol ) );
            }

            source.Append( $@"
        public override void Init(VisualElement ve , IUxmlAttributes bag , CreationContext cc )
        {{
            base.Init( ve , bag , cc );
            var self = ({classSymbol.Name}) ve;

" );

            foreach ( ISymbol fieldSymbol in uxmlTraitFields )
            {
                source.AppendLine( GetAttributeInitialization( fieldSymbol , uxmlTraitAttributeSymbol ) );
            }

            source.Append( $@"        }}
    }}
    public void QueryComponents()
    {{
" );

            foreach ( ISymbol fieldSymbol in elementFields )
            {
                // source.AppendLine( $"        {fieldSymbol.Name} = this.Q<{GetQualifyingTypeNameFromSymbol( fieldSymbol )}>(\"hi\");" );

                source.AppendLine( $"         {fieldSymbol.Name} = this.Q<{GetQualifyingTypeNameFromSymbol( fieldSymbol )}>(\"{GetUiElementAttributeData( fieldSymbol , uiElementAttributeSymbol )?.Name}\");" );
            }

            source.Append( $@"    }}
}}
}}
" );


            return source.ToString();
        }

        private string GetTypeNameFromSymbol( ISymbol           symbol ) => GetTypeName( GetTypeFromSymbol( symbol ) );
        private string GetQualifyingTypeNameFromSymbol( ISymbol symbol ) => GetQualifyingTypeName( GetTypeFromSymbol( symbol ) );

        private static UiElementAttributeData? GetUiElementAttributeData( ISymbol fieldSymbol , INamedTypeSymbol uiElementAttributeSymbol )
        {
            var attr = GetSingleAttributeData( fieldSymbol , uiElementAttributeSymbol );
            if ( attr == null )
                return null;

            var args = attr.ConstructorArguments.ToList();
            if ( args.Count != 1 )
            {
                throw new NotImplementedException( $"Attribute did not have enough parameters: expected 1 got {args.Count} {attr}: args: {args}" );
            }

            var name = args[0].Value as string;
            return new UiElementAttributeData()
            {
                Name = name ,
            };
        }

        private static UxmlTraitAttributeData? GetUxmlTraitAttributeData( ISymbol fieldSymbol , INamedTypeSymbol uxmlTraitAttributeSymbol )
        {
            AttributeData attr = GetSingleAttributeData( fieldSymbol , uxmlTraitAttributeSymbol );
            if ( attr == null )
                return null;

            var args = attr.ConstructorArguments.ToList();
            if ( args.Count != 2 )
            {
                throw new NotImplementedException( $"Attribute did not have enough parameters: expected 2 got {args.Count} {attr}: args: {args}" );
            }

            var name = args[0].Value as string;
            var defaultValue = args[1].Value;
            if ( defaultValue != null )
            {
                defaultValue = defaultValue.ToString();
                if ( (string) defaultValue == "False" )
                    defaultValue = "false";

                if ( (string) defaultValue == "True" )
                    defaultValue = "true";
            }

            var type = GetTypeFromSymbol( fieldSymbol );

            return new UxmlTraitAttributeData()
            {
                Name = name , Type = type , defaultValue = defaultValue
            };
        }

        private static AttributeData GetSingleAttributeData( ISymbol fieldSymbol , INamedTypeSymbol attributeSymbol )
        {
            var attr = fieldSymbol.GetAttributes()
                                  .SingleOrDefault( ad =>
                                                        ad?.AttributeClass?.Equals( attributeSymbol , SymbolEqualityComparer.Default ) ?? false );

            return attr;
        }


        private static ITypeSymbol GetTypeFromSymbol( ISymbol symbol )
        {
            switch ( symbol )
            {
                case IFieldSymbol fieldSymbol:
                    return fieldSymbol.Type;

                case IPropertySymbol propertySymbol:
                    return propertySymbol.Type;

                default:
                    throw new InvalidCastException( $"symbol was not property or field: {symbol}" );
            }
        }

        struct UxmlTraitAttributeData
        {
            public string Name;
            public ITypeSymbol Type;
            public object defaultValue;
        }

        struct UiElementAttributeData
        {
            public string Name;
        }

        private string GetAttributeDescription( ISymbol fieldSymbol , INamedTypeSymbol attributeSymbol )
        {
            // private UxmlIntAttributeDescription m_PlayerHealth = new() { name = "player-health" , defaultValue = 0 };
            var name = GetAttributeDescriptionName( fieldSymbol.Name );
            var attr = GetUxmlTraitAttributeData( fieldSymbol , attributeSymbol ).Value;
            var type = ConvertTypeToUxmlAttributeDescriptionType( attr.Type );

            var attributeName = attr.Name;
            var defaultValue = attr.defaultValue;
            return $"        private {type} {name} = new() {{ name = \"{attributeName}\" , defaultValue = {defaultValue} }};";
        }

        private string ConvertTypeToUxmlAttributeDescriptionType( ITypeSymbol type )
        {
            String typeString;
            string typeName = GetTypeName( type );
            switch ( typeName )
            {
                case "int":
                    typeString = "Int";
                    break;

                case "bool":
                    typeString = "Bool";
                    break;

                case "Color":
                    typeString = "Color";
                    break;

                case "string":
                    typeString = "String";
                    break;

                default:
                    Debug.WriteLine( $"Could not get type-name for type: {type.Name}" );
                    typeString = type.Name;
                    break;
            }

            return $"Uxml{typeString}AttributeDescription";
        }

        private static string GetTypeName( ITypeSymbol type ) { return type.ToDisplayString( SymbolDisplayFormat.MinimallyQualifiedFormat ); }

        private static string GetQualifyingTypeName( ITypeSymbol type ) { return type.ToDisplayString( SymbolDisplayFormat.FullyQualifiedFormat ); }

        private string GetAttributeInitialization( ISymbol symbol , ISymbol attributeSymbol )
        {
            // self.PlayerHealth = m_PlayerHealth.GetValueFromBag( bag , cc );
            return $"            self.{symbol.Name} = {GetAttributeDescriptionName( symbol.Name )}.GetValueFromBag( bag , cc );";
        }

        private string GetAttributeDescriptionName( string name ) => $"m_{name}";

    #endregion

    }

    public class SyntaxReceiver : ISyntaxContextReceiver
    {

        public List<ISymbol> Fields { get; } = new List<ISymbol>();

    #region Implementation of ISyntaxContextReceiver

        public void OnVisitSyntaxNode( GeneratorSyntaxContext context )
        {
            if ( context.Node is FieldDeclarationSyntax fieldDeclarationSyntax && fieldDeclarationSyntax.AttributeLists.Count > 0 )
            {
                foreach ( VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables )
                {
                    ISymbol symbol = context.SemanticModel.GetDeclaredSymbol( variable ) as IFieldSymbol;

                    if ( Helpers.IsDerivedFrom( symbol?.ContainingType.BaseType , "AtVisualElement" )
                      && symbol.GetAttributes()
                               .Any( ad => ad.AttributeClass?.ToDisplayString() == Helpers.UxmlTraitAttribute
                                        || ad.AttributeClass?.ToDisplayString() == Helpers.UiElementAttribute ) )
                    {
                        Fields.Add( symbol );
                    }
                }
            }

            if ( context.Node is PropertyDeclarationSyntax propertyDeclarationSyntax && propertyDeclarationSyntax.AttributeLists.Count > 0 )
            {
                ISymbol symbol = context.SemanticModel.GetDeclaredSymbol( propertyDeclarationSyntax ) as IPropertySymbol;

                if ( Helpers.IsDerivedFrom( symbol?.ContainingType.BaseType , "AtVisualElement" )
                  && symbol.GetAttributes()
                           .Any( ad => ad.AttributeClass?.ToDisplayString() == Helpers.UxmlTraitAttribute
                                    || ad.AttributeClass?.ToDisplayString() == Helpers.UiElementAttribute ) )
                {
                    Fields.Add( symbol );
                }
            }
        }

    #endregion

    }
}