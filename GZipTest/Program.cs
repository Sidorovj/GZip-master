using System;
using System.Diagnostics;
using System.IO;

namespace GZipTest
{
    public interface IZipper
    {
        /// <summary>
        /// Выполняет операцию по сжатию/распаковке
        /// </summary>
        /// <returns></returns>
        bool Execute();
        
        /// <summary>
        /// Прерывает выполнение операции
        /// </summary>
        void Cancel();
    }

    partial class Program
    {

        static IZipper zipper;

        /// <returns>0, если выполнение завершилось успешно</returns>
        static int Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(SoftExit);
            int _result = 1;
            try
            {
                if (args.Length == 0)
                {
                    ShowHelp();
                    args = Console.ReadLine().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                }
                _result = Execute(args);
                GC.Collect();
                Debug.WriteLine("Operation ended");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error: {1}\r\nStackTrace: {0}", ex.StackTrace, ex.Message);
            }
            Console.WriteLine("\r\nPress any key to quit");
            Console.ReadKey();
            return _result;
        }
        
        /// <summary>
        /// Выполнить проверку и запустить архиватор
        /// </summary>
        /// <returns>0, если выполнение завершилось успешно</returns>
        static private int Execute(string[] args)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            CheckArgs(args);
            
            switch (args[0].ToLower())
            {
                case "compress":
                    zipper = new Compressor(args[1], args[2]);                 
                    break;
                case "decompress":
                    zipper = new Decompressor(args[1], args[2]);
                    break;
            }
            watch.Stop();
            bool _success = zipper.Execute();
            if (_success)
                Console.WriteLine("\r\nOperation successfully ended in {0:N2} {1}", 
                    watch.ElapsedMilliseconds < 500 ? watch.ElapsedMilliseconds : watch.ElapsedMilliseconds / 1000.0, 
                    watch.ElapsedMilliseconds < 500 ? "ms" : "seconds");
            return _success ? 0 : 1;
        }

        /// <summary>
        /// Показать справочную информацию
        /// </summary>
        static void ShowHelp()
        {
            Console.WriteLine("Help: you can zip or inzip your file using the following commands:\r\n" +
                              "[de]compress [input file path] [output file path]\r\n" +
                              "For example: compress C:/tmp/help.txt C:/tmp/help \r\n" +
                              "If you want to exit while executing, press ctrl+C\r\n");
        }

        /// <summary>
        /// Мягкое прекращение операции
        /// </summary>
        static void SoftExit(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("\nStopping...");
            zipper.Cancel();
            args.Cancel = true;
        }

        /// <summary>
        /// Проверка аргументов по шаблону
        /// </summary>
        /// <param name="args"></param>
        public static void CheckArgs(string[] args)
        {
            if (args == null || args.Length != 3)
            {
                throw new Exception("Wrong parameters count");
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
