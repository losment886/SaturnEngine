namespace SaturnEngine.Asset
{
    public class SEHuffman
    {
        //public static int BufferCount = 1024000;

        /*
        public static byte[] Encode(byte[] b)
        {
            //Dictionary<byte,int> fre = new Dictionary<byte,int>();

            //byte[] cac = new byte[0];
            List<Tree> trs = new List<Tree>();
            List<Tree> cac = new List<Tree>();
            
            int[] frequency = new int[256];
            
            
            for (int i = 0; i < b.Length; i++)
            {
                frequency[b[i]]++;
            }
            for (int t = 0; t < frequency.Length; t++)
            {
                if(frequency[t] == 0)
                    continue;
                Tree te = new Tree();
                te.V2 = t;
                te.Level = frequency[t];
                trs.Add(te);
            }
            trs.Sort();
            cac = new List<Tree>();
            for (int i = 0;i < trs.Count;i++)
            {
                if((i + 1) < trs.Count)
                {
                    Tree ca = new Tree();
                    ca.V2 = (int)trs[i].V2 + (int)trs[i + 1].V2;
                    ca.Level = trs[i].Level + trs[i+1].Level;
                    ca.Left = trs[i];
                    trs[i].Father = ca;
                    trs[i+1].Father = ca;
                    ca.Right = trs[i+1];
                    cac.Add(ca);
                    i++;
                }
                else
                {
                    cac.Add(trs[i]);
                }
            }
            while(cac.Count > 1)
            {
                trs = cac;
                cac = new List<Tree>();
                trs.Sort();
                for (int i = 0; i < trs.Count; i++)
                {
                    if ((i + 1) < trs.Count)
                    {
                        Tree ca = new Tree();
                        ca.V2 = (int)trs[i].V2 + (int)trs[i + 1].V2;
                        ca.Level = trs[i].Level + trs[i + 1].Level;
                        ca.Left = trs[i];
                        ca.Right = trs[i + 1];
                        trs[i].Father = ca;
                        trs[i + 1].Father = ca;
                        cac.Add(ca);
                        i++;
                    }
                    else
                    {
                        cac.Add(trs[i]);
                    }
                }

            }
            trs = cac;
            Tree tps = trs[0];
            Dictionary<byte,Bits> rpp = new Dictionary<byte,Bits>();
            tps.OnEachDo += (t, l)=>
            {
                Console.WriteLine($"Tree:{(t.V2 ?? "")}|{t.Level}|{(t.HuffmanBits == null ? "" : t.HuffmanBits)}|F={(t.Father == null?"": t.Father.V2)}");
                if (l)
                {
                    rpp.Add((byte)(int)t.V2, t.HuffmanBits);
                }
                if (t.Left!=null)
                {
                    t.Left.HuffmanBits = new Bits();
                    if(t.HuffmanBits !=  null)
                    {
                        t.Left.HuffmanBits.Add(t.HuffmanBits);
                        t.Left.HuffmanBits.Add(0);
                    }
                    else
                    {
                        t.Left.HuffmanBits.Add(0);
                    }
                }
                if (t.Right != null)
                {
                    t.Right.HuffmanBits = new Bits();
                    if (t.HuffmanBits != null)
                    {
                        t.Right.HuffmanBits.Add(t.HuffmanBits);
                        t.Right.HuffmanBits.Add(1);
                    }
                    else
                    {
                        t.Right.HuffmanBits.Add(1);
                    }
                }
            };
            tps.Foreach();
            Bits cout = new Bits();
            for(int i = 0; i < b.Length; i++)
            {
                cout.Add(rpp[b[i]]);
            }
            return BitEditor.BitsToBytes(cout);
        }*/
        public static byte[] Encode(byte[] b)
        {
            if (b == null || b.Length == 0)
                return new byte[0];

            int[] frequency = new int[256];
            foreach (byte bt in b)
                frequency[bt]++;

            List<Tree> trs = new List<Tree>();
            for (int t = 0; t < frequency.Length; t++)
            {
                if (frequency[t] == 0) continue;
                trs.Add(new Tree { Value = t, Level = frequency[t] });
            }

            if (trs.Count == 0)
                return new byte[0];

            // Build Huffman Tree
            while (trs.Count > 1)
            {
                trs.Sort();
                Tree left = trs[0];
                Tree right = trs[1];
                trs.RemoveRange(0, 2);

                Tree parent = new Tree
                {
                    Level = left.Level + right.Level,
                    Left = left,
                    Right = right
                };
                left.Father = parent;
                right.Father = parent;
                trs.Add(parent);
            }

            Tree root = trs[0];
            Dictionary<byte, Bits> encodingMap = new Dictionary<byte, Bits>();

            root.OnEachDo += (current, isLeaf) =>
           {
               if (isLeaf)
               {
                   byte value = (byte)(int)current.Value;
                   encodingMap[value] = current.HuffmanBits;
               }

               if (current.Left != null)
               {
                   current.Left.HuffmanBits = new Bits();
                   if (current.HuffmanBits != null)
                       current.Left.HuffmanBits.Add(current.HuffmanBits);
                   current.Left.HuffmanBits.Add(0);
               }

               if (current.Right != null)
               {
                   current.Right.HuffmanBits = new Bits();
                   if (current.HuffmanBits != null)
                       current.Right.HuffmanBits.Add(current.HuffmanBits);
                   current.Right.HuffmanBits.Add(1);
               }
           };
            root.Foreach();
            // Encode data
            Bits encodedBits = new Bits();
            foreach (byte bt in b)
                encodedBits.Add(encodingMap[bt]);

            // Prepare output with header (frequency + bitCount) and encoded data
            byte[] freqBytes = new byte[256 * 4];
            for (int i = 0; i < 256; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(frequency[i]), 0, freqBytes, i * 4, 4);

            int bitCount = encodedBits.bts.Count;
            byte[] bitCountBytes = BitConverter.GetBytes(bitCount);
            byte[] dataBytes = BitEditor.BitsToBytes(encodedBits);

            byte[] result = new byte[freqBytes.Length + 4 + dataBytes.Length];
            Buffer.BlockCopy(freqBytes, 0, result, 0, freqBytes.Length);
            Buffer.BlockCopy(bitCountBytes, 0, result, freqBytes.Length, 4);
            Buffer.BlockCopy(dataBytes, 0, result, freqBytes.Length + 4, dataBytes.Length);

            return result;
        }
        public static byte[] Decode(byte[] encodedData)
        {
            if (encodedData.Length < 1028)
                throw new ArgumentException("Invalid encoded data.");

            // Parse frequency table and bit count
            int[] frequency = new int[256];
            for (int i = 0; i < 256; i++)
                frequency[i] = BitConverter.ToInt32(encodedData, i * 4);

            int bitCount = BitConverter.ToInt32(encodedData, 1024);
            byte[] huffmanBytes = new byte[encodedData.Length - 1028];
            Array.Copy(encodedData, 1028, huffmanBytes, 0, huffmanBytes.Length);

            // Rebuild Huffman Tree
            List<Tree> nodes = new List<Tree>();
            for (int i = 0; i < frequency.Length; i++)
            {
                if (frequency[i] > 0)
                    nodes.Add(new Tree { Value = i, Level = frequency[i] });
            }

            if (nodes.Count == 0)
                return new byte[0];

            while (nodes.Count > 1)
            {
                nodes.Sort();
                Tree left = nodes[0];
                Tree right = nodes[1];
                nodes.RemoveRange(0, 2);

                Tree parent = new Tree
                {
                    Level = left.Level + right.Level,
                    Left = left,
                    Right = right
                };
                left.Father = parent;
                right.Father = parent;
                nodes.Add(parent);
            }

            Tree root = nodes[0];
            Bits bits = BitEditor.BytesToBits(huffmanBytes);

            // Decode bits
            List<byte> decodedData = new List<byte>();
            Tree current = root;
            for (int i = 0; i < bitCount; i++)
            {
                current = bits.bts[i] ? current.Right : current.Left;
                if (current.Left == null && current.Right == null)
                {
                    decodedData.Add((byte)(int)current.Value);
                    current = root;
                }
            }

            return decodedData.ToArray();
        }
    }
}
