using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GZipTest
{
    interface IZipper
    {
        int Compress(string sFile, string dFile);
        int Decompress(string sFile, string dFile);
        bool GetCommandResult();
        void Cancel();
    }

    class Program
    {
        static IZipper zipper;
        static int Main(string[] args)
        {
            zipper = new Zipper();
            Console.CancelKeyPress += new ConsoleCancelEventHandler(SoftExit);

            try
            {
                if (args.Length == 0)
                {
                    ShowHelp();
                    //args = new string[] {"compress","C:/temp/temp.dat","C:/temp/tem" };
                    //File.Delete("C:/temp/tem.gz");

                    args = Console.ReadLine().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                }
                int result = Execute(args);
                Console.WriteLine("Press key to quit");
                Console.ReadKey();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error: {1}\r\nStackTrace: {0}", ex.StackTrace, ex.Message);
            }
            return 1;
        }

        static private int Execute(string[] args)
        {
            CheckArgs(args);            
            
            switch (args[0].ToLower())
            {
                case "compress":
                    zipper.Compress(args[1], args[2]);
                    break;
                case "decompress":
                    zipper.Decompress(args[1], args[2]);
                    break;
            }
            return zipper.GetCommandResult() == true ? 0:1 ;
        }

        static void ShowHelp()
        {
            Console.WriteLine("Help: you can zip or inzip your file using the following commands:\r\n" +
                              "[de]compress [input file path] [output file path]\r\n" +
                              "For example: compress C:/tmp/help.txt C:/tmp/help \r\n" +
                              "If you want to exit while executing, press ctrl+C\r\n");
        }


        static void SoftExit(object sender, ConsoleCancelEventArgs _args)
        {
            Console.WriteLine("\nStopping...");
            zipper.Cancel();
            _args.Cancel = true;
        }

        public static void CheckArgs(string[] args)
        {

            if (args.Length != 3)
            {
                throw new Exception("Wrong parameters count\r\n");
            }

            if (args[0].ToLower() != "compress" && args[0].ToLower() != "decompress")
            {
                throw new Exception("First argument must be \"compress\" or \"decompress\"");
            }

            if (!File.Exists(args[1]))
            {
                throw new Exception("Source file was not found");
            }            
        }
    }
}
