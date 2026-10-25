// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Notes;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>What the note blocks share: the store, templates, finding the note.</summary>
    internal static class NoteBlocks
    {
        public static INoteBlockStore Store(IBlockExecutionContext context)
            => context.Services.GetService<INoteBlockStore>()
               ?? throw new InvalidOperationException("Notes are not available here.");

        public static string Text(string? template, BlockCall call)
            => string.IsNullOrEmpty(template) ? string.Empty : Eval.TypedProcess<string>(template, call.Scope);

        /// <summary>The note the params point at, or null when there is none.</summary>
        public static async Task<BlockNote?> FindAsync(NoteRefParams param, BlockCall call)
        {
            var context = call.Context;
            var store = Store(context);
            if (!string.IsNullOrWhiteSpace(param.Id))
            {
                var id = Text(param.Id, call).Trim();
                if (!Guid.TryParse(id, out var noteId))
                    throw new InvalidOperationException($"'{id}' is not a note id.");
                return await store.GetAsync(context.UserId, noteId, context.CancellationToken);
            }

            var title = Text(param.Title, call).Trim();
            if (title.Length == 0)
                throw new InvalidOperationException("The note's title came out empty.");
            return await store.FindByTitleAsync(context.UserId, title, context.CancellationToken);
        }

        public static async Task<BlockNote> RequireAsync(NoteRefParams param, BlockCall call)
            => await FindAsync(param, call) ?? throw new InvalidOperationException("Note not found.");
    }

    [BlockType(BlockTypes.CreateNote,
        Description = "Creates a note of the workflow's owner (Markdown); output: the note.")]
    public class CreateNoteHandler : BlockHandlerBase<CreateNoteParams>
    {
        public override async Task<BlockResult> ExecuteAsync(BlockCall call, CreateNoteParams param)
        {
            var context = call.Context;
            var note = await NoteBlocks.Store(context).CreateAsync(context.UserId,
                NoteBlocks.Text(param.Title, call), NoteBlocks.Text(param.Body, call), param.Pinned,
                context.CancellationToken);
            return BlockResult.Main(note.ToOutput());
        }
    }

    [BlockType(BlockTypes.GetNote,
        Description = "Reads a note by id or title; output: the note. Fails when there is none (see IfNoteExists).")]
    public class GetNoteHandler : BlockHandlerBase<GetNoteParams>
    {
        public override async Task<BlockResult> ExecuteAsync(BlockCall call, GetNoteParams param)
            => BlockResult.Main((await NoteBlocks.RequireAsync(param, call)).ToOutput());
    }

    [BlockType(BlockTypes.IfNoteExists, Outputs = [BlockOutputs.True, BlockOutputs.False],
        Description = "Goes on through \"true\" with the note when it exists, else through \"false\" with its input.")]
    public class IfNoteExistsHandler : BlockHandlerBase<IfNoteExistsParams>
    {
        public override async Task<BlockResult> ExecuteAsync(BlockCall call, IfNoteExistsParams param)
        {
            var note = await NoteBlocks.FindAsync(param, call);
            return note is null
                ? BlockResult.To(BlockOutputs.False, call.Input)
                : BlockResult.To(BlockOutputs.True, note.ToOutput());
        }
    }

    [BlockType(BlockTypes.EditNote,
        Description = "Changes a note: a new title, its text replaced or appended to (a log), pinned; output: the note.")]
    public class EditNoteHandler : BlockHandlerBase<EditNoteParams>
    {
        public override async Task<BlockResult> ExecuteAsync(BlockCall call, EditNoteParams param)
        {
            var context = call.Context;
            var store = NoteBlocks.Store(context);
            var note = await NoteBlocks.RequireAsync(param, call);
            var ct = context.CancellationToken;

            if (param.Mode == EditNoteMode.Append)
            {
                note = await store.AppendAsync(context.UserId, note.Id, NoteBlocks.Text(param.Body, call), ct);
                if (param.NewTitle is not null || param.Pinned is not null)
                    note = await store.UpdateAsync(context.UserId, note.Id,
                        param.NewTitle is null ? null : NoteBlocks.Text(param.NewTitle, call), null, param.Pinned, ct);
            }
            else
            {
                note = await store.UpdateAsync(context.UserId, note.Id,
                    param.NewTitle is null ? null : NoteBlocks.Text(param.NewTitle, call),
                    param.Body is null ? null : NoteBlocks.Text(param.Body, call),
                    param.Pinned, ct);
            }

            return BlockResult.Main(note.ToOutput());
        }
    }

    [BlockType(BlockTypes.DeleteNote,
        Description = "Deletes one of the owner's notes; output: whether there was one.")]
    public class DeleteNoteHandler : BlockHandlerBase<DeleteNoteParams>
    {
        public override async Task<BlockResult> ExecuteAsync(BlockCall call, DeleteNoteParams param)
        {
            var note = await NoteBlocks.FindAsync(param, call);
            if (note is null)
                return BlockResult.Main(false);
            var context = call.Context;
            return BlockResult.Main(await NoteBlocks.Store(context).DeleteAsync(context.UserId, note.Id, context.CancellationToken));
        }
    }

    [BlockType(BlockTypes.Notify,
        Description = "Sends the workflow's owner a notification (live, and kept in their inbox); its input passes through.")]
    public class NotifyHandler : BlockHandlerBase<NotifyParams>
    {
        public override async Task<BlockResult> ExecuteAsync(BlockCall call, NotifyParams param)
        {
            var context = call.Context;
            var notifier = context.Services.GetService<IBlockNotifier>()
                ?? throw new InvalidOperationException("Notifications are not available here.");
            await notifier.NotifyAsync(context.UserId, NoteBlocks.Text(param.Title, call),
                NoteBlocks.Text(param.Body, call), NoteBlocks.Text(param.Link, call), context.CancellationToken);
            return BlockResult.Main(call.Input);
        }
    }
}
