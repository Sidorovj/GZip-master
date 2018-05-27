using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{

    /// <summary>
    /// Режим работы архиватора
    /// </summary>
    enum ZipMode
    {
        Hold,
        Compressing,
        Decompressing
    }

    /// <summary>
    /// Архиватор
    /// </summary>
    partial class Zipper : IZipper
    {
        #region Fields and properties

        /// <summary>
        /// Сколько запущено потоков
        /// </summary>
        private int threadsLaunchCount = 0;
        ZipMode zipMode = ZipMode.Hold;
        bool _cancelled = false;
        bool _success = false;
        string sourceFile;
        string destinationFile;
        static int _threadsCount { get { return Environment.ProcessorCount; } }

        const int blockSize = 1024 * 1024;
        int counterForDecompress = 0;
        QueueManager _queueReader = new QueueManager();
        QueueManager _queueWriter = new QueueManager();
        ManualResetEvent[] doneEvents = new ManualResetEvent[_threadsCount];

        #endregion

        #region Functions:public

        public void Compress(string sourceFile, string destinationFile)
        {
            zipMode = ZipMode.Compressing;
            this.sourceFile = sourceFile;
            this.destinationFile = destinationFile;
            Launch();
        }
        public void Decompress(string sourceFile, string destinationFile)
        {
            zipMode = ZipMode.Decompressing;
            this.sourceFile = sourceFile;
            this.destinationFile = destinationFile;
            Launch();
        }

        public bool GetCommandResult()
        {
            return !_cancelled && _success;
        }

        public void Cancel()
        {
            _cancelled = true;
        }

        #endregion

        #region Functions:private

        /// <summary>
        /// Запуск команды
        /// </summary>
        private void Launch()
        {
            if (zipMode == ZipMode.Hold)
                throw new Exception("Please select the command to execute");
            Console.WriteLine("{0}...\n", zipMode);

            Thread _reader = new Thread(new ThreadStart(ReadFromFile));
            Thread[] workers = new Thread[_threadsCount];

            _reader.Start();

            for (int i = 0; i < _threadsCount; i++)
            {
                doneEvents[i] = new ManualResetEvent(false);
                if (zipMode == ZipMode.Compressing)
                {
                    //ThreadPool.QueueUserWorkItem(CompressExecute, i);
                    workers[i] = new Thread(CompressExecute);
                }
                else
                {
                    //ThreadPool.QueueUserWorkItem(DecompressExecute, i);
                    workers[i] = new Thread(DecompressExecute);
                }
                workers[i].Start(i);

            }

            Thread _writer = new Thread(new ThreadStart(WriteToFile));
            _writer.Start();

            WaitHandle.WaitAll(doneEvents);
            _queueWriter.Stop();

            GC.Collect();
            if (!_cancelled)
            {
                Console.WriteLine("\nOperation succesfull");
                _success = true;
            }
        }

        /// <summary>
        /// Выполнение сжатия
        /// </summary>
        /// <param name="i"></param>
        private void CompressExecute(object i)
        {
            if(Program.isDebugMode)
                Console.WriteLine("Thrd{1} beg - Thr count = {0}", ++threadsLaunchCount,i);
            try
            {
                while (true && !_cancelled)
                {
                    ByteBlock _block = _queueReader.Dequeue();

                    if (_block == null)
                    {
                        return;
                    }

                    using (MemoryStream _memoryStream = new MemoryStream())
                    {
                        using (GZipStream cs = new GZipStream(_memoryStream, CompressionMode.Compress))
                        {
                            cs.Write(_block.Buffer, 0, _block.Buffer.Length);
                        }

                        byte[] compressedData = _memoryStream.ToArray();
                        ByteBlock _out = new ByteBlock(_block.Id, compressedData);
                        _queueWriter.EnqueueForWriting(_out);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in thread number {0}. \r\n Error description: {1}\r\nStackTrace: {2}", i, ex.Message, ex.StackTrace);
                _cancelled = true;
            }
            finally
            {
                ManualResetEvent doneEvent = doneEvents[(int)i];
                doneEvent.Set();
                if (Program.isDebugMode)
                Console.WriteLine("Thr{1} end - Thr count = {0}", --threadsLaunchCount, i);
            }

        }

        /// <summary>
        /// Выполнение разарихивирования
        /// </summary>
        /// <param name="i"></param>
        private void DecompressExecute(object i)
        {
            if (Program.isDebugMode)
                Console.WriteLine("Thrd{1} beg - Thr count = {0}", ++threadsLaunchCount, i);
                try
            {
                while (true && !_cancelled)
                {
                    ByteBlock _block = _queueReader.Dequeue();
                    if (_block == null)
                        return;

                    using (MemoryStream ms = new MemoryStream(_block.CompressedBuffer))
                    {
                        using (GZipStream _gz = new GZipStream(ms, CompressionMode.Decompress))
                        {
                            _gz.Read(_block.Buffer, 0, _block.Buffer.Length);
                            byte[] decompressedData = _block.Buffer.ToArray();
                            ByteBlock block = new ByteBlock(_block.Id, decompressedData);
                            _queueWriter.EnqueueForWriting(block);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in thread number {0}. \r\n Error description: {1}\r\nStackTrace: {2}", i, ex.Message, ex.StackTrace);
                _cancelled = true;
            }
            finally
            {
                ManualResetEvent doneEvent = doneEvents[(int)i];
                doneEvent.Set();
                if (Program.isDebugMode)
                    Console.WriteLine("Thr{1} end - Thr count = {0}", --threadsLaunchCount, i);
            }
        }


        /// <summary>
        /// Считывание файла
        /// </summary>
        private void ReadFromFile()
        {
            if (Program.isDebugMode)
                Console.WriteLine("Read  beg - Thr count = {0}", ++threadsLaunchCount);
            try
            {
                if (zipMode == ZipMode.Compressing)
                    using (FileStream _fileToBeCompressed = new FileStream(sourceFile, FileMode.Open))
                    {
                        int bytesCount;
                        byte[] lastBuffer;

                        while (_fileToBeCompressed.Position < _fileToBeCompressed.Length && !_cancelled)
                        {
                            if (_fileToBeCompressed.Length - _fileToBeCompressed.Position <= blockSize)
                            {
                                bytesCount = (int)(_fileToBeCompressed.Length - _fileToBeCompressed.Position);
                            }
                            else
                            {
                                bytesCount = blockSize;
                            }

                            lastBuffer = new byte[bytesCount];
                            _fileToBeCompressed.Read(lastBuffer, 0, bytesCount);
                            _queueReader.EnqueueForCompressing(lastBuffer);
                            ConsoleProgressBar.DrawTextProgressBar(_fileToBeCompressed.Position, _fileToBeCompressed.Length);
                        }
                    }
                else
                    using (FileStream _compressedFile = new FileStream(sourceFile, FileMode.Open))
                    {
                        while (_compressedFile.Position < _compressedFile.Length && !_cancelled)
                        {
                            byte[] lengthBuffer = new byte[8];
                            _compressedFile.Read(lengthBuffer, 0, lengthBuffer.Length);
                            int blockLength = BitConverter.ToInt32(lengthBuffer, 4);
                            byte[] compressedData = new byte[blockLength];
                            lengthBuffer.CopyTo(compressedData, 0);

                            _compressedFile.Read(compressedData, 8, blockLength - 8);
                            int _dataSize = BitConverter.ToInt32(compressedData, blockLength - 4);
                            byte[] lastBuffer = new byte[_dataSize];

                            ByteBlock _block = new ByteBlock(counterForDecompress, lastBuffer, compressedData);
                            _queueReader.EnqueueForWriting(_block);
                            counterForDecompress++;
                            ConsoleProgressBar.DrawTextProgressBar(_compressedFile.Position, _compressedFile.Length);
                        }
                    }
            }
            catch (Exception ex)
            {
                _cancelled = true;
                throw;
            }
            finally
            {
                _queueReader.Stop();
                if (Program.isDebugMode)
                    Console.WriteLine("Read  end - Thr count = {0}", --threadsLaunchCount);
            }
        }
        
        /// <summary>
        /// Запись в файл 
        /// </summary>
        private void WriteToFile()
        {
            if (Program.isDebugMode)
                Console.WriteLine("Write beg - Thr count = {0}", ++threadsLaunchCount);
            try
            {
                if (ZipMode.Compressing == zipMode)
                    using (FileStream _fileCompressed = new FileStream(destinationFile + ".gz", FileMode.Append))
                    {
                        while (true && !_cancelled)
                        {
                            ByteBlock _block = _queueWriter.Dequeue();
                            if (_block == null)
                            {
                                return;
                            }

                            BitConverter.GetBytes(_block.Buffer.Length).CopyTo(_block.Buffer, 4);
                            _fileCompressed.Write(_block.Buffer, 0, _block.Buffer.Length);
                        }
                    }
                else
                    using (FileStream _decompressedFile = new FileStream(destinationFile, FileMode.Append))
                    {
                        while (true && !_cancelled)
                        {
                            ByteBlock _block = _queueWriter.Dequeue();
                            if (_block == null)
                            {
                                return;
                            }

                            _decompressedFile.Write(_block.Buffer, 0, _block.Buffer.Length);
                        }
                    }
            }
            catch (Exception ex)
            {
                _cancelled = true;
                throw;
            }
            finally
            {
                if (Program.isDebugMode)
                    Console.WriteLine("Write end - Thr count = {0}",--threadsLaunchCount);
            }
        }

        #endregion
    }
}
