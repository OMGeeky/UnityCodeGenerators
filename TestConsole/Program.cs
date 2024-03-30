// See https://aka.ms/new-console-template for more information

using System.Diagnostics;


namespace ConsoleApp;

partial class Program
{
    static void Main( string[] args ) { HelloFrom( "Generated Code" ); }

    static partial void HelloFrom( string name );
}

public partial class Test1 : AtVisualElement
{
    // [UxmlTrait( "health" , 9 )]
    // public int MyProperty { get; set; }

    // [UxmlTrait( "health2" , 8)] public int MyField;
    // [UxmlTrait( "health1" , 8)] public int MyField2{get; set; }
    // [UxmlTrait( "health1" , false)] public bool MyBoolField2;
    // [UxmlTrait( "health1" , "hi")] public string MyStringField2;
    public void Test123()
    {
        Debug.Write( "test" );
        // Test987();
    }
}

public abstract class AtVisualElement { }
