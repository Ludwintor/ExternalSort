using System.Diagnostics;

namespace ExternalSort
{
    public static class PolyphaseSort
    {
        private const string TEMP_FILE = ".tape";

        public static void Sort<T>(string sourcePath, Func<string, T> selector, string outputPath = "", bool reverse = false, int tapesCount = 3) where T : IComparable
        {
            Tape[] tapes = new Tape[tapesCount - 1];
            Comparison<T> comparer = reverse ? (lhs, rhs) => Comparer<T>.Default.Compare(rhs, lhs) : Comparer<T>.Default.Compare;
            int level = DistributeBatches(sourcePath, tapes);
            int lastIndex = MergeBatches(tapesCount, level, tapes, selector, comparer);

            if (string.IsNullOrEmpty(outputPath))
                outputPath = sourcePath;
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            File.Move($"{TEMP_FILE}{lastIndex}", outputPath);

            Cleanup(tapesCount);
        }

        private static int DistributeBatches(string sourcePath, Tape[] tapes)
        {
            StreamReader source = File.OpenText(sourcePath);
            for (int i = 0; i < tapes.Length; i++)
            {
                tapes[i].Writer = File.CreateText($"{TEMP_FILE}{i}");
                tapes[i].TotalBatches = 1;
                tapes[i].EmptyBatches = 1;
            }
            int level = 1;
            int current = 0;
            while (!source.EndOfStream)
            {
                string? element = source.ReadLine();
                if (string.IsNullOrEmpty(element))
                    continue;
                // Select next tape so that all batches distributed evenly
                if (current == tapes.Length - 1)
                    current = 0;
                else
                    current = tapes[current].EmptyBatches < tapes[current + 1].EmptyBatches ? current + 1 : 0;
                if (tapes[current].EmptyBatches == 0)
                {
                    // Distribution works that we run out of empty batches only on last tape
                    // and it means that we run out of all "free space" to insert next batch, so increase level
                    level++;
                    ExpandTapes(tapes);
                    current = 0;
                }
                tapes[current].Writer.WriteLine(element);
                tapes[current].Writer.WriteLine();
                tapes[current].EmptyBatches--;
            }
            source.Close();
            for (int i = 0; i < tapes.Length; i++)
                tapes[i].Writer.Close();
            return level;
        }

        private static int MergeBatches<T>(int tapesCount, int level, Tape[] tapes, Func<string, T> selector, Comparison<T> comparer) where T : IComparable
        {
            for (int i = 0; i < tapes.Length; i++)
                tapes[i].Reader = File.OpenText($"{TEMP_FILE}{i}");
            // Last input tape ALWAYS will be with min batches count
            // (tapesCount - 1 result in index for output tape, but in first while loop iteration it will be last input tape)
            int minTape = tapesCount - 1;
            while (level > 0)
            {
                // Each phase left tape from current min tape will result in being min for next phase (this is circular)
                minTape = minTape == 0 ? tapesCount - 2 : minTape - 1;
                int batchesToMerge = tapes[minTape].TotalBatches;
                int outputTotalBatches = batchesToMerge;
                int outputEmptyBatches = 0;
                StreamWriter output = File.CreateText($"{TEMP_FILE}{tapesCount - 1}");
                // Merge minimum batches count in output tape
                while (batchesToMerge > 0)
                {
                    // Prepare one batch per tape
                    int realBatches = 0;
                    for (int i = 0; i < tapes.Length; i++)
                    {
                        tapes[i].TotalBatches--;
                        if (tapes[i].EmptyBatches > 0)
                        {
                            tapes[i].EmptyBatches--;
                            tapes[i].BatchEnd = true;
                        }
                        else
                        {
                            realBatches++;
                            tapes[i].BatchEnd = false;
                            // Store first element of each batch to find min between them
                            tapes[i].CurrentElement = tapes[i].Reader.ReadLine();//ReadNextElement(inputs[i]);
                        }
                    }
                    // Merge them
                    // If all batches was empty (dummy) then just go next
                    if (realBatches == 0)
                    {
                        outputEmptyBatches++;
                        batchesToMerge--;
                        continue;
                    }
                    
                    // Find min value among batches' current values and write to output tape
                    while (realBatches > 0)
                    {
                        int minIndex = FindMin(tapes, selector, comparer);
                        output.WriteLine(tapes[minIndex].CurrentElement);

                        tapes[minIndex].CurrentElement = tapes[minIndex].Reader.ReadLine();
                        // Empty string means that we reached end of batch
                        if (string.IsNullOrEmpty(tapes[minIndex].CurrentElement))
                        {
                            tapes[minIndex].BatchEnd = true;
                            realBatches--;
                        }
                    }
                    // Write batch delimeter if not last phase and merge next
                    if (level > 1)
                        output.WriteLine();
                    batchesToMerge--;
                }
                output.Close();
                // Now "transfer" all data to empty tape
                tapes[minTape].TotalBatches = outputTotalBatches;
                tapes[minTape].EmptyBatches = outputEmptyBatches;
                tapes[minTape].Reader.Close();
                output.Close();
                // Swap output tape with empty tape
                File.Delete($"{TEMP_FILE}{minTape}");
                File.Move($"{TEMP_FILE}{tapesCount - 1}", $"{TEMP_FILE}{minTape}");
                tapes[minTape].Reader = File.OpenText($"{TEMP_FILE}{minTape}");
                level--;
            }

            for (int i = 0; i < tapes.Length; i++)
                tapes[i].Reader.Close();
            return minTape;
        }

        private static void Cleanup(int tapesCount)
        {
            for (int i = 0; i < tapesCount; i++)
            {
                string name = $"{TEMP_FILE}{i}";
                if (File.Exists(name))
                    File.Delete(name);
            }
        }

        private static int FindMin<T>(Tape[] tapes, Func<string, T> selector, Comparison<T> comparer) where T : IComparable
        {
            int minIndex = -1;
            T minValue = default!;
            for (int i = 0; i < tapes.Length; i++)
            {
                if (tapes[i].BatchEnd)
                    continue;
                string? token = tapes[i].CurrentElement;
                if (string.IsNullOrEmpty(token))
                    continue;
                T current = selector(token);
                if (minIndex == -1 || comparer(current, minValue) < 0)
                {
                    minIndex = i;
                    minValue = current;
                }
            }
            return minIndex;
        }

        private static void ExpandTapes(Tape[] tapes)
        {
            int firstBatches = tapes[0].TotalBatches;
            for (int i = 0; i < tapes.Length - 1; i++)
            {
                tapes[i].EmptyBatches = firstBatches + tapes[i + 1].TotalBatches - tapes[i].TotalBatches;
                tapes[i].TotalBatches = firstBatches + tapes[i + 1].TotalBatches;
            }
            tapes[^1].EmptyBatches = firstBatches - tapes[^1].TotalBatches;
            tapes[^1].TotalBatches = firstBatches;
        }

        private struct Tape
        {
            public int TotalBatches = 0;
            public int EmptyBatches = 0;
            public string? CurrentElement = null;
            public bool BatchEnd = false;
            public StreamReader Reader = null!;
            public StreamWriter Writer = null!;

            public Tape() { }
        }
    }
}
