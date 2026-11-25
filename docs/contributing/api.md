# The API

This file tells how to write services, endpoints, and the API contract.

## Results and errors

Services return `IServiceResult` or `IServiceResult<T>`
(`EchoPhase.Types/Result`). They do not throw exceptions. Use `Success` and
`Failure`. Use `Map` and `Bind` to chain steps: after a failure, the
subsequent steps do not run. Exceptions are for bugs only. Do not use them
for results that the caller must handle.

An error has a stable code and a message. Codes are `const string` values in
the `*Errors` class of the area (`AuthErrors`, `ApiClientErrors`,
`BlockRunErrors`). If a code in `ErrorCodes` (`NotFound`, `Invalid`,
`Conflict`, `Forbidden`…) has the same meaning, use it again. Clients use the
codes in their logic. Thus, do not change the name of a released code.

## An API endpoint

Use an existing controller as the reference
(`Controllers/Api/v1/Storage/StorageController.cs`):

- The controller derives from `ApiControllerBase`. It is in
  `Controllers/Api/v1/<Area>/`.
- Request and response types are `sealed record` types with `/// <param>` docs
  and validation attributes.
- Each action has `[RequirePermission(Resources.X, Permissions.Y)]`. Each
  action that changes data also has `[ValidateAntiForgery]`.
- Each status code has `[ProducesResponseType]`. Errors use `ErrorBody`.
- Each action gives its `CancellationToken` to the calls that it makes.

`Failure(error)` changes a service failure into a response.
`ApiControllerBase.StatusFor` sets the status code. **If a new error code must
not return 400, add its case to `StatusFor`.** If you do not add it, the code
returns 400.

## Responses

Return each response body as a `sealed record` named `<Name>Response`. Put
the record in the controller file, in `<Area>/<Name>Response.cs`, or with the
other records of the area in `<Area>/<Area>Responses.cs`. The record is the list of the fields that go out of the
server. Thus, a new property of an entity stays inside until you add it.

Do not return a type of a library project, such as a service record. Only
the XML docs of `EchoPhase.Server` go into the contract. Thus, a library
type has no descriptions there, and a change in the library changes the
contract without notice. Two exceptions are allowed:

- A DTO of an external API that the server passes through (Discord,
  Twitch). It keeps the names of that API.
- A list or a map of such typed values, for example
  `Dictionary<string, Guid[]>`.

Map to the record with a static `From` method on the record. For a list,
use `.Select(XResponse.From)`. If a field depends on the user, write that
check in `From` (refer to `NoteResponse.From`). Then the access rule is in
one place.

Declare the record with `[ProducesResponseType<XResponse>(...)]`. Then the
contract contains the type, the `/// <param>` docs, and the nullability, and
the web client gets its types from the contract.

Do not return an entity or an anonymous object. The contract does not
contain such a body, so `openapi:check` cannot see its changes.
`ResponseTypeTests` fails if an action does not declare a typed success
response. A response without a body is 204, or 202 for work that continues
after the response. A redirect is 302.

If a list loads large rows, you can also give the record a static
`Expression<Func<TEntity, XResponse>>` and use it in the query (`Select`).
Then the database sends only the columns of the response.

Permissions are `resource:action` constants (`Security.BitMasks/Constants`).
`RolePermissions.cs` gives them to roles. A new permission changes the
permission encoding in tokens. Tell this in the pull request.

## The API contract

The web client and other clients use the contract (`artifacts/openapi.json`,
`mise run openapi`). Nullability is important: a reference type that is not
nullable is a required field.

The contract must stay compatible with clients in a release series. Before
1.0, a series is a minor version (`0.2.x`). `mise run openapi:check` compares
the contract with the latest release of its series. It fails if it finds an
incompatible change.

If you must make an incompatible change, the commit must start a new series.
Before 1.0, this is a `feat!:` commit. For an intentional exception, add an
entry with its reason to `openapi.err-ignore`. The README tells about the two
methods in "Breaking API changes".

If more values can be added to an enum in the future, give it
`[ExtensibleEnum]`. Then a new value is a compatible change, and clients must
accept values that they do not know.
