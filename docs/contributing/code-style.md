# Code style

This file tells the rules for the C# code.

`.editorconfig` contains the rules. `mise run lint` examines them in CI. These
rules are frequently unexpected:

- Each file starts with the licence header. The IDE adds it from
  `.editorconfig`:

  ```csharp
  // Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
  // See the LICENCE file in the repository root for full licence text.
  ```

- Use block-scoped namespaces. The namespace must agree with the folder. Put
  each brace on a separate line.
- Use explicit constructors that set `_camelCase` fields. Do not use primary
  constructors. Do not use `this.`.
- Use `var` only where the type is clear. Do not use `var` for built-in
  types.
- Lines have a maximum of 120 characters, with LF line endings.
- Write a `<summary>` for each public member. On API request and response
  records, the `/// <param>` text becomes the field description in the
  OpenAPI contract.

## Null

Nullable reference types are on in all projects, and their warnings cause the
build to fail. Do not ignore null values. Do not use the null-forgiving
operator (`x!`, `null!`, `default!`). `NullForgivingTests` examines `src` and
`tests` for it. Use one of these methods instead:

- Do a check, with an exception that tells what is missing:
  `entity ?? throw new InvalidOperationException("…")`. You can also use a
  pattern: `if (result is not { } value) return …;`.
- Give the information to the compiler. `IServiceResult<T>.Successful` has
  `[MemberNotNullWhen(true, nameof(Value))]`. `Try…` methods have
  `[NotNullWhen]` or `[MaybeNullWhen]`. Then a result after a check does not
  need `!`.
- Make the type correct. A value that can be missing is `T?`. A member that
  must have a value is `required`. A `DbSet` is `=> Set<T>()`.
- In tests, use `Assert.NotNull(x)` (or `Must.Present(x)` of the project) and
  `result.Ok()`. They fail with the reason.

## Package versions

The project sets package versions centrally, in `src/Directory.Packages.props`
and `tests/Directory.Packages.props`. Do not set versions in a project file.

## Time

Get the time from an injected `TimeProvider`. Do not use `DateTime.UtcNow`.
Then the tests can control the time.
