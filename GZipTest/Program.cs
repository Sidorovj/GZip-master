using System;
using System.IO;

namespace GZipTest
{
    interface IZipper
    {
        /// <summary>
        /// Архивирует заданный файл
        /// </summary>
        /// <param name="sFile">Путь до файл</param>
        /// <param name="dFile">Путь, куда сохранить файл</param>
        void Compress(string sFile, string dFile);
        /// <summary>
        /// Разархивирует файл
        /// </summary>
        /// <param name="sFile">Путь до файл</param>
        /// <param name="dFile">Путь, куда сохранить файл</param>
        void Decompress(string sFile, string dFile);
        /// <summary>
        /// Получает результат выполнения команды
        /// </summary>
        /// <returns>true, если успех</returns>
        bool GetCommandResult();
        /// <summary>
        /// Прерывает выполнение операции
        /// </summary>
        void Cancel();
    }

    partial class Program
    {
        /// <summary>
        /// Поставить false, если боевой режим
        /// </summary>
        public const bool isDebugMode = true;

        static IZipper zipper;

        /// <returns>0, если выполнение завершилось успешно</returns>
        static int Main(string[] args)
        {
            zipper = new Zipper();
            Console.CancelKeyPress += new ConsoleCancelEventHandler(SoftExit);
            if (isDebugMode)
            {
                if (1 == RunTasksIfDebug())
                    return 1;
            }

            try
            {
                if (args.Length == 0)
                {
                    ShowHelp();
                    //if (isDebugMode)
                    //{
                    //    //args = new string[] { "compress", "C:/temp/temp.dat", "C:/temp/tem.gz" };
                    //    //args = new string[] { "decompress", "C:/temp/tem.gz.gz", "C:/temp/tem.dat" };
                    //    //File.Delete("C:/temp/tem.gz");
                    //}
                    //else
                    args = Console.ReadLine().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                }
                int result = Execute(args);
                GC.Collect();
                Console.WriteLine("\r\nPress any key to quit");
                Console.ReadKey();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error: {1}\r\nStackTrace: {0}", ex.StackTrace, ex.Message);
            }
            Console.WriteLine("\r\nPress any key to quit");
            Console.ReadKey();
            return 1;
        }

        /// <summary>
        /// Подготовительные действия, при отладке
        /// </summary>
        static private int RunTasksIfDebug()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[DEBUG MODE] hint: turn if off in Program class [isDebugMode] ");
            Console.ForegroundColor = ConsoleColor.Gray;
            var errs = UnitTests.RunTests();
            if (errs.Count > 0)
            {
                Console.WriteLine("     An error occured while running Unit tests: ");
                foreach (var err in errs)
                    Console.WriteLine("{0}\r\n", err);
                Console.ReadKey();
                return 1;
            }
            Console.Clear();
            Console.WriteLine("[DEBUG MODE] hint: turn if off in Program class [isDebugMode] ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\r\n [All tests are ok]");
            Console.ForegroundColor = ConsoleColor.Gray;
            return 0;
        }

        /// <summary>
        /// Выполнить проверку и запустить архиватор
        /// </summary>
        /// <returns>0, если выполнение завершилось успешно</returns>
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
            return zipper.GetCommandResult() == true ? 0 : 1;
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
        static void SoftExit(object sender, ConsoleCancelEventArgs _args)
        {
            Console.WriteLine("\nStopping...");
            zipper.Cancel();
            _args.Cancel = true;
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
