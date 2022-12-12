using System.Text;

namespace ExternalSort
{
    public sealed class PolyphaseSort
    {
        private readonly StringBuilder _sb = new();
        private int _level;

        public void Sort<T>(string sourcePath, Func<string, T> selector, string outputPath = "", bool reverse = false, int n = 3) where T : IComparable
        {
            int[] batches = new int[n];
            int[] emptyBatches = new int[n];
            StreamReader source = File.OpenText(sourcePath);
            DistributeBatches(source, n, batches, emptyBatches);
            source.Close();

            Console.WriteLine($"Level = {_level}");
            Console.WriteLine($"Batches = {string.Join(", ", batches)}");
            Console.WriteLine($"Empty Batches = {string.Join(", ", emptyBatches)}");
            Console.WriteLine();

            int lastIndex = MergeBatches(n, batches, emptyBatches, selector);

            Console.WriteLine($"Batches = {string.Join(", ", batches)}");
            Console.WriteLine($"Empty Batches = {string.Join(", ", emptyBatches)}");

            if (File.Exists(outputPath))
                File.Delete(outputPath);
            File.Move($"tape{lastIndex}", outputPath);
        }

        private int MergeBatches<T>(int tapesCount, int[] batches, int[] emptyBatches, Func<string, T> selector) where T : IComparable
        {
            StreamReader[] inputs = new StreamReader[tapesCount - 1];
            int[] batchesLen = new int[tapesCount];
            int[] batchesLenLeft = new int[tapesCount];
            string[] firstElements = new string[tapesCount - 1];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = File.OpenText($"tape{i}");
                batchesLen[i] = 1;
            }
            int minTape = tapesCount - 2;
            while (_level > 0)
            {
                batches[tapesCount - 1] = 0;
                emptyBatches[tapesCount - 1] = 0;
                int batchesToMerge = batches[minTape];
                StreamWriter output = File.CreateText($"tape{tapesCount - 1}");
                while (batchesToMerge > 0)
                {
                    int realBatches = 0;
                    for (int i = 0; i < tapesCount - 1; i++)
                    {
                        batches[i]--;
                        if (emptyBatches[i] > 0)
                        {
                            emptyBatches[i]--;
                        }
                        else
                        {
                            batchesLenLeft[i] = batchesLen[i];
                            firstElements[i] = ReadNextElement(inputs[i]);
                            realBatches++;
                        }
                    }
                    batches[tapesCount - 1]++;
                    if (realBatches == 0)
                    {
                        emptyBatches[tapesCount - 1]++;
                        batchesToMerge--;
                        continue;
                    }
                    
                    while (realBatches > 0)
                    {
                        int minIndex = FindMin(batchesLenLeft, firstElements, selector);
                        batchesLenLeft[minIndex]--;
                        output.Write(firstElements[minIndex]);
                        output.Write(' ');
                        if (batchesLenLeft[minIndex] == 0)
                        {
                            realBatches--;
                        }
                        else
                        {
                            firstElements[minIndex] = ReadNextElement(inputs[minIndex]);
                            if (string.IsNullOrEmpty(firstElements[minIndex]))
                                realBatches--;
                        }
                    }
                    output.WriteLine();
                    batchesToMerge--;
                }
                output.Close();
                batches[minTape] = batches[tapesCount - 1];
                emptyBatches[minTape] = emptyBatches[tapesCount - 1];
                batchesLen[minTape] = batchesLen.Sum();
                inputs[minTape].Close();
                output.Close();
                File.Delete($"tape{minTape}");
                File.Move($"tape{tapesCount - 1}", $"tape{minTape}");
                inputs[minTape] = File.OpenText($"tape{minTape}");
                if (_level > 1)
                    minTape = minTape == 0 ? tapesCount - 2 : minTape - 1;
                _level--;
            }

            for (int i = 0; i < inputs.Length; i++)
                inputs[i].Close();
            return minTape;
        }

        private int FindMin<T>(int[] batchesLenLeft, string[] firstElements, Func<string, T> selector) where T : IComparable
        {
            int minIndex = -1;
            T minValue = default!;
            for (int i = 0; i < firstElements.Length; i++)
            {
                if (batchesLenLeft[i] == 0)
                    continue;
                string token = firstElements[i];
                if (string.IsNullOrEmpty(token))
                    continue;
                T current = selector(token);
                if (minIndex == -1 || current.CompareTo(minValue) < 0)
                {
                    minIndex = i;
                    minValue = current;
                }
            }
            return minIndex;
        }

        private void DistributeBatches(StreamReader source, int tapesCount, int[] batches, int[] emptyBatches)
        {
            StreamWriter[] files = new StreamWriter[tapesCount - 1];
            for (int i = 0; i < tapesCount - 1; i++)
            {
                files[i] = File.CreateText($"tape{i}");
                batches[i] = 1;
                emptyBatches[i] = 1;
            }
            _level = 1;
            int currentTape = 0;

            while (!source.EndOfStream)
            {
                // Select next tape so that all batches distributed evenly
                currentTape = emptyBatches[currentTape] < emptyBatches[currentTape + 1] ? currentTape + 1 : 0;
                if (emptyBatches[currentTape] == 0)
                {
                    // Distribution works that we run out of empty batches only on last tape
                    // and it means that we run out of all "free space" to insert next batch, so increase level
                    IncreaseLevel(batches, emptyBatches);
                    currentTape = 0;
                }
                WriteNextElement(source, files[currentTape]);
                files[currentTape].WriteLine();
                emptyBatches[currentTape]--;
            }

            for (int i = 0; i < tapesCount - 1; i++)
                files[i].Close();
        }

        private void IncreaseLevel(int[] batches, int[] emptyBatches)
        {
            _level++;
            int firstBatches = batches[0];
            for (int i = 0; i < batches.Length - 1; i++)
            {
                emptyBatches[i] = firstBatches + batches[i + 1] - batches[i];
                batches[i] = firstBatches + batches[i + 1];
            }
        }

        private void WriteNextElement(StreamReader input, StreamWriter output)
        {
            output.Write(ReadNextElement(input));
            output.Write(' ');
        }

        private string ReadNextElement(StreamReader source)
        {
            if (source.EndOfStream)
                return string.Empty;
            _sb.Clear();
            char letter = (char)source.Read();
            while (letter != ' ')
            {
                _sb.Append(letter);
                if (source.EndOfStream)
                    break;
                letter = (char)source.Read();
            }
            return _sb.Replace(Environment.NewLine, "").ToString();
        }
    }
}
