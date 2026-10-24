// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using Cronos;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Validation
{
    /// <summary>A problem with a program; <see cref="BlockId"/> is null when it is about the whole program.</summary>
    public sealed record ProgramIssue(int? BlockId, string Message);

    /// <summary>
    /// Checks a program before it is saved or run, so the runner meets only
    /// sound graphs: known types, valid params, edges between existing
    /// blocks through outputs their types have, and no cycles (loops are
    /// For/While bodies, not edges back).
    /// </summary>
    public sealed class BlockProgramValidator
    {
        public const int MaxTimeoutSeconds = 3600;
        public const int MaxRetries = 10;
        public const int MaxRetryDelayMs = 60_000;

        private readonly BlockTypeRegistry _registry;

        public BlockProgramValidator(BlockTypeRegistry registry)
        {
            _registry = registry;
        }

        public IReadOnlyList<ProgramIssue> Validate(BlockProgram program, int maxBlocks)
        {
            var issues = new List<ProgramIssue>();
            void Fail(int? id, string message) => issues.Add(new ProgramIssue(id, message));

            if (program.Blocks.Count == 0)
                Fail(null, "At least one block is required.");
            if (program.Blocks.Count > maxBlocks)
                Fail(null, $"A program may have at most {maxBlocks} blocks.");

            var types = new Dictionary<int, (Block Block, BlockTypeDescriptor? Type)>();
            var bound = new Dictionary<int, object>();
            foreach (var block in program.Blocks)
            {
                if (block.Id < 0)
                    Fail(block.Id, "Block ids must not be negative.");
                if (!types.TryAdd(block.Id, (block, null)))
                {
                    Fail(block.Id, $"Block id {block.Id} is used more than once.");
                    continue;
                }

                var descriptor = _registry.Find(block.Type, block.Version);
                if (descriptor is null)
                {
                    Fail(block.Id, $"Unknown block type {block.Type} version {block.Version}.");
                    continue;
                }
                types[block.Id] = (block, descriptor);

                try
                {
                    var parameters = bound[block.Id] = BlockParamsBinder.Bind(descriptor.ParamsType, block.Params);
                    if (parameters is ScheduleTriggerParams schedule && !IsCron(schedule.Cron))
                        Fail(block.Id, $"'{schedule.Cron}' is not a five-field cron expression.");
                    foreach (var problem in ScriptProblems(parameters))
                        Fail(block.Id, problem);
                }
                catch (Exception ex) when (ex is InvalidCastException or InvalidOperationException)
                {
                    Fail(block.Id, ex.Message);
                }

                if (block.Retry is { } retry
                    && (retry.MaxTries is < 1 or > MaxRetries || retry.DelayMs is < 0 or > MaxRetryDelayMs))
                    Fail(block.Id, $"Retry needs 1-{MaxRetries} tries and a delay of 0-{MaxRetryDelayMs} ms.");
            }

            var seen = new HashSet<BlockConnection>();
            foreach (var c in program.Connections)
            {
                if (!seen.Add(c))
                    Fail(c.From, $"Connection {c.From} -{c.Output}-> {c.To} is listed twice.");

                if (!types.TryGetValue(c.From, out var from))
                    Fail(null, $"Connection from block {c.From}, which does not exist.");
                else if (from.Type is { } type && !Outputs(from.Block, type, bound.GetValueOrDefault(c.From)).Contains(c.Output))
                    Fail(c.From, $"{from.Block.Type} has no output \"{c.Output}\" (it has {string.Join(", ", Outputs(from.Block, type, bound.GetValueOrDefault(c.From)))}).");

                if (!types.TryGetValue(c.To, out var to))
                    Fail(null, $"Connection to block {c.To}, which does not exist.");
                else if (to.Type is { IsTrigger: true })
                    Fail(c.To, $"{to.Block.Type} starts runs; it cannot have inputs.");
            }

            if (FindCycle(program) is { } cycle)
                Fail(cycle, $"Block {cycle} is part of a cycle; repeat work with For or While instead.");

            foreach (var id in program.StartIds)
                if (!types.ContainsKey(id))
                    Fail(null, $"Start block {id} does not exist.");

            var settings = program.Settings;
            if (settings.TimeoutSeconds is < 1 or > MaxTimeoutSeconds)
                Fail(null, $"TimeoutSeconds must be between 1 and {MaxTimeoutSeconds}.");
            if (settings.TimeZone is { } zone && !IsIanaTimeZone(zone))
                Fail(null, $"'{zone}' is not an IANA time zone, e.g. 'Europe/Moscow'.");

            return issues;
        }

        /// <summary>
        /// Where a manual run starts: the program's StartIds, else its
        /// ManualTrigger blocks, else every block without inputs that does not
        /// wait for an event (webhook, schedule).
        /// </summary>
        public IReadOnlyCollection<int> ManualStartIds(BlockProgram program)
        {
            if (program.StartIds.Count > 0)
                return program.StartIds;

            var manual = program.Blocks.Where(b => b.Type == BlockTypes.ManualTrigger).Select(b => b.Id).ToList();
            if (manual.Count > 0)
                return manual;

            var withInputs = program.Connections.Select(c => c.To).ToHashSet();
            return program.Blocks
                .Where(b => !withInputs.Contains(b.Id)
                            && _registry.Find(b.Type, b.Version) is not { IsTrigger: true })
                .Select(b => b.Id)
                .ToList();
        }

        // Expressions, templates and paths parse before the program runs, so a
        // typo is reported on save with its position, not mid-run.
        private static IEnumerable<string> ScriptProblems(object bound)
        {
            var checks = bound switch
            {
                IfParams p => [("condition", Eval.CheckExpression(p.Condition))],
                WhileParams p => [("condition", Eval.CheckExpression(p.Condition))],
                PrintParams p => [("text", Eval.CheckTemplate(p.Text))],
                StopAndErrorParams p => [("message", Eval.CheckTemplate(p.Message))],
                SwitchParams p => p.Rules.Select((r, i) => ($"rules[{i}].condition", Eval.CheckExpression(r.Condition))).ToArray(),
                ExecuteWorkflowParams p => [("input", BlockValues.Check(p.Input))],
                RespondToWebhookParams p => [("body", BlockValues.Check(p.Body)),
                    .. p.Headers.Select(h => ($"headers.{h.Key}", Eval.CheckTemplate(h.Value)))],
                StorageParams p => [("key", p.Key.Length > 0 ? Eval.CheckTemplate(p.Key) : null),
                    ("value", BlockValues.Check(p.Value)), ("default", BlockValues.Check(p.Default)),
                    ("prefix", p.Prefix is { } prefix ? Eval.CheckTemplate(prefix) : null),
                    ("after", p.After is { } after ? Eval.CheckTemplate(after) : null)],
                HttpRequestParams p => [("url", Eval.CheckTemplate(p.Url)), ("body", BlockValues.Check(p.Body)),
                    .. p.Headers.Select(h => ($"headers.{h.Key}", Eval.CheckTemplate(h.Value))),
                    .. p.Query.Select(q => ($"query.{q.Key}", Eval.CheckTemplate(q.Value)))],
                SetParams p => new (string, string?)[]
                {
                    ("name", Eval.CheckPath(p.Name)),
                    ("value", p.Value.ValueKind == JsonValueKind.String ? Eval.CheckTemplate(p.Value.GetString() ?? "") : null)
                },
                ForParams p => [("counterVar", Eval.CheckPath(p.CounterVar))],
                DiscordGetUserGuildsParams { Name: { Length: > 0 } name } => [("name", Eval.CheckPath(name))],
                TwitchGetVipsParams { Name: { Length: > 0 } name } => [("name", Eval.CheckPath(name))],
                _ => Array.Empty<(string, string?)>()
            };

            foreach (var (field, problem) in checks)
                if (problem is not null)
                    yield return $"{field}: {problem}";
        }

        public static bool IsCron(string expression)
            => CronExpression.TryParse(expression, CronFormat.Standard, out _);

        public static bool IsIanaTimeZone(string id)
            => id.Length <= 64 && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && zone.HasIanaId;

        // The type's outputs, those its params add (Switch rules), and the
        // error one. Continue goes through the first output, "main" for a type
        // with none (StopAndError).
        private static IEnumerable<string> Outputs(Block block, BlockTypeDescriptor type, object? parameters)
        {
            IEnumerable<string> outputs = parameters is IHasOutputs extra ? [.. extra.ExtraOutputs, .. type.Outputs] : type.Outputs;
            return block.OnError switch
            {
                BlockErrorPolicy.ErrorOutput => [.. outputs, BlockOutputs.Error],
                BlockErrorPolicy.Continue when type.Outputs.Count == 0 => [type.FirstOutput],
                _ => outputs
            };
        }

        // Iterative DFS (programs may be deep): returns a block on a cycle, or null.
        private static int? FindCycle(BlockProgram program)
        {
            var edges = program.Connections
                .GroupBy(c => c.From)
                .ToDictionary(g => g.Key, g => g.Select(c => c.To).Distinct().ToList());
            var state = new Dictionary<int, int>(); // 1 = on the path, 2 = done

            foreach (var root in program.Blocks.Select(b => b.Id))
            {
                if (state.ContainsKey(root))
                    continue;

                var stack = new Stack<(int Id, int Next)>();
                stack.Push((root, 0));
                state[root] = 1;
                while (stack.Count > 0)
                {
                    var (id, next) = stack.Pop();
                    var targets = edges.GetValueOrDefault(id);
                    if (targets is not null && next < targets.Count)
                    {
                        stack.Push((id, next + 1));
                        var target = targets[next];
                        if (state.GetValueOrDefault(target) == 1)
                            return target;
                        if (!state.ContainsKey(target))
                        {
                            state[target] = 1;
                            stack.Push((target, 0));
                        }
                    }
                    else
                    {
                        state[id] = 2;
                    }
                }
            }

            return null;
        }
    }
}
