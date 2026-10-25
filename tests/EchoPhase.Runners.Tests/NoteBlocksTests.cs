// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Notes;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Tests
{
    /// <summary>The note blocks and Notify against in-memory stand-ins.</summary>
    public class NoteBlocksTests : IAsyncDisposable
    {
        private readonly MemoryNotes _notes = new();
        private readonly MemoryNotifier _notifier = new();
        private readonly RunnerHarness _harness;

        public NoteBlocksTests()
        {
            _harness = new RunnerHarness(services => services
                .AddSingleton<INoteBlockStore>(_notes)
                .AddSingleton<IBlockNotifier>(_notifier));
        }

        public ValueTask DisposeAsync() => _harness.DisposeAsync();

        private Task<Blocks.Contexts.IBlockExecutionContext> Run(string blocks, string connections, object? input = null)
            => _harness.RunAsync($$"""{ "blocks": [ {{blocks}} ], "connections": [ {{connections}} ] }""", input: input);

        [Fact]
        public async Task CreateThenAppend_KeepsALog()
        {
            var created = await Run("""
                { "id": 1, "type": "CreateNote", "params": { "title": "Log", "body": "started @input.n" } },
                { "id": 2, "type": "EditNote", "params": { "id": "@input.id", "body": "more", "mode": "Append" } },
                { "id": 3, "type": "Print", "params": { "text": "@input.body" } }
                """, """{ "from": 1, "to": 2 }, { "from": 2, "to": 3 }""", new Dictionary<string, object?> { ["n"] = 1 });

            Assert.Empty(created.Errors);
            Assert.Equal(["started 1\n\nmore"], created.Output);
            Assert.Equal(_harness.UserId, Assert.Single(_notes.Notes.Values).Owner);
        }

        [Fact]
        public async Task IfNoteExists_BranchesByTitle()
        {
            await _notes.CreateAsync(_harness.UserId, "Daily", "x", false);
            const string Blocks = """
                { "id": 1, "type": "IfNoteExists", "params": { "title": "@input" } },
                { "id": 2, "type": "Print", "params": { "text": "yes @input.title" } },
                { "id": 3, "type": "Print", "params": { "text": "no @input" } }
                """;
            const string Connections = """{ "from": 1, "to": 2, "output": "true" }, { "from": 1, "to": 3, "output": "false" }""";

            Assert.Equal(["yes Daily"], (await Run(Blocks, Connections, "daily")).Output);
            Assert.Equal(["no Weekly"], (await Run(Blocks, Connections, "Weekly")).Output);
        }

        [Fact]
        public async Task GetNote_FailsWhenThereIsNone()
        {
            var context = await Run("""
                { "id": 1, "type": "GetNote", "params": { "id": "@input" } },
                { "id": 2, "type": "Print", "params": { "text": "@input.title" } }
                """, """{ "from": 1, "to": 2 }""", Guid.NewGuid().ToString());

            Assert.NotEmpty(context.Errors);
            Assert.Empty(context.Output);
        }

        [Fact]
        public async Task DeleteNote_SaysWhetherThereWasOne()
        {
            var note = await _notes.CreateAsync(_harness.UserId, "Old", "x", false);
            const string Blocks = """
                { "id": 1, "type": "DeleteNote", "params": { "id": "@input" } },
                { "id": 2, "type": "Print", "params": { "text": "@input" } }
                """;

            Assert.Equal(["true"], (await Run(Blocks, """{ "from": 1, "to": 2 }""", note.Id.ToString())).Output);
            Assert.Equal(["false"], (await Run(Blocks, """{ "from": 1, "to": 2 }""", note.Id.ToString())).Output);
        }

        [Fact]
        public async Task Notify_TellsTheOwner_AndPassesItsInputOn()
        {
            var context = await Run("""
                { "id": 1, "type": "Notify", "params": { "title": "Done: @input", "link": "/notes" } },
                { "id": 2, "type": "Print", "params": { "text": "@input" } }
                """, """{ "from": 1, "to": 2 }""", "build");

            Assert.Equal(["build"], context.Output);
            Assert.Equal((_harness.UserId, "Done: build", "/notes"), Assert.Single(_notifier.Sent));
        }

        [Fact]
        public void Validation_NeedsAnIdOrATitle()
        {
            var program = RunnerHarness.Parse("""
                { "blocks": [ { "id": 1, "type": "GetNote", "params": { } } ], "connections": [] }
                """);

            Assert.NotEmpty(_harness.Validator.Validate(program, maxBlocks: 200));
        }

        private sealed class MemoryNotes : INoteBlockStore
        {
            public Dictionary<Guid, (Guid Owner, BlockNote Note)> Notes { get; } = [];

            public Task<BlockNote> CreateAsync(Guid userId, string title, string body, bool pinned, CancellationToken ct = default)
            {
                var note = new BlockNote(Guid.NewGuid(), title, body, pinned, DateTime.UtcNow, DateTime.UtcNow);
                Notes[note.Id] = (userId, note);
                return Task.FromResult(note);
            }

            public Task<BlockNote?> GetAsync(Guid userId, Guid noteId, CancellationToken ct = default)
                => Task.FromResult(Notes.TryGetValue(noteId, out var e) && e.Owner == userId ? e.Note : null);

            public Task<BlockNote?> FindByTitleAsync(Guid userId, string title, CancellationToken ct = default)
                => Task.FromResult(Notes.Values
                    .Where(e => e.Owner == userId && string.Equals(e.Note.Title, title, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Note).FirstOrDefault());

            public Task<BlockNote> UpdateAsync(Guid userId, Guid noteId, string? title, string? body, bool? pinned,
                CancellationToken ct = default)
            {
                var note = Notes[noteId].Note;
                note = note with { Title = title ?? note.Title, Body = body ?? note.Body, Pinned = pinned ?? note.Pinned };
                Notes[noteId] = (userId, note);
                return Task.FromResult(note);
            }

            public Task<BlockNote> AppendAsync(Guid userId, Guid noteId, string text, CancellationToken ct = default)
                => UpdateAsync(userId, noteId, null, $"{Notes[noteId].Note.Body}\n\n{text}", null, ct);

            public Task<bool> DeleteAsync(Guid userId, Guid noteId, CancellationToken ct = default)
                => Task.FromResult(Notes.Remove(noteId));
        }

        private sealed class MemoryNotifier : IBlockNotifier
        {
            public List<(Guid User, string Title, string Link)> Sent { get; } = [];

            public Task<Guid> NotifyAsync(Guid userId, string title, string body, string link, CancellationToken ct = default)
            {
                Sent.Add((userId, title, link));
                return Task.FromResult(Guid.NewGuid());
            }
        }
    }
}
