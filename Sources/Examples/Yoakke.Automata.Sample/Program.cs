using System;
using Yoakke.Automata.Discrete;

namespace Yoakke.Automata.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var dfa = new Dfa<char, char>();
            dfa.AcceptingStates.Add('q');
            dfa.InitialState = 'q';
            dfa.AddTransition('q', 'a', 'q');
            dfa.Complete("ab", 't');
            Console.WriteLine(dfa.ToDot());
        }
    }
}
