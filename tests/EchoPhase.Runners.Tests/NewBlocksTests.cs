// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Text;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Http;
using EchoPhase.Runners.Blocks.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Tests
{
    /// <summary>Switch, ExecuteWorkflow, HttpRequest and RespondToWebhook.</summary>
    public class NewBlocksTests : IAsyncDisposable
    {
        private readonly RunnerHarness _harness = new();

        public ValueTask DisposeAsync() => _harness.DisposeAsync();

        private const string Switch = """
            {
              "blocks": [
                { "id": 1, "type": "Switch", "params": { "rules": [
                    { "condition": "input.kind == 'a'", "output": "isA" },
                    { "condition": "input.n > 10" } ] } },
                { "id": 2, "type": "Print", "params": { "text": "a" } },
                { "id": 3, "type": "Print", "params": { "text": "big @input.n" } },
                { "id": 4, "type": "Print", "params": { "text": "other" } }
              ],
              "connections": [
                { "from": 1, "output": "isA", "to": 2 },
                { "from": 1, "output": "1", "to": 3 },
                { "from": 1, "output": "fallback", "to": 4 }
              ]
            }
            """;

        [Theory]
        [InlineData("a", 50, "a")]
        [InlineData("b", 50, "big 50")]
        [InlineData("b", 5, "other")]
        public async Task Switch_TakesTheFirstMatchingRule(string kind, long n, string printed)
        {
            var context = await _harness.RunAsync(Switch, input: new Dictionary<string, object?> { ["kind"] = kind, ["n"] = n });

            Assert.Empty(context.Errors);
            Assert.Equal([printed], context.Output);
        }

        [Fact]
        public void Switch_RejectsUnknownOutputsAndBadRules()
        {
            var unknown = RunnerHarness.Parse("""
                { "blocks": [ { "id": 1, "type": "Switch", "params": { "rules": [ { "condition": "true" } ] } },
                              { "id": 2, "type": "Print", "params": { "text": "x" } } ],
                  "connections": [ { "from": 1, "output": "7", "to": 2 } ] }
                """);
            Assert.Contains(_harness.Validator.Validate(unknown, 200), i => i.Message.Contains("no output \"7\""));

            var broken = RunnerHarness.Parse("""
                { "blocks": [ { "id": 1, "type": "Switch", "params": { "rules": [ { "condition": "a ==" } ] } } ] }
                """);
            Assert.Contains(_harness.Validator.Validate(broken, 200), i => i.Message.StartsWith("rules[0].condition"));
        }

        // The owner is set once the harness (and so the run's user) exists.
        private sealed class Workflows(Dictionary<Guid, string> programs) : IWorkflowSource
        {
            public Guid Owner { get; set; }

            public Task<BlockProgram?> FindAsync(Guid user, Guid workflowId, CancellationToken ct)
                => Task.FromResult(user == Owner && programs.TryGetValue(workflowId, out var json) ? RunnerHarness.Parse(json) : null);
        }

        private static readonly Guid Child = Guid.NewGuid();

        // The child belongs to the harness's user.
        private static async Task<IBlockExecutionContext> RunWithChildAsync(string parent, string child)
        {
            var workflows = new Workflows(new() { [Child] = child });
            await using var harness = new RunnerHarness(s => s.AddSingleton<IWorkflowSource>(workflows));
            workflows.Owner = harness.UserId;
            return await harness.RunAsync(parent.Replace("CHILD", Child.ToString()));
        }

        [Fact]
        public async Task ExecuteWorkflow_ReturnsTheLastOutput_AndKeepsVariablesApart()
        {
            var context = await RunWithChildAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "Set", "params": { "name": "x", "value": 1 } },
                    { "id": 2, "type": "ExecuteWorkflow", "params": { "workflowId": "CHILD", "input": "@({ n: x + 1 })" } },
                    { "id": 3, "type": "Print", "params": { "text": "got @input, x=@x" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 }, { "from": 2, "to": 3 } ]
                }
                """, """
                {
                  "blocks": [
                    { "id": 1, "type": "ManualTrigger", "params": {} },
                    { "id": 2, "type": "Print", "params": { "text": "child sees @input.n" } },
                    { "id": 3, "type": "Set", "params": { "name": "x", "value": "@(input.n * 10)" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 }, { "from": 1, "to": 3 } ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(["child sees 2", "got 20, x=1"], context.Output);
        }

        [Fact]
        public async Task ExecuteWorkflow_FailingChild_FailsTheBlock()
        {
            var context = await RunWithChildAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "ExecuteWorkflow", "onError": "Continue", "params": { "workflowId": "CHILD" } },
                    { "id": 2, "type": "Print", "params": { "text": "@input.error.type" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 } ]
                }
                """, """
                { "blocks": [ { "id": 1, "type": "StopAndError", "params": { "message": "nope" } } ] }
                """);

            Assert.Equal(["SubWorkflowFailedException"], context.Output);
        }

        [Fact]
        public async Task ExecuteWorkflow_SelfCall_StopsAtTheDepthLimit()
        {
            var self = """
                { "blocks": [ { "id": 1, "type": "ExecuteWorkflow", "params": { "workflowId": "CHILD" } } ] }
                """;
            var context = await RunWithChildAsync(self, self.Replace("CHILD", Child.ToString()));

            var error = Assert.Single(context.Errors);
            Assert.Contains("SubWorkflowFailed", error.Type);
        }

        private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<(HttpRequestMessage Request, string? Body)> Seen { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Seen.Add((request, request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
                return respond(request);
            }
        }

        private static RunnerHarness WithHttp(Stub stub)
            => new(s => s.AddHttpClient(HttpRequestHandler.ClientName).ConfigurePrimaryHttpMessageHandler(() => stub));

        [Fact]
        public async Task HttpRequest_SendsTemplates_AndParsesJson()
        {
            var stub = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "id": 7, "tags": ["x"] }""", Encoding.UTF8, "application/json")
            });
            await using var harness = WithHttp(stub);

            var context = await harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "HttpRequest", "params": {
                        "method": "post", "url": "https://api.example.com/users/@input.id",
                        "query": { "q": "a b" }, "headers": { "Authorization": "Bearer @input.token" },
                        "body": "@({ name: input.name })" } },
                    { "id": 2, "type": "Print", "params": { "text": "@input.id @(input.tags[0])" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 } ]
                }
                """, input: new Dictionary<string, object?> { ["id"] = 5L, ["token"] = "t0k", ["name"] = "Ann" });

            Assert.Empty(context.Errors);
            Assert.Equal(["7 x"], context.Output);
            var (request, body) = Assert.Single(stub.Seen);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.NotNull(request.RequestUri);
            Assert.Equal("https://api.example.com/users/5?q=a%20b", request.RequestUri.AbsoluteUri);
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Bearer t0k", request.Headers.Authorization.ToString());
            Assert.Equal("""{"name":"Ann"}""", body);
            Assert.NotNull(request.Content);
            Assert.NotNull(request.Content.Headers.ContentType);
            Assert.Equal("application/json", request.Content.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task HttpRequest_ErrorStatus_FailsUnlessAllowed()
        {
            var stub = new Stub(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("missing") });
            await using var harness = WithHttp(stub);

            var failed = await harness.RunAsync("""
                { "blocks": [ { "id": 1, "type": "HttpRequest", "params": { "url": "https://x.test/a" } } ] }
                """);
            var error = Assert.Single(failed.Errors);
            Assert.Equal("HttpStatusException", error.Type);
            Assert.Contains("404", error.Message);

            var allowed = await harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "HttpRequest", "params": { "url": "https://x.test/a", "failOnError": false, "fullResponse": true } },
                    { "id": 2, "type": "Print", "params": { "text": "@input.status @input.body" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 } ]
                }
                """);
            Assert.Equal(["404 missing"], allowed.Output);
        }

        [Fact]
        public async Task HttpRequest_RejectsNonHttpUrls()
        {
            var context = await _harness.RunAsync("""
                { "blocks": [ { "id": 1, "type": "HttpRequest", "params": { "url": "file:///etc/passwd" } } ] }
                """);

            Assert.Contains("not an http or https URL", Assert.Single(context.Errors).Message);
        }

        [Fact]
        public async Task HttpRequest_CannotReachPrivateAddresses()
        {
            // The real handler, as configured for the app.
            var context = await _harness.RunAsync("""
                { "blocks": [ { "id": 1, "type": "HttpRequest", "params": { "url": "http://127.0.0.1:9/" } } ] }
                """);

            Assert.Contains("not a public address", Assert.Single(context.Errors).Message);
        }

        [Theory]
        [InlineData("127.0.0.1", false)]
        [InlineData("10.1.2.3", false)]
        [InlineData("172.20.0.1", false)]
        [InlineData("192.168.1.1", false)]
        [InlineData("169.254.169.254", false)]
        [InlineData("100.64.0.1", false)]
        [InlineData("::1", false)]
        [InlineData("fd00::1", false)]
        [InlineData("fe80::1", false)]
        [InlineData("::ffff:10.0.0.1", false)]
        [InlineData("8.8.8.8", true)]
        [InlineData("2606:4700:4700::1111", true)]
        public void PublicNetwork_ClassifiesAddresses(string address, bool isPublic)
            => Assert.Equal(isPublic, PublicNetworkConnector.IsPublic(IPAddress.Parse(address)));

        [Fact]
        public async Task RespondToWebhook_AnswersOnce_AndPassesItsInputOn()
        {
            var reply = new WebhookReply(WebhookRespondMode.ByBlock);
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "RespondToWebhook", "params": {
                        "status": 201, "body": "@({ ok: true, n: input.n })", "headers": { "X-Run": "yes" } } },
                    { "id": 2, "type": "RespondToWebhook", "params": { "status": 500 } },
                    { "id": 3, "type": "Print", "params": { "text": "after @input.n" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 }, { "from": 2, "to": 3 } ]
                }
                """, input: new Dictionary<string, object?> { ["n"] = 3L }, reply: reply);

            Assert.Equal(["after 3"], context.Output);
            var response = await reply.Response;
            Assert.Equal(201, response.Status);
            Assert.Equal("yes", response.Headers["X-Run"]);
            Assert.Equal("""{"ok":true,"n":3}""", Scripting.Eval.ToJson(response.Body));
        }

        [Fact]
        public void RespondToWebhook_RefusesDangerousHeaders()
        {
            var program = RunnerHarness.Parse("""
                { "blocks": [ { "id": 1, "type": "RespondToWebhook", "params": { "headers": { "Set-Cookie": "a=b" } } } ] }
                """);

            Assert.Contains(_harness.Validator.Validate(program, 200), i => i.Message.Contains("Set-Cookie"));
        }

        [Fact]
        public async Task Dates_UseTheProgramsTimeZone()
        {
            var context = await _harness.RunAsync("""
                { "blocks": [ { "id": 1, "type": "Print", "params": { "text": "@(date('2026-01-01T00:00:00Z'))" } } ] }
                """, timeZone: "Asia/Tokyo");

            Assert.Equal(["2026-01-01T09:00:00+09:00"], context.Output);
        }
    }
}
