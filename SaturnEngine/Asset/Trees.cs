using SaturnEngine.Management.IO;
using SaturnEngine.Management.SEMemory;

namespace SaturnEngine.Asset
{
    
    
    /// <summary>
    /// B+ 树主类，LRL专属优化款
    /// </summary>
    class LRLBPlusTree
    {
        //规定 Key与Value都为ulong
        
        
        /// <summary>
        /// B+ 树节点抽象基类
        /// </summary>
        abstract class LRLBPlusTreeNode
        {
            public List<ulong> Keys;
            public int Degree; // 树的阶 (最大子节点数)

            protected LRLBPlusTreeNode(int degree)
            {
                Degree = degree;
                Keys = new List<ulong>(degree);
            }

            public abstract bool IsLeaf { get; }
        }

        /// <summary>
        /// B+ 树内部节点
        /// </summary>
        class LRLInternalNode : LRLBPlusTreeNode
        {
            public List<LRLBPlusTreeNode> Children;

            public LRLInternalNode(int degree) : base(degree)
            {
                Children = new List<LRLBPlusTreeNode>(degree);
            }

            public override bool IsLeaf => false;
        }

        /// <summary>
        /// B+ 树叶节点 (存储实际数据)
        /// </summary>
        class LRLLeafNode : LRLBPlusTreeNode
        {
            public List<ulong> Values;
            public LRLLeafNode? Next; // 链表指向下一叶节点

            public LRLLeafNode(int degree) : base(degree)
            {
                Values = new List<ulong>(degree);
                Next = null;
            }

            public override bool IsLeaf => true;
        }


        private LRLBPlusTreeNode _root;
        private LRLLeafNode? _firstLeaf;
        public int Degree { get; }
        private int _maxLeafKeys; // 叶节点最大 key 数 = Degree - 1
        private int _minLeafKeys; // 叶节点最小 key 数 = ceil((Degree-1)/2)
        private int _maxInternalChildren; // 内部节点最大子节点数 = Degree
        private int _minInternalChildren; // 内部节点最小子节点数 = ceil(Degree/2)

        public LRLBPlusTree(int degree = 4)
        {
            if (degree < 3)
                throw new ArgumentException("B+ 树的阶至少为 3");
            Degree = degree;
            _maxLeafKeys = degree - 1;
            _minLeafKeys = (int)Math.Ceiling((degree - 1) / 2.0);
            _maxInternalChildren = degree;
            _minInternalChildren = (int)Math.Ceiling(degree / 2.0);

            _root = new LRLLeafNode(degree);
            _firstLeaf = (LRLLeafNode)_root;
        }

        // ==================== 查找 ====================

        /// <summary>
        /// 精确查找 key 对应的 value
        /// </summary>
        public ulong? Search(ulong key)
        {
            var leaf = FindLeaf(key);
            int idx = leaf.Keys.BinarySearch(key);
            if (idx >= 0)
                return leaf.Values[idx];
            return default;
        }

        /// <summary>
        /// 判断 key 是否存在
        /// </summary>
        public bool Contains(ulong key)
        {
            var leaf = FindLeaf(key);
            return leaf.Keys.BinarySearch(key) >= 0;
        }

        /// <summary>
        /// 范围查询: 返回 [startKey, endKey] 范围内的所有键值对
        /// </summary>
        public List<(ulong Key, ulong Value)> RangeSearch(ulong startKey, ulong endKey)
        {
            var result = new List<(ulong, ulong)>();
            var leaf = FindLeaf(startKey);

            while (leaf != null)
            {
                for (int i = 0; i < leaf.Keys.Count; i++)
                {
                    if (leaf.Keys[i].CompareTo(startKey) >= 0 &&
                        leaf.Keys[i].CompareTo(endKey) <= 0)
                    {
                        result.Add((leaf.Keys[i], leaf.Values[i]));
                    }

                    if (leaf.Keys[i].CompareTo(endKey) > 0)
                        return result;
                }

                leaf = leaf.Next;
            }

            return result;
        }

        /// <summary>
        /// 找到 key 所在的叶节点
        /// </summary>
        private LRLLeafNode FindLeaf(ulong key)
        {
            var node = _root;
            while (!node.IsLeaf)
            {
                var internalNode = (LRLInternalNode)node;
                int i = 0;
                while (i < node.Keys.Count && key.CompareTo(node.Keys[i]) >= 0)
                    i++;
                node = internalNode.Children[i];
            }

            return (LRLLeafNode)node;
        }

        // ==================== 插入 ====================

        /// <summary>
        /// 插入键值对
        /// </summary>
        public void Insert(ulong key, ulong value)
        {
            var leaf = FindLeaf(key);

            // 如果 key 已存在, 替换 value
            int existIdx = leaf.Keys.BinarySearch(key);
            if (existIdx >= 0)
            {
                leaf.Values[existIdx] = value;
                return;
            }

            // 插入到叶节点
            InsertIntoLeaf(leaf, key, value);

            // 叶节点溢出则分裂
            if (leaf.Keys.Count > _maxLeafKeys)
            {
                SplitLeaf(leaf);
            }
        }

        private void InsertIntoLeaf(LRLLeafNode leaf, ulong key, ulong value)
        {
            int insertPos = ~leaf.Keys.BinarySearch(key);
            leaf.Keys.Insert(insertPos, key);
            leaf.Values.Insert(insertPos, value);
        }

        /// <summary>
        /// 分裂叶节点
        /// </summary>
        private void SplitLeaf(LRLLeafNode leaf)
        {
            int degree = Degree;
            var newLeaf = new LRLLeafNode(degree);

            // 将一半 key 移到新叶节点
            int mid = leaf.Keys.Count / 2;
            newLeaf.Keys.AddRange(leaf.Keys.GetRange(mid, leaf.Keys.Count - mid));
            newLeaf.Values.AddRange(leaf.Values.GetRange(mid, leaf.Values.Count - mid));
            leaf.Keys.RemoveRange(mid, leaf.Keys.Count - mid);
            leaf.Values.RemoveRange(mid, leaf.Values.Count - mid);

            // 维护叶节点链表
            newLeaf.Next = leaf.Next;
            leaf.Next = newLeaf;

            // 将新叶节点的第一个 key 提升到父节点
            ulong promoteKey = newLeaf.Keys[0];
            InsertIntoParent(leaf, promoteKey, newLeaf);
        }

        /// <summary>
        /// 分裂内部节点
        /// </summary>
        private void SplitInternal(LRLInternalNode node)
        {
            int degree = Degree;
            var newNode = new LRLInternalNode(degree);

            int mid = node.Keys.Count / 2;
            // 提升的 key (不放入新节点)
            ulong promoteKey = node.Keys[mid];

            // 右半部分的 key 和 children 移到新节点
            newNode.Keys.AddRange(node.Keys.GetRange(mid + 1, node.Keys.Count - mid - 1));
            newNode.Children.AddRange(node.Children.GetRange(mid + 1, node.Children.Count - mid - 1));

            // 缩减原节点
            node.Keys.RemoveRange(mid, node.Keys.Count - mid);
            node.Children.RemoveRange(mid + 1, node.Children.Count - mid - 1);

            InsertIntoParent(node, promoteKey, newNode);
        }

        /// <summary>
        /// 将分裂产生的新节点插入到父节点
        /// </summary>
        private void InsertIntoParent(LRLBPlusTreeNode leftChild, ulong key,
            LRLBPlusTreeNode rightChild)
        {
            if (leftChild == _root)
            {
                // 根节点分裂, 新建根
                var newRoot = new LRLInternalNode(Degree);
                newRoot.Keys.Add(key);
                newRoot.Children.Add(leftChild);
                newRoot.Children.Add(rightChild);
                _root = newRoot;
                return;
            }

            // 找到父节点
            var parent = FindParent(_root, leftChild)!;

            int insertIdx = parent.Keys.BinarySearch(key);
            if (insertIdx < 0) insertIdx = ~insertIdx;
            parent.Keys.Insert(insertIdx, key);
            parent.Children.Insert(insertIdx + 1, rightChild);

            // 父节点溢出则继续向上分裂
            if (parent.Keys.Count > _maxInternalChildren - 1)
            {
                SplitInternal(parent);
            }
        }

        /// <summary>
        /// 在子树中查找目标节点的父节点
        /// </summary>
        private LRLInternalNode? FindParent(LRLBPlusTreeNode current,
            LRLBPlusTreeNode target)
        {
            if (current.IsLeaf) return null;

            var internalNode = (LRLInternalNode)current;
            foreach (var child in internalNode.Children)
            {
                if (child == target)
                    return internalNode;
                var result = FindParent(child, target);
                if (result != null)
                    return result;
            }

            return null;
        }

        // ==================== 修改 ====================

        /// <summary>
        /// 修改指定 key 对应的 value; key 不存在则返回 false
        /// </summary>
        public bool Update(ulong key, ulong newValue)
        {
            var leaf = FindLeaf(key);
            int idx = leaf.Keys.BinarySearch(key);
            if (idx < 0) return false;
            leaf.Values[idx] = newValue;
            return true;
        }

        // ==================== 删除 ====================

        /// <summary>
        /// 删除指定 key
        /// </summary>
        public bool Delete(ulong key)
        {
            var leaf = FindLeaf(key);
            int idx = leaf.Keys.BinarySearch(key);
            if (idx < 0) return false;

            // 从叶节点删除
            leaf.Keys.RemoveAt(idx);
            leaf.Values.RemoveAt(idx);

            if (leaf == _root)
            {
                // 根就是叶节点, 无需额外处理
                return true;
            }

            // 处理下溢
            if (leaf.Keys.Count < _minLeafKeys)
            {
                HandleLeafUnderflow(leaf);
            }

            return true;
        }

        /// <summary>
        /// 处理叶节点下溢
        /// </summary>
        private void HandleLeafUnderflow(LRLLeafNode leaf)
        {
            var parent = (LRLInternalNode)FindParent(_root, leaf)!;
            int childIdx = parent.Children.IndexOf(leaf);

            // 尝试从左兄弟借
            if (childIdx > 0)
            {
                var leftSibling = (LRLLeafNode)parent.Children[childIdx - 1];
                if (leftSibling.Keys.Count > _minLeafKeys)
                {
                    BorrowFromLeftLeaf(leaf, leftSibling, parent, childIdx);
                    return;
                }
            }

            // 尝试从右兄弟借
            if (childIdx < parent.Children.Count - 1)
            {
                var rightSibling = (LRLLeafNode)parent.Children[childIdx + 1];
                if (rightSibling.Keys.Count > _minLeafKeys)
                {
                    BorrowFromRightLeaf(leaf, rightSibling, parent, childIdx);
                    return;
                }
            }

            // 无法借, 合并
            if (childIdx > 0)
            {
                var leftSibling = (LRLLeafNode)parent.Children[childIdx - 1];
                MergeLeaves(leftSibling, leaf, parent, childIdx - 1);
            }
            else
            {
                var rightSibling = (LRLLeafNode)parent.Children[childIdx + 1];
                MergeLeaves(leaf, rightSibling, parent, childIdx);
            }
        }

        private void BorrowFromLeftLeaf(LRLLeafNode leaf,
            LRLLeafNode leftSibling,
            LRLInternalNode parent, int childIdx)
        {
            // 从左兄弟移最后一个元素到当前叶节点开头
            var lastKey = leftSibling.Keys[^1];
            var lastVal = leftSibling.Values[^1];
            leftSibling.Keys.RemoveAt(leftSibling.Keys.Count - 1);
            leftSibling.Values.RemoveAt(leftSibling.Values.Count - 1);

            leaf.Keys.Insert(0, lastKey);
            leaf.Values.Insert(0, lastVal);

            // 更新父节点分隔 key
            parent.Keys[childIdx - 1] = leaf.Keys[0];
        }

        private void BorrowFromRightLeaf(LRLLeafNode leaf,
            LRLLeafNode rightSibling,
            LRLInternalNode parent, int childIdx)
        {
            // 从右兄弟移第一个元素到当前叶节点末尾
            var firstKey = rightSibling.Keys[0];
            var firstVal = rightSibling.Values[0];
            rightSibling.Keys.RemoveAt(0);
            rightSibling.Values.RemoveAt(0);

            leaf.Keys.Add(firstKey);
            leaf.Values.Add(firstVal);

            // 更新父节点分隔 key
            parent.Keys[childIdx] = rightSibling.Keys[0];
        }

        /// <summary>
        /// 合并两个叶节点 (left 吸收 right)
        /// </summary>
        private void MergeLeaves(LRLLeafNode left, LRLLeafNode right,
            LRLInternalNode parent, int leftChildIdx)
        {
            left.Keys.AddRange(right.Keys);
            left.Values.AddRange(right.Values);
            left.Next = right.Next;

            // 从父节点移除分隔 key 和右子节点
            parent.Keys.RemoveAt(leftChildIdx);
            parent.Children.RemoveAt(leftChildIdx + 1);

            // 父节点下溢处理
            if (parent == _root)
            {
                if (parent.Children.Count == 1)
                {
                    _root = parent.Children[0];
                }
            }
            else if (parent.Keys.Count < _minInternalChildren - 1)
            {
                HandleInternalUnderflow(parent);
            }
        }

        /// <summary>
        /// 处理内部节点下溢
        /// </summary>
        private void HandleInternalUnderflow(LRLInternalNode node)
        {
            var parent = (LRLInternalNode)FindParent(_root, node)!;
            int childIdx = parent.Children.IndexOf(node);

            // 尝试从左兄弟借
            if (childIdx > 0)
            {
                var leftSibling = (LRLInternalNode)parent.Children[childIdx - 1];
                if (leftSibling.Keys.Count > _minInternalChildren - 1)
                {
                    BorrowFromLeftInternal(node, leftSibling, parent, childIdx);
                    return;
                }
            }

            // 尝试从右兄弟借
            if (childIdx < parent.Children.Count - 1)
            {
                var rightSibling = (LRLInternalNode)parent.Children[childIdx + 1];
                if (rightSibling.Keys.Count > _minInternalChildren - 1)
                {
                    BorrowFromRightInternal(node, rightSibling, parent, childIdx);
                    return;
                }
            }

            // 合并
            if (childIdx > 0)
            {
                var leftSibling = (LRLInternalNode)parent.Children[childIdx - 1];
                MergeInternal(leftSibling, node, parent, childIdx - 1);
            }
            else
            {
                var rightSibling = (LRLInternalNode)parent.Children[childIdx + 1];
                MergeInternal(node, rightSibling, parent, childIdx);
            }
        }

        private void BorrowFromLeftInternal(LRLInternalNode node,
            LRLInternalNode leftSibling,
            LRLInternalNode parent, int childIdx)
        {
            // 父节点分隔 key 下沉到 node
            node.Keys.Insert(0, parent.Keys[childIdx - 1]);
            // 左兄弟最后一个子节点移过来
            node.Children.Insert(0, leftSibling.Children[^1]);
            leftSibling.Children.RemoveAt(leftSibling.Children.Count - 1);

            // 左兄弟最后一个 key 提升到父节点
            parent.Keys[childIdx - 1] = leftSibling.Keys[^1];
            leftSibling.Keys.RemoveAt(leftSibling.Keys.Count - 1);
        }

        private void BorrowFromRightInternal(LRLInternalNode node,
            LRLInternalNode rightSibling,
            LRLInternalNode parent, int childIdx)
        {
            // 父节点分隔 key 下沉到 node
            node.Keys.Add(parent.Keys[childIdx]);
            // 右兄弟第一个子节点移过来
            node.Children.Add(rightSibling.Children[0]);
            rightSibling.Children.RemoveAt(0);

            // 右兄弟第一个 key 提升到父节点
            parent.Keys[childIdx] = rightSibling.Keys[0];
            rightSibling.Keys.RemoveAt(0);
        }

        private void MergeInternal(LRLInternalNode left,
            LRLInternalNode right,
            LRLInternalNode parent, int leftChildIdx)
        {
            // 父节点分隔 key 下沉
            left.Keys.Add(parent.Keys[leftChildIdx]);
            // 合并 right
            left.Keys.AddRange(right.Keys);
            left.Children.AddRange(right.Children);

            // 从父节点移除
            parent.Keys.RemoveAt(leftChildIdx);
            parent.Children.RemoveAt(leftChildIdx + 1);

            if (parent == _root)
            {
                if (parent.Children.Count == 1)
                {
                    _root = parent.Children[0];
                }
            }
            else if (parent.Keys.Count < _minInternalChildren - 1)
            {
                HandleInternalUnderflow(parent);
            }
        }

        // ==================== 打印 / 验证 ====================

        /// <summary>
        /// 以树形结构打印 B+ 树
        /// </summary>
        public void Print()
        {
            Console.WriteLine($"=== B+ Tree (Degree={Degree}) ===");
            PrintNode(_root, 0);
            Console.WriteLine();
            Console.Write("Leaf chain: ");
            PrintLeafChain();
        }

        private void PrintNode(LRLBPlusTreeNode node, int indent)
        {
            string prefix = new string(' ', indent * 4);

            if (node.IsLeaf)
            {
                var leaf = (LRLLeafNode)node;
                Console.Write($"{prefix}[Leaf] ");
                for (int i = 0; i < leaf.Keys.Count; i++)
                {
                    Console.Write($"{leaf.Keys[i]}={leaf.Values[i]}");
                    if (i < leaf.Keys.Count - 1) Console.Write(", ");
                }

                Console.WriteLine();
            }
            else
            {
                var internalNode = (LRLInternalNode)node;
                Console.Write($"{prefix}[Internal] keys: ");
                Console.WriteLine(string.Join(", ", internalNode.Keys));
                foreach (var child in internalNode.Children)
                {
                    PrintNode(child, indent + 1);
                }
            }
        }

        private void PrintLeafChain()
        {
            var leaf = _firstLeaf;
            while (leaf != null)
            {
                Console.Write("[");
                for (int i = 0; i < leaf.Keys.Count; i++)
                {
                    Console.Write($"{leaf.Keys[i]}={leaf.Values[i]}");
                    if (i < leaf.Keys.Count - 1) Console.Write(", ");
                }

                Console.Write("]");
                if (leaf.Next != null) Console.Write(" -> ");
                leaf = leaf.Next;
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 获取所有键值对 (按序遍历叶节点链表)
        /// </summary>
        public List<(ulong Key, ulong Value)> GetAll()
        {
            var result = new List<(ulong, ulong)>();
            var leaf = _firstLeaf;
            while (leaf != null)
            {
                for (int i = 0; i < leaf.Keys.Count; i++)
                    result.Add((leaf.Keys[i], leaf.Values[i]));
                leaf = leaf.Next;
            }

            return result;
        }

        /// <summary>
        /// 从字节数组反序列化还原 B+ 树 (直接重建, 不逐个 Insert)
        /// </summary>
        public static LRLBPlusTree LoadFromByteArray(byte[] data)
        {
            SEMemoryStream ms = new SEMemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            BinaryOperator bo = new BinaryOperator(ms);
            
            // 读取 Degree
            int degree = bo.ReadInt32();
            var tree = new LRLBPlusTree(degree);
            
            // 递归重建树
            tree._root = LdNode(bo, degree);
            
            // 重建叶节点链表
            LRLLeafNode? firstLeaf = null;
            LRLLeafNode? prevLeaf = null;
            BuildLeafChain(tree._root, ref firstLeaf, ref prevLeaf);
            tree._firstLeaf = firstLeaf;
            
            return tree;
        }

        /// <summary>
        /// 将 B+ 树序列化为字节数组 (先序遍历, 紧凑格式)
        /// </summary>
        public byte[] SaveToByteArray()
        {
            SEMemoryStream ms = new SEMemoryStream();
            BinaryOperator bo = new BinaryOperator(ms);
            
            // Header: Degree
            bo.Write(Degree);
            
            // 先序遍历写入所有节点
            SvNode(bo, _root);
            
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            byte[] datas = new byte[ms.Length];
            ms.Read(datas, 0, (int)ms.Length);
            return datas;
        }

        /// <summary>
        /// 递归写入节点 (先序遍历)
        /// </summary>
        void SvNode(BinaryOperator bo, LRLBPlusTreeNode node)
        {
            if (node.IsLeaf)
            {
                var leaf = (LRLLeafNode)node;
                // 类型标记: 1 = 叶节点
                bo.Write((byte)1);
                // Keys 数量 (用 byte, 阶数有限所以数量不会超过 255)
                bo.Write((byte)leaf.Keys.Count);
                // 写入所有 Keys
                for (int i = 0; i < leaf.Keys.Count; i++)
                    bo.Write(leaf.Keys[i]);
                // 写入所有 Values
                for (int i = 0; i < leaf.Values.Count; i++)
                    bo.Write(leaf.Values[i]);
            }
            else
            {
                var internalNode = (LRLInternalNode)node;
                // 类型标记: 0 = 内部节点
                bo.Write((byte)0);
                // Keys 数量
                bo.Write((byte)internalNode.Keys.Count);
                // 写入所有 Keys
                for (int i = 0; i < internalNode.Keys.Count; i++)
                    bo.Write(internalNode.Keys[i]);
                // 递归写入所有子节点
                for (int i = 0; i < internalNode.Children.Count; i++)
                    SvNode(bo, internalNode.Children[i]);
            }
        }

        /// <summary>
        /// 递归读取节点 (先序遍历)
        /// </summary>
        static LRLBPlusTreeNode LdNode(BinaryOperator bo, int degree)
        {
            byte nodeType = bo.ReadUInt8();
            
            if (nodeType == 1) // 叶节点
            {
                var leaf = new LRLLeafNode(degree);
                int keyCount = bo.ReadUInt8();
                for (int i = 0; i < keyCount; i++)
                    leaf.Keys.Add(bo.ReadUInt64());
                for (int i = 0; i < keyCount; i++)
                    leaf.Values.Add(bo.ReadUInt64());
                return leaf;
            }
            else // 内部节点
            {
                var internalNode = new LRLInternalNode(degree);
                int keyCount = bo.ReadUInt8();
                for (int i = 0; i < keyCount; i++)
                    internalNode.Keys.Add(bo.ReadUInt64());
                // 子节点数量 = Keys.Count + 1
                int childCount = keyCount + 1;
                for (int i = 0; i < childCount; i++)
                    internalNode.Children.Add(LdNode(bo, degree));
                return internalNode;
            }
        }

        /// <summary>
        /// 遍历树, 按先序遍历顺序链接叶节点
        /// </summary>
        static void BuildLeafChain(LRLBPlusTreeNode node, ref LRLLeafNode? firstLeaf, ref LRLLeafNode? prevLeaf)
        {
            if (node.IsLeaf)
            {
                var leaf = (LRLLeafNode)node;
                if (firstLeaf == null)
                    firstLeaf = leaf;
                if (prevLeaf != null)
                    prevLeaf.Next = leaf;
                prevLeaf = leaf;
            }
            else
            {
                var internalNode = (LRLInternalNode)node;
                foreach (var child in internalNode.Children)
                    BuildLeafChain(child, ref firstLeaf, ref prevLeaf);
            }
        }
    }

    
    
    
    
    
    /// <summary>
    /// B+ 树主类
    /// </summary>
    class BPlusTree<TKey, TValue> where TKey : IComparable<TKey>
    {
        /// <summary>
        /// B+ 树节点抽象基类
        /// </summary>
        abstract class BPlusTreeNode<TKey, TValue> where TKey : IComparable<TKey>
        {
            public List<TKey> Keys;
            public int Degree; // 树的阶 (最大子节点数)

            protected BPlusTreeNode(int degree)
            {
                Degree = degree;
                Keys = new List<TKey>(degree);
            }

            public abstract bool IsLeaf { get; }
        }

        /// <summary>
        /// B+ 树内部节点
        /// </summary>
        class InternalNode<TKey, TValue> : BPlusTreeNode<TKey, TValue> where TKey : IComparable<TKey>
        {
            public List<BPlusTreeNode<TKey, TValue>> Children;

            public InternalNode(int degree) : base(degree)
            {
                Children = new List<BPlusTreeNode<TKey, TValue>>(degree);
            }

            public override bool IsLeaf => false;
        }

        /// <summary>
        /// B+ 树叶节点 (存储实际数据)
        /// </summary>
        class LeafNode<TKey, TValue> : BPlusTreeNode<TKey, TValue> where TKey : IComparable<TKey>
        {
            public List<TValue> Values;
            public LeafNode<TKey, TValue>? Next; // 链表指向下一叶节点

            public LeafNode(int degree) : base(degree)
            {
                Values = new List<TValue>(degree);
                Next = null;
            }

            public override bool IsLeaf => true;
        }


        private BPlusTreeNode<TKey, TValue> _root;
        private LeafNode<TKey, TValue>? _firstLeaf;
        public int Degree { get; }
        private int _maxLeafKeys; // 叶节点最大 key 数 = Degree - 1
        private int _minLeafKeys; // 叶节点最小 key 数 = ceil((Degree-1)/2)
        private int _maxInternalChildren; // 内部节点最大子节点数 = Degree
        private int _minInternalChildren; // 内部节点最小子节点数 = ceil(Degree/2)

        public BPlusTree(int degree = 4)
        {
            if (degree < 3)
                throw new ArgumentException("B+ 树的阶至少为 3");
            Degree = degree;
            _maxLeafKeys = degree - 1;
            _minLeafKeys = (int)Math.Ceiling((degree - 1) / 2.0);
            _maxInternalChildren = degree;
            _minInternalChildren = (int)Math.Ceiling(degree / 2.0);

            _root = new LeafNode<TKey, TValue>(degree);
            _firstLeaf = (LeafNode<TKey, TValue>)_root;
        }

        // ==================== 查找 ====================

        /// <summary>
        /// 精确查找 key 对应的 value
        /// </summary>
        public TValue? Search(TKey key)
        {
            var leaf = FindLeaf(key);
            int idx = leaf.Keys.BinarySearch(key);
            if (idx >= 0)
                return leaf.Values[idx];
            return default;
        }

        /// <summary>
        /// 判断 key 是否存在
        /// </summary>
        public bool Contains(TKey key)
        {
            var leaf = FindLeaf(key);
            return leaf.Keys.BinarySearch(key) >= 0;
        }

        /// <summary>
        /// 范围查询: 返回 [startKey, endKey] 范围内的所有键值对
        /// </summary>
        public List<(TKey Key, TValue Value)> RangeSearch(TKey startKey, TKey endKey)
        {
            var result = new List<(TKey, TValue)>();
            var leaf = FindLeaf(startKey);

            while (leaf != null)
            {
                for (int i = 0; i < leaf.Keys.Count; i++)
                {
                    if (leaf.Keys[i].CompareTo(startKey) >= 0 &&
                        leaf.Keys[i].CompareTo(endKey) <= 0)
                    {
                        result.Add((leaf.Keys[i], leaf.Values[i]));
                    }

                    if (leaf.Keys[i].CompareTo(endKey) > 0)
                        return result;
                }

                leaf = leaf.Next;
            }

            return result;
        }

        /// <summary>
        /// 找到 key 所在的叶节点
        /// </summary>
        private LeafNode<TKey, TValue> FindLeaf(TKey key)
        {
            var node = _root;
            while (!node.IsLeaf)
            {
                var internalNode = (InternalNode<TKey, TValue>)node;
                int i = 0;
                while (i < node.Keys.Count && key.CompareTo(node.Keys[i]) >= 0)
                    i++;
                node = internalNode.Children[i];
            }

            return (LeafNode<TKey, TValue>)node;
        }

        // ==================== 插入 ====================

        /// <summary>
        /// 插入键值对
        /// </summary>
        public void Insert(TKey key, TValue value)
        {
            var leaf = FindLeaf(key);

            // 如果 key 已存在, 替换 value
            int existIdx = leaf.Keys.BinarySearch(key);
            if (existIdx >= 0)
            {
                leaf.Values[existIdx] = value;
                return;
            }

            // 插入到叶节点
            InsertIntoLeaf(leaf, key, value);

            // 叶节点溢出则分裂
            if (leaf.Keys.Count > _maxLeafKeys)
            {
                SplitLeaf(leaf);
            }
        }

        private void InsertIntoLeaf(LeafNode<TKey, TValue> leaf, TKey key, TValue value)
        {
            int insertPos = ~leaf.Keys.BinarySearch(key);
            leaf.Keys.Insert(insertPos, key);
            leaf.Values.Insert(insertPos, value);
        }

        /// <summary>
        /// 分裂叶节点
        /// </summary>
        private void SplitLeaf(LeafNode<TKey, TValue> leaf)
        {
            int degree = Degree;
            var newLeaf = new LeafNode<TKey, TValue>(degree);

            // 将一半 key 移到新叶节点
            int mid = leaf.Keys.Count / 2;
            newLeaf.Keys.AddRange(leaf.Keys.GetRange(mid, leaf.Keys.Count - mid));
            newLeaf.Values.AddRange(leaf.Values.GetRange(mid, leaf.Values.Count - mid));
            leaf.Keys.RemoveRange(mid, leaf.Keys.Count - mid);
            leaf.Values.RemoveRange(mid, leaf.Values.Count - mid);

            // 维护叶节点链表
            newLeaf.Next = leaf.Next;
            leaf.Next = newLeaf;

            // 将新叶节点的第一个 key 提升到父节点
            TKey promoteKey = newLeaf.Keys[0];
            InsertIntoParent(leaf, promoteKey, newLeaf);
        }

        /// <summary>
        /// 分裂内部节点
        /// </summary>
        private void SplitInternal(InternalNode<TKey, TValue> node)
        {
            int degree = Degree;
            var newNode = new InternalNode<TKey, TValue>(degree);

            int mid = node.Keys.Count / 2;
            // 提升的 key (不放入新节点)
            TKey promoteKey = node.Keys[mid];

            // 右半部分的 key 和 children 移到新节点
            newNode.Keys.AddRange(node.Keys.GetRange(mid + 1, node.Keys.Count - mid - 1));
            newNode.Children.AddRange(node.Children.GetRange(mid + 1, node.Children.Count - mid - 1));

            // 缩减原节点
            node.Keys.RemoveRange(mid, node.Keys.Count - mid);
            node.Children.RemoveRange(mid + 1, node.Children.Count - mid - 1);

            InsertIntoParent(node, promoteKey, newNode);
        }

        /// <summary>
        /// 将分裂产生的新节点插入到父节点
        /// </summary>
        private void InsertIntoParent(BPlusTreeNode<TKey, TValue> leftChild, TKey key,
            BPlusTreeNode<TKey, TValue> rightChild)
        {
            if (leftChild == _root)
            {
                // 根节点分裂, 新建根
                var newRoot = new InternalNode<TKey, TValue>(Degree);
                newRoot.Keys.Add(key);
                newRoot.Children.Add(leftChild);
                newRoot.Children.Add(rightChild);
                _root = newRoot;
                return;
            }

            // 找到父节点
            var parent = FindParent(_root, leftChild)!;

            int insertIdx = parent.Keys.BinarySearch(key);
            if (insertIdx < 0) insertIdx = ~insertIdx;
            parent.Keys.Insert(insertIdx, key);
            parent.Children.Insert(insertIdx + 1, rightChild);

            // 父节点溢出则继续向上分裂
            if (parent.Keys.Count > _maxInternalChildren - 1)
            {
                SplitInternal(parent);
            }
        }

        /// <summary>
        /// 在子树中查找目标节点的父节点
        /// </summary>
        private InternalNode<TKey, TValue>? FindParent(BPlusTreeNode<TKey, TValue> current,
            BPlusTreeNode<TKey, TValue> target)
        {
            if (current.IsLeaf) return null;

            var internalNode = (InternalNode<TKey, TValue>)current;
            foreach (var child in internalNode.Children)
            {
                if (child == target)
                    return internalNode;
                var result = FindParent(child, target);
                if (result != null)
                    return result;
            }

            return null;
        }

        // ==================== 修改 ====================

        /// <summary>
        /// 修改指定 key 对应的 value; key 不存在则返回 false
        /// </summary>
        public bool Update(TKey key, TValue newValue)
        {
            var leaf = FindLeaf(key);
            int idx = leaf.Keys.BinarySearch(key);
            if (idx < 0) return false;
            leaf.Values[idx] = newValue;
            return true;
        }

        // ==================== 删除 ====================

        /// <summary>
        /// 删除指定 key
        /// </summary>
        public bool Delete(TKey key)
        {
            var leaf = FindLeaf(key);
            int idx = leaf.Keys.BinarySearch(key);
            if (idx < 0) return false;

            // 从叶节点删除
            leaf.Keys.RemoveAt(idx);
            leaf.Values.RemoveAt(idx);

            if (leaf == _root)
            {
                // 根就是叶节点, 无需额外处理
                return true;
            }

            // 处理下溢
            if (leaf.Keys.Count < _minLeafKeys)
            {
                HandleLeafUnderflow(leaf);
            }

            return true;
        }

        /// <summary>
        /// 处理叶节点下溢
        /// </summary>
        private void HandleLeafUnderflow(LeafNode<TKey, TValue> leaf)
        {
            var parent = (InternalNode<TKey, TValue>)FindParent(_root, leaf)!;
            int childIdx = parent.Children.IndexOf(leaf);

            // 尝试从左兄弟借
            if (childIdx > 0)
            {
                var leftSibling = (LeafNode<TKey, TValue>)parent.Children[childIdx - 1];
                if (leftSibling.Keys.Count > _minLeafKeys)
                {
                    BorrowFromLeftLeaf(leaf, leftSibling, parent, childIdx);
                    return;
                }
            }

            // 尝试从右兄弟借
            if (childIdx < parent.Children.Count - 1)
            {
                var rightSibling = (LeafNode<TKey, TValue>)parent.Children[childIdx + 1];
                if (rightSibling.Keys.Count > _minLeafKeys)
                {
                    BorrowFromRightLeaf(leaf, rightSibling, parent, childIdx);
                    return;
                }
            }

            // 无法借, 合并
            if (childIdx > 0)
            {
                var leftSibling = (LeafNode<TKey, TValue>)parent.Children[childIdx - 1];
                MergeLeaves(leftSibling, leaf, parent, childIdx - 1);
            }
            else
            {
                var rightSibling = (LeafNode<TKey, TValue>)parent.Children[childIdx + 1];
                MergeLeaves(leaf, rightSibling, parent, childIdx);
            }
        }

        private void BorrowFromLeftLeaf(LeafNode<TKey, TValue> leaf,
            LeafNode<TKey, TValue> leftSibling,
            InternalNode<TKey, TValue> parent, int childIdx)
        {
            // 从左兄弟移最后一个元素到当前叶节点开头
            var lastKey = leftSibling.Keys[^1];
            var lastVal = leftSibling.Values[^1];
            leftSibling.Keys.RemoveAt(leftSibling.Keys.Count - 1);
            leftSibling.Values.RemoveAt(leftSibling.Values.Count - 1);

            leaf.Keys.Insert(0, lastKey);
            leaf.Values.Insert(0, lastVal);

            // 更新父节点分隔 key
            parent.Keys[childIdx - 1] = leaf.Keys[0];
        }

        private void BorrowFromRightLeaf(LeafNode<TKey, TValue> leaf,
            LeafNode<TKey, TValue> rightSibling,
            InternalNode<TKey, TValue> parent, int childIdx)
        {
            // 从右兄弟移第一个元素到当前叶节点末尾
            var firstKey = rightSibling.Keys[0];
            var firstVal = rightSibling.Values[0];
            rightSibling.Keys.RemoveAt(0);
            rightSibling.Values.RemoveAt(0);

            leaf.Keys.Add(firstKey);
            leaf.Values.Add(firstVal);

            // 更新父节点分隔 key
            parent.Keys[childIdx] = rightSibling.Keys[0];
        }

        /// <summary>
        /// 合并两个叶节点 (left 吸收 right)
        /// </summary>
        private void MergeLeaves(LeafNode<TKey, TValue> left, LeafNode<TKey, TValue> right,
            InternalNode<TKey, TValue> parent, int leftChildIdx)
        {
            left.Keys.AddRange(right.Keys);
            left.Values.AddRange(right.Values);
            left.Next = right.Next;

            // 从父节点移除分隔 key 和右子节点
            parent.Keys.RemoveAt(leftChildIdx);
            parent.Children.RemoveAt(leftChildIdx + 1);

            // 父节点下溢处理
            if (parent == _root)
            {
                if (parent.Children.Count == 1)
                {
                    _root = parent.Children[0];
                }
            }
            else if (parent.Keys.Count < _minInternalChildren - 1)
            {
                HandleInternalUnderflow(parent);
            }
        }

        /// <summary>
        /// 处理内部节点下溢
        /// </summary>
        private void HandleInternalUnderflow(InternalNode<TKey, TValue> node)
        {
            var parent = (InternalNode<TKey, TValue>)FindParent(_root, node)!;
            int childIdx = parent.Children.IndexOf(node);

            // 尝试从左兄弟借
            if (childIdx > 0)
            {
                var leftSibling = (InternalNode<TKey, TValue>)parent.Children[childIdx - 1];
                if (leftSibling.Keys.Count > _minInternalChildren - 1)
                {
                    BorrowFromLeftInternal(node, leftSibling, parent, childIdx);
                    return;
                }
            }

            // 尝试从右兄弟借
            if (childIdx < parent.Children.Count - 1)
            {
                var rightSibling = (InternalNode<TKey, TValue>)parent.Children[childIdx + 1];
                if (rightSibling.Keys.Count > _minInternalChildren - 1)
                {
                    BorrowFromRightInternal(node, rightSibling, parent, childIdx);
                    return;
                }
            }

            // 合并
            if (childIdx > 0)
            {
                var leftSibling = (InternalNode<TKey, TValue>)parent.Children[childIdx - 1];
                MergeInternal(leftSibling, node, parent, childIdx - 1);
            }
            else
            {
                var rightSibling = (InternalNode<TKey, TValue>)parent.Children[childIdx + 1];
                MergeInternal(node, rightSibling, parent, childIdx);
            }
        }

        private void BorrowFromLeftInternal(InternalNode<TKey, TValue> node,
            InternalNode<TKey, TValue> leftSibling,
            InternalNode<TKey, TValue> parent, int childIdx)
        {
            // 父节点分隔 key 下沉到 node
            node.Keys.Insert(0, parent.Keys[childIdx - 1]);
            // 左兄弟最后一个子节点移过来
            node.Children.Insert(0, leftSibling.Children[^1]);
            leftSibling.Children.RemoveAt(leftSibling.Children.Count - 1);

            // 左兄弟最后一个 key 提升到父节点
            parent.Keys[childIdx - 1] = leftSibling.Keys[^1];
            leftSibling.Keys.RemoveAt(leftSibling.Keys.Count - 1);
        }

        private void BorrowFromRightInternal(InternalNode<TKey, TValue> node,
            InternalNode<TKey, TValue> rightSibling,
            InternalNode<TKey, TValue> parent, int childIdx)
        {
            // 父节点分隔 key 下沉到 node
            node.Keys.Add(parent.Keys[childIdx]);
            // 右兄弟第一个子节点移过来
            node.Children.Add(rightSibling.Children[0]);
            rightSibling.Children.RemoveAt(0);

            // 右兄弟第一个 key 提升到父节点
            parent.Keys[childIdx] = rightSibling.Keys[0];
            rightSibling.Keys.RemoveAt(0);
        }

        private void MergeInternal(InternalNode<TKey, TValue> left,
            InternalNode<TKey, TValue> right,
            InternalNode<TKey, TValue> parent, int leftChildIdx)
        {
            // 父节点分隔 key 下沉
            left.Keys.Add(parent.Keys[leftChildIdx]);
            // 合并 right
            left.Keys.AddRange(right.Keys);
            left.Children.AddRange(right.Children);

            // 从父节点移除
            parent.Keys.RemoveAt(leftChildIdx);
            parent.Children.RemoveAt(leftChildIdx + 1);

            if (parent == _root)
            {
                if (parent.Children.Count == 1)
                {
                    _root = parent.Children[0];
                }
            }
            else if (parent.Keys.Count < _minInternalChildren - 1)
            {
                HandleInternalUnderflow(parent);
            }
        }

        // ==================== 打印 / 验证 ====================

        /// <summary>
        /// 以树形结构打印 B+ 树
        /// </summary>
        public void Print()
        {
            Console.WriteLine($"=== B+ Tree (Degree={Degree}) ===");
            PrintNode(_root, 0);
            Console.WriteLine();
            Console.Write("Leaf chain: ");
            PrintLeafChain();
        }

        private void PrintNode(BPlusTreeNode<TKey, TValue> node, int indent)
        {
            string prefix = new string(' ', indent * 4);

            if (node.IsLeaf)
            {
                var leaf = (LeafNode<TKey, TValue>)node;
                Console.Write($"{prefix}[Leaf] ");
                for (int i = 0; i < leaf.Keys.Count; i++)
                {
                    Console.Write($"{leaf.Keys[i]}={leaf.Values[i]}");
                    if (i < leaf.Keys.Count - 1) Console.Write(", ");
                }

                Console.WriteLine();
            }
            else
            {
                var internalNode = (InternalNode<TKey, TValue>)node;
                Console.Write($"{prefix}[Internal] keys: ");
                Console.WriteLine(string.Join(", ", internalNode.Keys));
                foreach (var child in internalNode.Children)
                {
                    PrintNode(child, indent + 1);
                }
            }
        }

        private void PrintLeafChain()
        {
            var leaf = _firstLeaf;
            while (leaf != null)
            {
                Console.Write("[");
                for (int i = 0; i < leaf.Keys.Count; i++)
                {
                    Console.Write($"{leaf.Keys[i]}={leaf.Values[i]}");
                    if (i < leaf.Keys.Count - 1) Console.Write(", ");
                }

                Console.Write("]");
                if (leaf.Next != null) Console.Write(" -> ");
                leaf = leaf.Next;
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 获取所有键值对 (按序遍历叶节点链表)
        /// </summary>
        public List<(TKey Key, TValue Value)> GetAll()
        {
            var result = new List<(TKey, TValue)>();
            var leaf = _firstLeaf;
            while (leaf != null)
            {
                for (int i = 0; i < leaf.Keys.Count; i++)
                    result.Add((leaf.Keys[i], leaf.Values[i]));
                leaf = leaf.Next;
            }

            return result;
        }
    }

    public class BinaryTree : IComparable, IComparer<BinaryTree>
    {
        public BinaryTree? Father;
        public BinaryTree? Left;
        public BinaryTree? Right;
        public object? Value;
        public long Level;
        public Bits? HuffmanBits;

        public BinaryTree()
        {
            //HuffmanBits = new Bits();
        }

        public int Compare(BinaryTree? x, BinaryTree? y)
        {
            return x.Level.CompareTo(y.Level);
        }

        public delegate void OnEach(BinaryTree now, bool IsLeaf);

        public event OnEach? OnEachDo;

        /// <summary>
        /// 采用中，左，右顺序(并非如此,反正是先中间，再左边，左边完了到右边
        /// </summary>
        public void Foreach()
        {
            Pfc(this);
        }

        void Pfc(BinaryTree t)
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

        public BinaryTree GetLastLeftNode()
        {
            return LeftNode(this);
        }

        public BinaryTree GetLastRightNode()
        {
            return RightNode(this);
        }

        public BinaryTree LeftNode(BinaryTree t)
        {
            if (t.Left != null)
                return LeftNode(t.Left);
            else
                return t;
        }

        public BinaryTree RightNode(BinaryTree t)
        {
            if (t.Right != null)
                return RightNode(t.Right);
            else
                return t;
        }

        public int CompareTo(object? obj)
        {
            return Level.CompareTo(((BinaryTree)obj).Level);
        }
    }
}