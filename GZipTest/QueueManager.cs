using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;


namespace GZipTest
{
    /// <summary>
    /// Массив байтов для компрессии/распаковки
    /// </summary>
    public class ByteBlock
    {
        public int Id { get; private set; }
        public byte[] Buffer { get; private set; }
        public byte[] CompressedBuffer { get; private set; }


        public ByteBlock(int id, byte[] buffer) : this(id, buffer, new byte[0])
        {

        }

        public ByteBlock(int id, byte[] buffer, byte[] compressedBuffer)
        {
            this.Id = id;
            this.Buffer = buffer;
            this.CompressedBuffer = compressedBuffer;
        }

    }

    /// <summary>
    /// Очередь для параллельной работы с массивами байтов
    /// </summary>
    public class QueueManager
    {
        object locker = new object();
        Queue<ByteBlock> queue = new Queue<ByteBlock>();
        bool isDead = false;
        static int maxQueueLength = 0;

        // Доступ вовне нужен для того, чтобы увидеть прогресс
        public int blockId { get; private set; } = 0;
        /// <summary>
        /// Ограничение макс. длины очереди. Поставим значение, исходя из доступных 200 МБ оперативки (100 для буфера и 100 для обработанного буфера)
        /// </summary>
        static int MaxQueueLength
        {
            get
            {
                if (maxQueueLength == 0)
                {
                    int _size = 100 * 1024 * 1024 / Zipper.blockSize;
                    maxQueueLength = _size <= 1 ? 2 : _size;
                }
                return maxQueueLength;
            }
        }

        /// <summary>
        /// Добавить элемент в очередь на запись в файл
        /// </summary>
        /// <param name="block"></param>
        public void EnqueueForWriting(ByteBlock block)
        {
            int _id = block.Id;
            lock (locker)
            {
                if (isDead)
                    throw new InvalidOperationException("Queue already stopped");

                while (_id != blockId)
                {
                    Monitor.Wait(locker);
                }

                AddBlockToQueue(block);
            }
        }

        /// <summary>
        /// Добавить элемент в очередь на сжатие
        /// </summary>
        /// <param name="buffer"></param>
        public void EnqueueForCompressing(byte[] buffer)
        {
            lock (locker)
            {
                if (isDead)
                    throw new InvalidOperationException("Queue already stopped");

                ByteBlock _block = new ByteBlock(blockId, buffer);
                AddBlockToQueue(_block);
            }
        }
        
        /// <returns>Первый элемент очереди</returns>
        public ByteBlock Dequeue()
        {
            lock (locker)
            {
                if (queue.Count == 0)
                    while (queue.Count == 0 && !isDead)
                        Monitor.Wait(locker);

                Monitor.PulseAll(locker);
                if (queue.Count == 0)
                    return null;
                return queue.Dequeue();

            }
        }

        /// <summary>
        /// Остановка очереди
        /// </summary>
        public void Stop()
        {
            lock (locker)
            {
                isDead = true;
                Monitor.PulseAll(locker);
            }
        }

        /// <summary>
        /// Добавляет элемент в очередь, если ее длина меньше допустимой
        /// </summary>
        /// <param name="block">Блок для постановки в очередь</param>
        private void AddBlockToQueue(ByteBlock block)
        {
            while (queue.Count >= MaxQueueLength)
            {
                Monitor.Wait(locker);
            }
            queue.Enqueue(block);
            blockId++;
            Monitor.PulseAll(locker);
        }
    }
}