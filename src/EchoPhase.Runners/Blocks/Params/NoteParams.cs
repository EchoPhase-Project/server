// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Attributes;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    /// <summary>Which note: by id, or by title among the owner's own notes.</summary>
    public abstract class NoteRefParams : IValidatable
    {
        /// <summary>The note's id (a template: "@input.noteId").</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>Instead of the id: the title of one of the owner's notes, ignoring case (a template).</summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        public virtual IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) == string.IsNullOrWhiteSpace(Title))
                return ValidationResult.Failure(error => error.Set(nameof(Id), "Give the note's id or its title, one of the two."));
            return ValidationResult.Success();
        }
    }

    public class CreateNoteParams : IValidatable
    {
        /// <summary>A template; empty: the text's first line.</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Markdown (a template).</summary>
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("pinned")]
        public bool Pinned { get; set; }

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Body))
                return ValidationResult.Failure(error => error.Set(nameof(Body), "A note needs a title or some text."));
            return ValidationResult.Success();
        }
    }

    public class GetNoteParams : NoteRefParams
    {
    }

    public class IfNoteExistsParams : NoteRefParams
    {
    }

    public class DeleteNoteParams : NoteRefParams
    {
    }

    [ExtensibleEnum]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EditNoteMode
    {
        /// <summary>The text becomes `body`.</summary>
        Replace,

        /// <summary>`body` is added at the end, as a new paragraph (a log).</summary>
        Append
    }

    public class EditNoteParams : NoteRefParams
    {
        /// <summary>The new title (a template); absent: it stays.</summary>
        [JsonPropertyName("newTitle")]
        public string? NewTitle { get; set; }

        /// <summary>Markdown (a template); absent: the text stays.</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("mode")]
        public EditNoteMode Mode { get; set; } = EditNoteMode.Replace;

        /// <summary>Absent: it stays.</summary>
        [JsonPropertyName("pinned")]
        public bool? Pinned { get; set; }

        public override IValidationResult Validate()
        {
            var reference = base.Validate();
            if (!reference.Successful)
                return reference;
            if (NewTitle is null && Body is null && Pinned is null)
                return ValidationResult.Failure(error => error.Set(nameof(Body), "Nothing to change: give newTitle, body or pinned."));
            if (Mode == EditNoteMode.Append && string.IsNullOrEmpty(Body))
                return ValidationResult.Failure(error => error.Set(nameof(Body), "Append needs a body."));
            return ValidationResult.Success();
        }
    }

    public class NotifyParams : IValidatable
    {
        /// <summary>A template.</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>More text (a template), or nothing.</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>A path in the app to open ("/notes"), a template; anything else is dropped.</summary>
        [JsonPropertyName("link")]
        public string? Link { get; set; }

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Title))
                return ValidationResult.Failure(error => error.Set(nameof(Title), "Title cannot be empty."));
            return ValidationResult.Success();
        }
    }
}
