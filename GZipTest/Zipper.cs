using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class Zipper : IZipper
    {
        #region Fields and properties
        
        ZipMode zipMode = ZipMode.Hold;
        bool cancelled = false;
        bool success = false;
        string sourceFile;
        string destinationFile;
        int counterForDecompress = 0;
        QueueManager queueReader = new QueueManager();
        QueueManager queueWriter = new QueueManager();
        ManualResetEvent[] doneEvents = new ManualResetEvent[ThreadsCount];
        ManualResetEvent writeEndEvent = new ManualResetEvent(false);
        /// <summary>
        /// Для вычисления прогресс-бара
        /// </summary>
        long totalBlockCount = 0;
        public const int blockSize = 1024 * 1024;

        static int ThreadsCount { get { return Environment.ProcessorCount; } }

        #endregion

        #region Functions:public

        public void Compress(string sourceFile, string destinationFile)
        {
            zipMode = ZipMode.Compressing;
            Launch(sourceFile, destinationFile);
        }
        public void Decompress(string sourceFile, string destinationFile)
        {
            zipMode = ZipMode.Decompressing;
            Launch(sourceFile, destinationFile);
        }

        public bool GetCommandResult()
        {
            return !cancelled && success;
        }

        public void Cancel()
        {
            cancelled = true;
        }

        #endregion

        #region Functions:private

        /// <summary>
        /// Запуск команды
        /// </summary>
        private void Launch(string sourceFile, string destinationFile)
        {
            this.sourceFile = sourceFile;
            this.destinationFile = destinationFile;

            if (zipMode == ZipMode.Hold)
                throw new Exception("Please select the command to execute");
            Console.WriteLine("{0}...", zipMode);

            Thread _reader = new Thread(new ThreadStart(ReadFromFile));
            Thread[] _workers = new Thread[ThreadsCount];

            _reader.Start();

            for (int i = 0; i < ThreadsCount; i++)
            {
                doneEvents[i] = new ManualResetEvent(false);
                if (zipMode == ZipMode.Compressing)
                {
                    //ThreadPool.QueueUserWorkItem(CompressExecute, i);
                    _workers[i] = new Thread(CompressExecute);
                }
                else
                {
                    //ThreadPool.QueueUserWorkItem(DecompressExecute, i);
                    _workers[i] = new Thread(DecompressExecute);
                }
                _workers[i].Start(i);

            }

            Thread _writer = new Thread(new ThreadStart(WriteToFile));
            _writer.Start();

            TimerCallback tmCallback = new TimerCallback(DrawCurrentProgress);
            Timer progressTimer = new Timer(tmCallback, 0, 0, 50);

            WaitHandle.WaitAll(doneEvents);
            queueWriter.Stop();
            WaitHandle.WaitAll(new WaitHandle[] { writeEndEvent });

            progressTimer.Dispose();
            DrawCurrentProgress(null);

            GC.Collect();
            if (!cancelled)
            {
                success = true;
            }
        }

        private void DrawCurrentProgress(object obj)
        {
            if (totalBlockCount > 0)
                ConsoleProgressBar.DrawTextProgressBar(queueReader.blockId + queueWriter.blockId, totalBlockCount * 2);
        }

        /// <summary>
        /// Выполнение сжатия
        /// </summary>
        /// <param name="i"></param>
        private void CompressExecute(object i)
        {
            Debug.WriteLine("Thrd {0} start", i);
            try
            {
                while (true && !cancelled)
                {
                    ByteBlock _block = queueReader.Dequeue();

                    if (_block == null)
                    {
                        return;
                    }

                    using (MemoryStream _memoryStream = new MemoryStream())
                    {
                        using (GZipStream _cs = new GZipStream(_memoryStream, CompressionMode.Compress))
                        {
                            _cs.Write(_block.Buffer, 0, _block.Buffer.Length);
                        }

                        byte[] _compressedData = _memoryStream.ToArray();
                        ByteBlock _out = new ByteBlock(_block.Id, _compressedData);
                        queueWriter.EnqueueForWriting(_out);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in thread number {0}. \r\n Error description: {1}\r\nStackTrace: {2}", i, ex.Message, ex.StackTrace);
                cancelled = true;
            }
            finally
            {
                Debug.WriteLine("Thrd {0} end",  i);
                ManualResetEvent _doneEvent = doneEvents[(int)i];
                _doneEvent.Set();
            }

        }

        /// <summary>
        /// Выполнение разарихивирования
        /// </summary>
        /// <param name="i"></param>
        private void DecompressExecute(object i)
        {
            Debug.WriteLine("Thrd {0} start", i);
            try
            {
                while (true && !cancelled) 
                {
                    ByteBlock _block = queueReader.Dequeue();
                    if (_block == null)
                        return;

                    using (MemoryStream _ms = new MemoryStream(_block.CompressedBuffer))
                    {
                        using (GZipStream _gz = new GZipStream(_ms, CompressionMode.Decompress))
                        {
                            _gz.Read(_block.Buffer, 0, _block.Buffer.Length);
                            byte[] _decompressedData = _block.Buffer.ToArray();
                            ByteBlock _decompressedBlock = new ByteBlock(_block.Id, _decompressedData);
                            queueWriter.EnqueueForWriting(_decompressedBlock);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in thread number {0}. \r\n Error description: {1}\r\nStackTrace: {2}", i, ex.Message, ex.StackTrace);
                cancelled = true;
            }
            finally
            {
                Debug.WriteLine("Thrd {0} end", i);
                ManualResetEvent _doneEvent = doneEvents[(int)i];
                _doneEvent.Set();
            }
        }


        /// <summary>
        /// Считывание файла
        /// </summary>
        private void ReadFromFile()
        {
            Debug.WriteLine("Read start");
            try
            {
                using (FileStream _sourceFile = new FileStream(sourceFile, FileMode.Open))
                {
                    int _bytesCount;
                    byte[] _lastBuffer;
                    totalBlockCount = (long)Math.Ceiling((decimal)(_sourceFile.Length*1.0 / blockSize));
                    ConsoleProgressBar.SetFileSize(_sourceFile.Length);

                    while (_sourceFile.Position < _sourceFile.Length && !cancelled)
                    {
                        if (zipMode == ZipMode.Compressing)
                        {
                            if (_sourceFile.Length - _sourceFile.Position <= blockSize)
                            {
                                _bytesCount = (int)(_sourceFile.Length - _sourceFile.Position);
                            }
                            else
                            {
                                _bytesCount = blockSize;
                            }
                            _lastBuffer = new byte[_bytesCount];
                            _sourceFile.Read(_lastBuffer, 0, _bytesCount);
                            queueReader.EnqueueForCompressing(_lastBuffer);
                        }
                        else
                        {
                            byte[] _lengthBuffer = new byte[8];
                            _sourceFile.Read(_lengthBuffer, 0, _lengthBuffer.Length);
                            int _blockLength = BitConverter.ToInt32(_lengthBuffer, 4);
                            byte[] _compressedData = new byte[_blockLength];
                            _lengthBuffer.CopyTo(_compressedData, 0);

                            _sourceFile.Read(_compressedData, 8, _blockLength - 8);
                            int _dataSize = BitConverter.ToInt32(_compressedData, _blockLength - 4);
                            _lastBuffer = new byte[_dataSize];

                            ByteBlock _block = new ByteBlock(counterForDecompress, _lastBuffer, _compressedData);
                            queueReader.EnqueueForWriting(_block);
                            counterForDecompress++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                cancelled = true;
                throw;
            }
            finally
            {
                queueReader.Stop();
                Debug.WriteLine("Read end");
            }
        }
        
        /// <summary>
        /// Запись в файл 
        /// </summary>
        private void WriteToFile()
        {
            Debug.WriteLine("Write begin");
            try
            {
                using (FileStream _destFile = new FileStream(destinationFile + (zipMode == ZipMode.Compressing ? ".gz" : ""), FileMode.Append))
                {
                    while (true && !cancelled)
                    {
                        ByteBlock _block = queueWriter.Dequeue();
                        if (_block == null)
                        {
                            return;
                        }
                        if (zipMode == ZipMode.Compressing)
                            BitConverter.GetBytes(_block.Buffer.Length).CopyTo(_block.Buffer, 4);
                        _destFile.Write(_block.Buffer, 0, _block.Buffer.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                cancelled = true;
                throw;
            }
            finally
            {
                Debug.WriteLine("Write end");
                writeEndEvent.Set();
            }
        }

        #endregion
    }
}
