using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest
{

    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
    static class UnitTests
    {
        private static List<string> errors;
        public static List<string> RunTests()
        {
            errors = new List<string>();
            TestProgressBar();
            TestByteBlock();
            TestQueueManager();
            AddMessageToList(new Zipper().RunTests());
            AddMessageToList(Program.RunTests());
            return errors;
        }

        private static void AddMessageToList(Exception ex)
        {
            if (ex!= null)
                errors.Add(string.Format("Method: {0}, error: {1}", ex.TargetSite, ex.Message));
        }

        private static void TestProgressBar()
        {
            try
            {
                ConsoleProgressBar.DrawTextProgressBar(0, 0);
                ConsoleProgressBar.DrawTextProgressBar(-10,-10);
                ConsoleProgressBar.DrawTextProgressBar(10, 10);
                try {
                    ConsoleProgressBar.DrawTextProgressBar(10, 1);
                }
                catch (Exception ex)
                {
                    if (!ex.Message.StartsWith("Значение должно быть больше или равно нулю и меньше"))
                        throw;
                }
                ConsoleProgressBar.DrawTextProgressBar(1, 10);
            }
            catch (Exception ex)
            {
                AddMessageToList(ex);
            }
        }

        private static void TestByteBlock()
        {
            try
            {
                ByteBlock bl = new ByteBlock(0, null);
                bl = new ByteBlock(0, null, null);
            }
            catch (Exception ex)
            {
                AddMessageToList(ex);
            }
        }
        private static void TestQueueManager()
        {
            try
            {
                QueueManager qm = new QueueManager();
                qm.Stop();
                var res = qm.Dequeue();
                if (res != null)
                    throw new ArgumentException("Unexpected result from QM.Dequeue");
            }
            catch (Exception ex)
            {
                AddMessageToList(ex);
            }
        }
        
    }

    partial class Program
    {
        /// <summary>
        /// Unit tests
        /// </summary>
        [TestMethod]
        public static Exception RunTests()
        {
            try
            {
                try
                {
                    CheckArgs(null);
                }
                catch (Exception ex)
                {
                    if (ex.Message != "Wrong parameters count")
                        throw;
                }
                try
                {
                    CheckArgs(new string[] {"dada","dada","dada" });
                }
                catch (Exception ex)
                {
                    if (ex.Message != "First argument must be \"compress\" or \"decompress\"")
                        throw;
                }
                try
                {
                    CheckArgs(new string[] { "compress", "superPuperFileDoesNotExist", "dada" });
                }
                catch (Exception ex)
                {
                    if (ex.Message != "Source file was not found")
                        throw;
                }
                var fs = File.Create("tmp.dat");
                fs.Close();
                File.WriteAllText("tmp.dat", "hi");
                if (File.Exists("tmpZipped.gz"))
                    File.Delete("tmpZipped.gz");
                if (File.Exists("tmpUnzipped.dat"))
                    File.Delete("tmpUnzipped.dat");
                Execute(new string[] { "compress", "tmp.dat", "tmpZipped" });
                zipper = new Zipper();
                Execute(new string[] { "decompress", "tmpZipped.gz", "tmpUnzipped.dat" });
                if (!File.Exists("tmp.dat") || !File.Exists("tmpZipped.gz") || !File.Exists("tmpUnzipped.dat"))
                    throw new Exception("Error while zipping or unzipping (file does not exists)");
                File.Delete("tmp.dat");
                File.Delete("tmpZipped.gz");
                File.Delete("tmpUnzipped.dat");
                zipper = new Zipper();
            }
            catch (Exception ex)
            {
                return ex;
            }
            return null;
        }
    }

    partial class Zipper
    {
        /// <summary>
        /// Unit tests
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public Exception RunTests()
        {
            try
            {

            }
            catch (Exception ex)
            {
                return ex;
            }
            return null;
        }
    }
}
