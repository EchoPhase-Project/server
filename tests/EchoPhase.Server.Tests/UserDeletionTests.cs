// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Cli.Commands.User.Delete;

namespace EchoPhase.Server.Tests
{
    public class UserDeletionTests
    {
        private static readonly Guid Alice = Guid.NewGuid();
        private static readonly Guid Bob = Guid.NewGuid();

        [Fact]
        public void TheOnlyAdminIsTheLast()
            => Assert.True(UserDeletion.IsLastAdmin(Alice, [Alice]));

        [Fact]
        public void AnAdminWithAnotherIsNotTheLast()
            => Assert.False(UserDeletion.IsLastAdmin(Alice, [Alice, Bob]));

        [Fact]
        public void SomeoneWhoIsNoAdminIsNeverTheLast()
        {
            Assert.False(UserDeletion.IsLastAdmin(Bob, [Alice]));
            Assert.False(UserDeletion.IsLastAdmin(Bob, []));
        }

        [Theory]
        [InlineData("probe1", true)]
        [InlineData("  probe1 ", true)]
        [InlineData("Probe1", false)]
        [InlineData("probe", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void ConfirmationIsTheExactName(string? typed, bool confirms)
            => Assert.Equal(confirms, UserDeletion.Confirms(typed, "probe1"));
    }
}
