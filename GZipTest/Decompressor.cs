using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace GZipTest
{
    public class Decompressor : Zipper
    {
        public Decompressor(string sourceFile, string destinationFile) : base(sourceFile, destinationFile)
        {
            zipMode = ZipMode.Decompressing;
        }
        public override bool Execute()
        {
            return ExecuteOperation(DecompressExecute);
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
        protected override void ReadFromFile()
        {
            Debug.WriteLine("Read start");
            try
            {
                using (FileStream _sourceFile = new FileStream(sourceFile, FileMode.Open))
                {
                    byte[] _lastBuffer;
                    totalBlockCount = (long)Math.Ceiling((decimal)(_sourceFile.Length * 1.0 / blockSize));
                    ConsoleProgressBar.SetFileSize(_sourceFile.Length);

                    while (_sourceFile.Position < _sourceFile.Length && !cancelled)
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
    }
}