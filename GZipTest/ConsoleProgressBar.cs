using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest
{
    /// <summary>
    /// Показывает прогресс-бар в консоли
    /// <see cref="https://gist.github.com/gabehesse/975472"/>
    /// </summary>
    public static class ConsoleProgressBar
    {
        static string sizeDimension;
        static long size;
        static long oldTotal;

        /// <summary>
        /// Отрисовка прогресс-бара
        /// </summary>
        /// <param name="progress">Текущее состояние</param>
        /// <param name="total">Конечное состояние</param>
        public static void DrawTextProgressBar(long progress, long total)
        {
            if (progress <0 ||total<=0)
                return;
            if (oldTotal != total)
            {
                sizeDimension = "B";
                size = total;
                if (total > 1024 * 1024 * 1024)
                {
                    sizeDimension = "GB";
                    size = total / 1024 / 1024 / 1024;
                }
                else if (total > 1024 * 1024)
                {
                    sizeDimension = "MB";
                    size = total / 1024 / 1024;
                }
                else if (total > 1024)
                {
                    sizeDimension = "KB";
                    size = total / 1024;
                }
                oldTotal = total;
            }
            //draw empty progress bar
            Console.CursorLeft = 0;
            Console.Write("["); //start
            Console.CursorLeft = 32;
            Console.Write("]"); //end
            Console.CursorLeft = 1;
            float onechunk = 30.0f / total;

            //draw filled part 
            int position = 1;
            for (int i = 0; i < onechunk * progress; i++)
            {
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            //draw unfilled part
            for (int i = position; i <= 31; i++)
            {
                Console.BackgroundColor = ConsoleColor.Black;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            //draw totals
            Console.CursorLeft = 35;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(" {0:N2} % of {1} {2}", ((double)progress / total * 100), size, sizeDimension);//progress.ToString() + " of " + total.ToString() + "    "); //blanks at the end remove any excess
        }
    }
}
