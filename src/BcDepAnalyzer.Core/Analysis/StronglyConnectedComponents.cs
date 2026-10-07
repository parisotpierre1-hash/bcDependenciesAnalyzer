namespace BcDepAnalyzer.Core.Analysis;

internal static class StronglyConnectedComponents
{
    // Iterative Tarjan. Components are returned in emission order: with edges "depends on",
    // every component appears after all components it depends on. Members are sorted ascending.
    public static List<int[]> Find(int[][] adjacency)
    {
        var count = adjacency.Length;
        var index = new int[count];
        Array.Fill(index, -1);
        var low = new int[count];
        var onStack = new bool[count];
        var stack = new Stack<int>();
        var callStack = new Stack<(int Node, int NextEdge)>();
        var components = new List<int[]>();
        var counter = 0;

        void Visit(int node)
        {
            index[node] = low[node] = counter++;
            stack.Push(node);
            onStack[node] = true;
            callStack.Push((node, 0));
        }

        for (var root = 0; root < count; root++)
        {
            if (index[root] != -1)
            {
                continue;
            }

            Visit(root);

            while (callStack.Count > 0)
            {
                var (node, nextEdge) = callStack.Pop();

                if (nextEdge < adjacency[node].Length)
                {
                    callStack.Push((node, nextEdge + 1));
                    var target = adjacency[node][nextEdge];

                    if (index[target] == -1)
                    {
                        Visit(target);
                    }
                    else if (onStack[target])
                    {
                        low[node] = Math.Min(low[node], index[target]);
                    }

                    continue;
                }

                if (low[node] == index[node])
                {
                    var members = new List<int>();
                    int member;
                    do
                    {
                        member = stack.Pop();
                        onStack[member] = false;
                        members.Add(member);
                    }
                    while (member != node);

                    members.Sort();
                    components.Add([.. members]);
                }

                if (callStack.Count > 0)
                {
                    var parent = callStack.Peek().Node;
                    low[parent] = Math.Min(low[parent], low[node]);
                }
            }
        }

        return components;
    }
}
