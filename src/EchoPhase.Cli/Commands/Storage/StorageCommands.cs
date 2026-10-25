// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.Configuration.Storage.S3;
using EchoPhase.Storage;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace EchoPhase.Cli.Commands.Storage
{
    public class StorageSettings : CommandSettings
    {
    }

    /// <summary>Whether the bucket of <c>Storage:S3</c> answers.</summary>
    public class CheckCommand : AsyncCommand<StorageSettings>
    {
        private readonly IObjectStorage _storage;
        private readonly S3Options _options;

        public CheckCommand(IObjectStorage storage, IOptions<S3Options> options)
        {
            _storage = storage;
            _options = options.Value;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, StorageSettings settings, CancellationToken cancellationToken)
        {
            if (!_storage.IsConfigured)
                return StorageCli.NotConfigured();

            await _storage.CheckAsync(cancellationToken);
            AnsiConsole.MarkupLineInterpolated($"[green]Bucket {_options.Bucket} at {_options.Endpoint} answers.[/]");
            return CliExit.Success;
        }
    }

    public class PutSettings : StorageSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("Key to store it under, e.g. tests/hello.txt")]
        public string Key { get; set; } = string.Empty;

        [CommandArgument(1, "[FILE]")]
        [Description("File to upload; stdin when left out")]
        public string? File { get; set; }

        [CommandOption("--content-type <TYPE>")]
        [Description("Content type (default application/octet-stream)")]
        public string? ContentType { get; set; }
    }

    /// <summary>Uploads a file, or stdin when no file is given, under a key.</summary>
    public class PutCommand : AsyncCommand<PutSettings>
    {
        private readonly IObjectStorage _storage;

        public PutCommand(IObjectStorage storage)
        {
            _storage = storage;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, PutSettings settings, CancellationToken cancellationToken)
        {
            if (!_storage.IsConfigured)
                return StorageCli.NotConfigured();

            await using var content = settings.File is null
                ? Console.OpenStandardInput()
                : System.IO.File.OpenRead(settings.File);
            await _storage.PutAsync(settings.Key, content, settings.ContentType, cancellationToken);

            var stored = await _storage.HeadAsync(settings.Key, cancellationToken);
            Console.Error.WriteLine($"Stored {settings.Key} ({stored?.Size ?? 0} bytes).");
            return CliExit.Success;
        }
    }

    public class GetSettings : StorageSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("Key of the object")]
        public string Key { get; set; } = string.Empty;

        [CommandArgument(1, "[FILE]")]
        [Description("File to write; stdout when left out")]
        public string? File { get; set; }
    }

    /// <summary>Downloads an object to a file or stdout.</summary>
    public class GetCommand : AsyncCommand<GetSettings>
    {
        private readonly IObjectStorage _storage;

        public GetCommand(IObjectStorage storage)
        {
            _storage = storage;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, GetSettings settings, CancellationToken cancellationToken)
        {
            if (!_storage.IsConfigured)
                return StorageCli.NotConfigured();

            await using var stored = await _storage.GetAsync(settings.Key, cancellationToken);
            if (stored is null)
            {
                Console.Error.WriteLine($"No object {settings.Key}.");
                return CliExit.Failure;
            }

            await using var target = settings.File is null
                ? Console.OpenStandardOutput()
                : System.IO.File.Create(settings.File);
            await stored.Content.CopyToAsync(target, cancellationToken);
            return CliExit.Success;
        }
    }

    public class ListSettings : StorageSettings
    {
        [CommandArgument(0, "[PREFIX]")]
        [Description("Only keys starting with it")]
        public string? Prefix { get; set; }

        [CommandOption("--json")]
        [Description("One JSON object per line")]
        public bool Json { get; set; }
    }

    /// <summary>Lists the objects, optionally under a prefix.</summary>
    public class ListCommand : AsyncCommand<ListSettings>
    {
        private readonly IObjectStorage _storage;

        public ListCommand(IObjectStorage storage)
        {
            _storage = storage;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings, CancellationToken cancellationToken)
        {
            if (!_storage.IsConfigured)
                return StorageCli.NotConfigured();

            var table = new Table().AddColumns("Key", "Size", "Modified");
            var count = 0;
            await foreach (var item in _storage.ListAsync(settings.Prefix, cancellationToken))
            {
                count++;
                if (settings.Json)
                    Console.WriteLine(JsonConvert.SerializeObject(new { key = item.Key, size = item.Size, modified = item.LastModified }));
                else
                    table.AddRow(Markup.Escape(item.Key), item.Size.ToString(), item.LastModified?.ToString("u") ?? "");
            }

            if (!settings.Json)
            {
                AnsiConsole.Write(table);
                AnsiConsole.MarkupLineInterpolated($"{count} objects.");
            }
            return CliExit.Success;
        }
    }

    public class DeleteSettings : StorageSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("Key of the object")]
        public string Key { get; set; } = string.Empty;
    }

    /// <summary>Removes an object.</summary>
    public class DeleteCommand : AsyncCommand<DeleteSettings>
    {
        private readonly IObjectStorage _storage;

        public DeleteCommand(IObjectStorage storage)
        {
            _storage = storage;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, DeleteSettings settings, CancellationToken cancellationToken)
        {
            if (!_storage.IsConfigured)
                return StorageCli.NotConfigured();

            await _storage.DeleteAsync(settings.Key, cancellationToken);
            AnsiConsole.MarkupLineInterpolated($"Removed {settings.Key}.");
            return CliExit.Success;
        }
    }

    internal static class StorageCli
    {
        public static int NotConfigured()
        {
            Console.Error.WriteLine("Object storage is not configured: set Storage:S3 or S3_ENDPOINT (and the other S3_* variables).");
            return CliExit.Failure;
        }
    }
}
