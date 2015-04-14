using System;

namespace BankBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)    
            {
                PrintUsage(); 
                return;
            }
            for (int x = 0; x < args.Length; x++)
                args[x] = args[x].Replace("\"", string.Empty);
            if (args.Length == 2)
                BankBuilder.BuildBankFile(args[0], args[1]);
            else
                BankBuilder.BuildBankFile(args[0], string.Empty);
            Console.WriteLine("Done.");
        }

        static void PrintUsage()
        {
            Console.WriteLine("Audio Synth Bank Builder Tool");
            Console.WriteLine("CopyRight: Alex Veltsistas");
            Console.WriteLine("----------------------------");
            Console.WriteLine("Usage: BankBuilder.exe InputBankName [OutputBankName]");
            Console.WriteLine("\tInputBankName - File name of text based bank. Ex. \"C:\\bank.txt\"");
            Console.WriteLine("\tOutputBankName - File name given to the output bank. If not provided the same name of the input bank is used Ex. \"C:\\MyBank.bank\"");
        }
    }
}
