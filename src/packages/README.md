# Local NuGet packages

Packages built from source that are not on nuget.org (yet). `NuGet.config` in
`src/` and in the repository root add this folder as a package source; the
backend Dockerfile copies it before `dotnet restore`.

| Package | Version | Built from |
|---|---|---|
| Isopoh.Cryptography.Argon2, .Blake2b, .SecureArray | see the `.nupkg` files | [EchoPhase-Project/Isopoh.Cryptography.Argon2](https://github.com/EchoPhase-Project/Isopoh.Cryptography.Argon2), branch `echophase` (upstream v2 + patches) |

To rebuild: `mise run isopoh:pack` (see mise-tasks/dotnet.toml).
