namespace SaturnEngine.Asset
{
    public class Tree : IComparable, IComparer<Tree>
    {
        public Tree? Father;
        public Tree? Left;
        public Tree? Right;
        public object? Value;
        public long Level;
        public Bits? HuffmanBits;

        public Tree()
        {
            //HuffmanBits = new Bits();
        }

        public int Compare(Tree? x, Tree? y)
        {
            return x.Level.CompareTo(y.Level);
        }

        public delegate void OnEach(Tree now, bool IsLeaf);
        public event OnEach? OnEachDo;
        /// <summary>
        /// 采用中左右顺序(并非如此,反正是先中间，再左边，左边完了到右边
        /// </summary>
        public void Foreach()
        {
            Pfc(this);
        }

        void Pfc(Tree t)
        {
            bool lef = false;
            if (t.Left == null && t.Right == null)
            {
                lef = true;
                OnEachDo?.Invoke(t, lef);
            }
            else
            {
                OnEachDo?.Invoke(t, lef);
                if (t.Left != null)
                {
                    Pfc(t.Left);
                }
                if (t.Right != null)
                {
                    Pfc(t.Right);
                }
            }
        }

        public Tree GetLastLeftNode()
        {
            return LeftNode(this);
        }
        public Tree GetLastRightNode()
        {
            return RightNode(this);
        }

        public Tree LeftNode(Tree t)
        {
            if (t.Left != null)
                return LeftNode(t.Left);
            else
                return t;
        }
        public Tree RightNode(Tree t)
        {
            if (t.Right != null)
                return RightNode(t.Right);
            else
                return t;
        }

        public int CompareTo(object? obj)
        {
            return Level.CompareTo(((Tree)obj).Level);
        }
    }
}
