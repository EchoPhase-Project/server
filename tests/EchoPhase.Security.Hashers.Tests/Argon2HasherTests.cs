// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Argon2;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Cryptography.Vaults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Hashers.Tests
{
    public class Argon2HasherTests
    {
        private const string Password = "Passw0rdA";

        // Made by Isopoh.Cryptography.Argon2 from NuGet (the 1.x API published as
        // 2.0.0) with the pepper as Argon2's secret: hashes already in the database
        // look like these, so any library change must keep verifying them.
        private const string CurrentFormat =
            "$argon2id$v=19$m=65536,t=3,p=4$ZGVmZ2hpamtsbW5vcHFycw$i/MB1DilT4PgWnfOLij4LZTFNzwyGBlteqtNPnXrPQw";
        private const string OldestFormat =
            "$argon2i$v=19$m=65536,t=6,p=4$ZGVmZ2hpamtsbW5vcHFycw$tw2cgjE5gQ2sQh1+mWHkvptlAqgfw/mh7aIq+9tozpY";
        private const string WeakerFormat =
            "$argon2id$v=19$m=32768,t=3,p=1$ZGVmZ2hpamtsbW5vcHFycw$52vdTBl4YPEj+Yl7QRsB61KlLeo/4uZxRF7PSRIwbR0";

        private static Argon2Hasher Hasher(Argon2Options? options = null)
        {
            var services = new ServiceCollection().AddSingleton<IKeyVault, FixedKeyVault>().BuildServiceProvider();
            return new Argon2Hasher(Options.Create(options ?? new Argon2Options()), new KeyRing(services));
        }

        private static readonly User User = new("test");

        [Fact]
        public void HashesInTheCurrentFormat_Verify()
            => Assert.Equal(PasswordVerificationResult.Success, Hasher().VerifyHashedPassword(User, CurrentFormat, Password));

        [Theory]
        [InlineData(OldestFormat)]
        [InlineData(WeakerFormat)]
        public void OlderParameters_VerifyAndAskForARehash(string stored)
            => Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, Hasher().VerifyHashedPassword(User, stored, Password));

        [Theory]
        [InlineData(CurrentFormat)]
        [InlineData(OldestFormat)]
        public void WrongPassword_Fails(string stored)
            => Assert.Equal(PasswordVerificationResult.Failed, Hasher().VerifyHashedPassword(User, stored, "Wrong0000"));

        [Fact]
        public void NewHash_RoundTripsWithTheCurrentParameters()
        {
            var hasher = Hasher();
            var stored = hasher.HashPassword(User, Password);

            Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=4$", stored);
            Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(User, stored, Password));
            Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(User, stored, "Wrong0000"));
        }

        [Fact]
        public void SaltIsRandom()
        {
            var hasher = Hasher();
            Assert.NotEqual(hasher.HashPassword(User, Password), hasher.HashPassword(User, Password));
        }

        [Fact]
        public void Garbage_Fails()
            => Assert.Equal(PasswordVerificationResult.Failed, Hasher().VerifyHashedPassword(User, "not a hash", Password));

        [Fact]
        public async Task ConcurrentVerifications_AllSucceed()
        {
            var hasher = Hasher(new Argon2Options { MaxConcurrentHashes = 2 });

            var results = await Task.WhenAll(Enumerable.Range(0, 6)
                .Select(_ => Task.Run(() => hasher.VerifyHashedPassword(User, CurrentFormat, Password),
                    TestContext.Current.CancellationToken)));

            Assert.All(results, r => Assert.Equal(PasswordVerificationResult.Success, r));
        }
    }
}
