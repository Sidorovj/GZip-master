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
            bool _success = false;
            var _fs = File.Create("tmp.dat");
            _fs.Close();
            File.WriteAllText("tmp.dat", "hi");
            if (File.Exists("tmpZipped.gz"))
                File.Delete("tmpZipped.gz");
            if (File.Exists("tmpUnzipped.dat"))
                File.Delete("tmpUnzipped.dat");

            IZipper zipper = new Compressor("tmp.dat", "tmpZipped");
            var taskCompress = Task.Factory.StartNew(() => _success=zipper.Execute());
            Task.WaitAll(taskCompress);
            Assert.AreEqual(_success, true);
                

            zipper = new Decompressor("tmpZipped.gz", "tmpUnzipped.dat");
            var taskDecompress = Task.Factory.StartNew(() => _success=zipper.Execute());
            Task.WaitAll(taskDecompress);
            Assert.AreEqual(_success, true);

            if (!File.Exists("tmp.dat") || !File.Exists("tmpZipped.gz") || !File.Exists("tmpUnzipped.dat"))
                throw new Exception("Error while zipping or unzipping (file does not exists)");

            File.Delete("tmp.dat");
            File.Delete("tmpZipped.gz");
            File.Delete("tmpUnzipped.dat");
        }

        //[TestMethod]
        //public void TestProgressBar()
        //{
        //    ConsoleProgressBar.DrawTextProgressBar(0, 0);
        //    ConsoleProgressBar.DrawTextProgressBar(-10, -10);
        //    ConsoleProgressBar.DrawTextProgressBar(10, 10);
        //    try
        //    {
        //        ConsoleProgressBar.DrawTextProgressBar(10, 1);
        //    }
        //    catch (Exception ex)
        //    {
        //        if (!ex.Message.StartsWith("Значение должно быть больше или равно нулю и меньше"))
        //            throw;
        //    }
        //    ConsoleProgressBar.DrawTextProgressBar(1, 10);
        //}

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
