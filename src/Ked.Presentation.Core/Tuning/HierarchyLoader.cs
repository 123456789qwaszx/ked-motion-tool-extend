using System;
using System.Text;

namespace Ked.Presentation.Core
{
    /// <summary>
    /// 오브젝트 계층(HierarchyDto) → RectNodeTree.
    /// 노드 이름·부모 관계·값은 전부 인자에서 온다 — 여기가 아는 것은 루트의 키와 무대의 pivot(가운데)뿐이다.
    /// </summary>
    public static class HierarchyLoader
    {
        /// <summary>계층의 루트 노드의 키.</summary>
        public const string RootKey = "__root";

        /// <summary>
        /// 이름으로 계층 하나를 골라 트리로 만든다. keyPrefix는 노드 키 앞에 붙는다.
        /// </summary>
        public static RectNodeTree BuildTree(
            HierarchyFileDto file,
            string name,
            string keyPrefix = "")
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if (file.hierarchies == null || file.hierarchies.Count == 0)
                throw new ArgumentException("계층이 하나도 없다.", nameof(file));

            if (file.stageSize == null)
                throw new ArgumentException("무대의 크기(stageSize)가 없다.", nameof(file));

            HierarchyDto hierarchy = Find(file, name);

            // 무대는 가운데 pivot이다.
            RectSpace space = new(file.stageSize.ToVec2(), Vec2.Half);

            return BuildTree(hierarchy, space, keyPrefix);
        }

        /// <summary>계층 하나를 지정한 공간 밑에 세운다. 노드는 적힌 순서대로(부모 먼저) 들어간다.</summary>
        public static RectNodeTree BuildTree(
            HierarchyDto hierarchy,
            RectSpace rootSpace,
            string keyPrefix = "")
        {
            RectNodeTree tree = new(rootSpace);
            AddTo(tree, hierarchy, keyPrefix, attachParentKey: null);
            return tree;
        }

        /// <summary>
        /// 이미 있는 트리에 계층을 붙인다.
        /// attachParentKey가 null이면 트리의 루트 공간 직속이다.
        /// </summary>
        public static void AddTo(
            RectNodeTree tree,
            HierarchyDto hierarchy,
            string keyPrefix = "",
            string attachParentKey = null)
        {
            if (tree == null)
                throw new ArgumentNullException(nameof(tree));

            if (hierarchy == null)
                throw new ArgumentNullException(nameof(hierarchy));

            if (hierarchy.nodes == null || hierarchy.nodes.Count == 0)
                throw new ArgumentException($"계층 '{hierarchy.name}'에 노드가 없다.", nameof(hierarchy));

            keyPrefix ??= "";

            for (int i = 0; i < hierarchy.nodes.Count; i++)
            {
                HierarchyNodeDto node = hierarchy.nodes[i];

                if (node == null || string.IsNullOrEmpty(node.id))
                    throw new ArgumentException($"계층 '{hierarchy.name}'의 노드 [{i}]에 id가 없다.", nameof(hierarchy));

                string key = keyPrefix + node.id;

                // 빈 parent = 루트 직속. 루트 자신(__root)은 parent가 비어 있고
                // 부착 지점(없으면 루트 공간 직속)에 붙는다.
                string parentKey = string.IsNullOrEmpty(node.parent)
                    ? attachParentKey
                    : keyPrefix + node.parent;

                tree.Add(key, parentKey, ToState(node, hierarchy.name));
            }
        }

        /// <summary>노드 → 상태 값. 필드 해석은 여기 한 곳뿐이다.</summary>
        public static RectNodeState ToState(HierarchyNodeDto node, string context = null)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            // 빈 필드를 0으로 떨어뜨리지 않는다.
            if (node.anchoredPosition == null || node.anchorMin == null || node.anchorMax == null ||
                node.pivot == null || node.sizeDelta == null ||
                node.localScale == null || node.localEulerAngles == null)
            {
                throw new ArgumentException(
                    $"노드 '{node.id}'(계층 {context ?? "?"})에 빈 트랜스폼 필드가 있다.", nameof(node));
            }

            return new RectNodeState(
                anchoredPosition: node.anchoredPosition.ToVec2(),
                anchorMin: node.anchorMin.ToVec2(),
                anchorMax: node.anchorMax.ToVec2(),
                pivot: node.pivot.ToVec2(),
                sizeDelta: node.sizeDelta.ToVec2(),
                localScale: node.localScale.ToVec3(),
                localEulerAngles: node.localEulerAngles.ToVec3());
        }

        private static HierarchyDto Find(HierarchyFileDto file, string name)
        {
            for (int i = 0; i < file.hierarchies.Count; i++)
            {
                if (string.Equals(file.hierarchies[i]?.name, name, StringComparison.Ordinal))
                    return file.hierarchies[i];
            }

            // 못 찾으면 무엇이 있었는지까지 말한다 — "비슷한 이름"으로 잇지 않는다.
            StringBuilder available = new();

            for (int i = 0; i < file.hierarchies.Count; i++)
            {
                if (i > 0)
                    available.Append(", ");

                available.Append(file.hierarchies[i]?.name ?? "(null)");
            }

            throw new ArgumentException(
                $"'{name}'라는 계층이 없다. 있는 것: [{available}]", nameof(name));
        }
    }
}
