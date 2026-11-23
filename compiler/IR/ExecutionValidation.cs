using System;
using System.Collections.Generic;

namespace Lyric;

internal sealed partial class IrProgram
{
    internal partial void ValidateExecution()
    {
        // Sets describe executed definitions, not initialized storage or live owners.
        // Branch-local definitions never escape; their explicit If result does.
        // Use a work stack rather than recursive descent for deep source trees.
        for (int f = 0; f < FunctionCount(); f += 1)
        {
            Stack<Action> work = new();
            Dictionary<IrNode, List<HashSet<IrNode>>> breaks = new();
            Dictionary<IrNode, HashSet<IrNode>> loopTransfers = new();
            Visit(FunctionAt(f).Body!, new HashSet<IrNode>(), new HashSet<IrNode>(), _ => { });
            while (work.Count != 0)
            {
                work.Pop()();
            }

            void Visit(IrNode region, HashSet<IrNode> available,
                HashSet<IrNode> pending, Action<bool> finished)
            {
                // Pending Move results are obligations, not owning storage slots.
                HashSet<IrNode> inherited = new(pending);
                int index = 0;
                work.Push(Next);
                void Next()
                {
                    if (index == region.ChildCount())
                    {
                        // Only a loop body may fall through to its backedge.
                        Require(region.Parent?.Kind == IrKind.While && region.Role == IrRole.Body,
                            "reachable region exit lacks Return or Yield");
                        Require(pending.SetEquals(loopTransfers[region.Parent!]),
                            "loop backedge changes pending transfers");
                        work.Push(() => finished(true));
                        return;
                    }
                    IrNode node = region.ChildAt(index);
                    index += 1;
                    for (int i = 0; i < node.OperandCount(); i += 1)
                    {
                        Require(available.Contains(node.OperandAt(i).Definition!),
                            "operand definition has not executed");
                        if (node.OperandAt(i).Definition!.Kind == IrKind.Move)
                        {
                            Require(pending.Remove(node.OperandAt(i).Definition!),
                                "transfer has not executed or was already consumed");
                        }
                    }
                    if (node.Kind == IrKind.If)
                    {
                        bool firstContinues = false;
                        HashSet<IrNode> firstPending = new(pending);
                        HashSet<IrNode> secondPending = new(pending);
                        Visit(node.ChildAt(0), new HashSet<IrNode>(available), firstPending, first =>
                        {
                            firstContinues = first;
                            Visit(node.ChildAt(1), new HashSet<IrNode>(available), secondPending, second =>
                            {
                                if (firstContinues || second)
                                {
                                    pending.Clear();
                                    if (firstContinues)
                                    {
                                        pending.UnionWith(firstPending);
                                    }
                                    if (second)
                                    {
                                        pending.UnionWith(secondPending);
                                    }
                                    available.Add(node);
                                    work.Push(Next);
                                }
                                else
                                {
                                    work.Push(() => finished(false));
                                }
                            });
                        });
                        return;
                    }
                    if (node.Kind == IrKind.While)
                    {
                        // A condition executes first, but a break inside it can skip
                        // later definitions. Intersect every exit, never just ancestry.
                        HashSet<IrNode> condition = new(available);
                        List<HashSet<IrNode>> exits = new();
                        breaks.Add(node, exits);
                        loopTransfers.Add(node, new HashSet<IrNode>(pending));
                        HashSet<IrNode> conditionPending = new(pending);
                        Visit(node.ChildAt(0), condition, conditionPending, continues =>
                        {
                            if (continues)
                            {
                                // A repeated condition cannot consume an outer transfer
                                // once and then consume it again on another iteration.
                                Require(conditionPending.SetEquals(loopTransfers[node]),
                                    "loop condition changes pending outer transfers");
                                exits.Add(new HashSet<IrNode>(condition));
                                Visit(node.ChildAt(1), new HashSet<IrNode>(condition),
                                    new HashSet<IrNode>(conditionPending), _ => CompleteLoop());
                            }
                            else
                            {
                                CompleteLoop();
                            }
                        });
                        return;

                        void CompleteLoop()
                        {
                            breaks.Remove(node);
                            loopTransfers.Remove(node);
                            if (exits.Count == 0)
                            {
                                work.Push(() => finished(false));
                                return;
                            }
                            // Body-private definitions must not escape even if one
                            // break happens to execute them. Condition definitions may.
                            foreach (HashSet<IrNode> exit in exits)
                            {
                                condition.IntersectWith(exit);
                            }
                            available.UnionWith(condition);
                            work.Push(Next);
                        }
                    }
                    if (node.Kind is IrKind.Break or IrKind.Continue)
                    {
                        Require(pending.SetEquals(loopTransfers[node.Target!]),
                            "loop transfer abandons or consumes pending transfers");
                        if (node.Kind == IrKind.Break)
                        {
                            breaks[node.Target!].Add(new HashSet<IrNode>(available));
                        }
                        work.Push(() => finished(false));
                        return;
                    }
                    if (node.Kind is IrKind.Return or IrKind.Yield)
                    {
                        // Generated cleanup may follow a transfer syntactically. It is
                        // unreachable and must not make definitions available elsewhere.
                        Require(node.Kind == IrKind.Return ? pending.Count == 0 : pending.IsSubsetOf(inherited),
                            "region exit abandons an unconsumed transfer");
                        work.Push(() => finished(node.Kind == IrKind.Yield));
                        return;
                    }
                    if (node.Kind == IrKind.Move)
                    {
                        Require(pending.Add(node), "execution overwrites a pending transfer");
                    }
                    if (node.Result != IrResult.None)
                    {
                        available.Add(node);
                    }
                    work.Push(Next);
                }
            }
        }

        static void Require(bool valid, string reason)
        {
            if (!valid)
            {
                throw new InvalidOperationException("Invalid IR execution: " + reason + ".");
            }
        }
    }
}
