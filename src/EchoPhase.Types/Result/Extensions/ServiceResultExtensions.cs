using System.Diagnostics.CodeAnalysis;

namespace EchoPhase.Types.Result.Extensions
{
    public static class ServiceResultExtensions
    {
        public static bool TryGetValue<T>(this IServiceResult<T> result, [NotNullWhen(true)] out T? value)
        {
            if (result.Successful && result.Value != null)
            {
                value = result.Value;
                return true;
            }
            value = default;
            return false;
        }

        public static bool TryGetError<T>(this IServiceResult<T> result, [NotNullWhen(true)] out IServiceError? value)
        {
            if (!result.Successful && result.Error != null)
            {
                value = result.Error;
                return true;
            }
            value = default;
            return false;
        }

        [return: NotNullIfNotNull(nameof(result.Value))]
        public static U? OnSuccess<T, U>(
            this IServiceResult<T> result,
            Func<T, U> func
        ) => (result.TryGetValue(out var value))
                ? func(value)
                : default(U);

        public static void OnSuccess<T>(
            this IServiceResult<T> result,
            Action<T> action)
        {
            if (result.TryGetValue(out var value))
                action(value);
        }

        public static Task<U?> OnSuccessAsync<T, U>(
            this IServiceResult<T> result,
            Func<T, Task<U>> func
        ) => (result.TryGetValue(out var value))
                ? func(value).ContinueWith(t => (U?)t.Result)
                : Task.FromResult(default(U));

        public static Task OnSuccessAsync<T>(
            this IServiceResult<T> result,
            Func<T, Task> action)
        {
            if (result.TryGetValue(out var value))
                return action(value);
            return Task.CompletedTask;
        }

        [return: NotNullIfNotNull(nameof(result.Error))]
        public static U? OnFailure<T, U>(
            this IServiceResult<T> result,
            Func<IServiceError, U> func
        ) => (result.TryGetError(out var err))
                ? func(err)
                : default(U);

        public static void OnFailure<T>(
            this IServiceResult<T> result,
            Action<IServiceError> action)
        {
            if (result.TryGetError(out var err))
                action(err);
        }

        public static Task<U?> OnFailureAsync<T, U>(
            this IServiceResult<T> result,
            Func<IServiceError, Task<U>> func
        ) => (result.TryGetError(out var err))
                ? func(err).ContinueWith(t => (U?)t.Result)
                : Task.FromResult(default(U));

        public static Task OnFailureAsync<T>(
            this IServiceResult<T> result,
            Func<IServiceError, Task> action)
        {
            if (result.TryGetError(out var err))
                return action(err);
            return Task.CompletedTask;
        }

        public static T GetValueOrThrow<T>(this IServiceResult<T> result)
        {
            if (result.TryGetValue(out var value))
                return value;
            throw new InvalidOperationException(
                result.TryGetError(out var err) ? $"{err.Code}: {err.Message}" : "Operation failed.");
        }
    }
}
