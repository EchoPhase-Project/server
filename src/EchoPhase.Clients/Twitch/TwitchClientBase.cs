using EchoPhase.Clients.Twitch.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using EchoPhase.Clients.Abstractions;

namespace EchoPhase.Clients.Twitch
{
    public abstract class TwitchClientBase
    {
        private readonly HttpSender _sender;

        private static readonly JsonSerializerOptions _jsonOptions =
            new(JsonSerializerDefaults.Web);

        protected TwitchClientBase(HttpClient client)
        {
            _sender = new HttpSender(client);
        }

        protected async Task<PagedResult<IEnumerable<TR>>> SendAsync<TQ, TB, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            string bearerToken,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
        {
            var (statusCode, stream) = await _sender.SendAsync(
                uri, method, query, body,
                new AuthenticationHeaderValue("Bearer", bearerToken),
                ct);

            await using (stream)
            {
                if (statusCode >= HttpStatusCode.OK && statusCode < HttpStatusCode.MultipleChoices)
                {
                    var wrapper = await JsonSerializer.DeserializeAsync<TwitchApiResponseDto<TR>>(
                        stream, _jsonOptions, ct);

                    if (wrapper is null)
                        return PagedResult<IEnumerable<TR>>.Fail(
                            new ApiError((int)statusCode, "Response deserialization returned null."),
                            statusCode);

                    return PagedResult<IEnumerable<TR>>.Ok(
                        wrapper.Data,
                        new PageInfo(wrapper.Pagination?.Cursor),
                        statusCode);
                }

                var error = await JsonSerializer.DeserializeAsync<TwitchApiError>(stream, _jsonOptions, ct);
                return PagedResult<IEnumerable<TR>>.Fail(
                    new ApiError((int)statusCode, error?.Message),
                    statusCode);
            }
        }

        protected async Task<Result<TR>> SendRawAsync<TQ, TB, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            string bearerToken,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
        {
            var (statusCode, stream) = await _sender.SendAsync(
                uri, method, query, body,
                new AuthenticationHeaderValue("Bearer", bearerToken),
                ct);

            await using (stream)
            {
                if (statusCode >= HttpStatusCode.OK && statusCode < HttpStatusCode.MultipleChoices)
                {
                    var data = await JsonSerializer.DeserializeAsync<TR>(stream, _jsonOptions, ct);

                    if (data is null)
                        return Result<TR>.Fail(
                            new ApiError((int)statusCode, "Response deserialization returned null."),
                            statusCode);

                    return Result<TR>.Ok(data, statusCode);
                }

                var error = await JsonSerializer.DeserializeAsync<TwitchApiError>(stream, _jsonOptions, ct);
                return Result<TR>.Fail(
                    new ApiError((int)statusCode, error?.Message),
                    statusCode);
            }
        }
    }
}
