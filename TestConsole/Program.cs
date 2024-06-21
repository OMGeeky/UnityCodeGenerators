// See https://aka.ms/new-console-template for more information

using System.Diagnostics;

using TestConsole;

namespace TestConsole
{
    static partial class Program
    {
        static void Main( string[] args )
        {
            var t = new Test1();
            t.Test123();
            HelloFrom( "Generated Code" );
        }

        static partial void HelloFrom( string name );
    }

    [AtUiComponent("Test123")]
    public partial class Test1 : AtVisualElement
    {
         // protected override string UxmlPath =>"";
        [UiElement]
        public AtVisualElement test;
    
        public void Test123()
        {
            Console.WriteLine( "test start" ); 
            QueryElements();
            Console.WriteLine( "test end" ); 
        }
    }

    public abstract class AtVisualElement
    {
        protected AtVisualElement()
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            Console.WriteLine( $"UxmlPath: '{UxmlPath}'" );
        }
        // protected abstract string UxmlPath { get; }
        protected virtual string UxmlPath { get; }

        protected virtual void QueryElements()
        {
            Console.WriteLine( "Nothing overwriting this..." );
        }
    }
}

namespace UnityEngine
{
    namespace UIElements
    {
        static class VisualElementExtensions
        {
            public static AtVisualElement Q<T>( this AtVisualElement self, string name )
            {
                Console.WriteLine( $"Querying for '{name}' inside type {self.GetType().Name}" );
                return new Test1();
            }
        }
    }
}