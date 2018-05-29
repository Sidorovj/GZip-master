using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GZipTest;
using System.IO;
using System.Threading.Tasks;

namespace GZipUnitTests
{
    [TestClass]
    public class UnitTests
    {
        [TestMethod]
        public void TestZipper()
        {
            IZipper zipper = new Zipper();

            var fs = File.Create("tmp.dat");
            fs.Close();
            File.WriteAllText("tmp.dat", "hi");
            if (File.Exists("tmpZipped.gz"))
                File.Delete("tmpZipped.gz");
            if (File.Exists("tmpUnzipped.dat"))
                File.Delete("tmpUnzipped.dat");

            var taskCompress = Task.Factory.StartNew(() => zipper.Compress("tmp.dat", "tmpZipped"));
            Task.WaitAll(taskCompress);
            if (!zipper.GetCommandResult())
                throw new Exception("Не удалось архивировать файл");

            zipper = new Zipper();
            var taskDecompress = Task.Factory.StartNew(() => zipper.Decompress("tmpZipped.gz", "tmpUnzipped.dat"));
            Task.WaitAll(taskDecompress);
            if (!zipper.GetCommandResult())
                throw new Exception("Не удалось разархивировать файл");

            if (!File.Exists("tmp.dat") || !File.Exists("tmpZipped.gz") || !File.Exists("tmpUnzipped.dat"))
                throw new Exception("Error while zipping or unzipping (file does not exists)");

            File.Delete("tmp.dat");
            File.Delete("tmpZipped.gz");
            File.Delete("tmpUnzipped.dat");
        }

        [TestMethod]
        public void TestProgressBar()
        {
            ConsoleProgressBar.DrawTextProgressBar(0, 0);
            ConsoleProgressBar.DrawTextProgressBar(-10, -10);
            ConsoleProgressBar.DrawTextProgressBar(10, 10);
            try
            {
                ConsoleProgressBar.DrawTextProgressBar(10, 1);
            }
            catch (Exception ex)
            {
                if (!ex.Message.StartsWith("Значение должно быть больше или равно нулю и меньше"))
                    throw;
            }
            ConsoleProgressBar.DrawTextProgressBar(1, 10);
        }

        [TestMethod]
        public void TestByteBlock()
        {
            ByteBlock bl = new ByteBlock(0, null);
            bl = new ByteBlock(0, null, null);

        }

        [TestMethod]
        public void TestQueueManager()
        {
            QueueManager qm = new QueueManager();
            qm.Stop();
            var res = qm.Dequeue();

            Assert.AreEqual(res, null);
        }
    }
}
