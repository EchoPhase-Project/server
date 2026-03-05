namespace EchoPhase.Security.BitMasks.Tests
{
    public class RolesBitMaskTests
    {
        private readonly RolesBitMask _mask = new();

        private static readonly string[] AllRoles =
            Roles.AsEnumerable().Order().ToArray();

        private static Dictionary<string, object?> AsDictionary(object? result) =>
            Assert.IsType<Dictionary<string, object?>>(result);

        // ── Encode ────────────────────────────────────────────────────────────

        [Fact]
        public void Encode_SingleRole_ReturnsSuccessfulResult()
        {
            var result = _mask.Encode(new[] { Roles.Admin });

            Assert.True(result.Successful);
        }

        [Fact]
        public void Encode_AllRoles_ReturnsSuccessfulResult()
        {
            var result = _mask.Encode(AllRoles);

            Assert.True(result.Successful);
        }

        [Fact]
        public void Encode_EmptyArray_ReturnsFailure()
        {
            var result = _mask.Encode(Array.Empty<string>());

            Assert.False(result.Successful);
        }

        [Fact]
        public void Encode_UnknownRole_ReturnsFailure()
        {
            var result = _mask.Encode(new[] { "unknown_role" });

            Assert.False(result.Successful);
        }

        [Fact]
        public void Encode_MixedKnownAndUnknown_ReturnsFailure()
        {
            var result = _mask.Encode(new[] { Roles.Admin, "unknown_role" });

            Assert.False(result.Successful);
        }

        // ── Decode ────────────────────────────────────────────────────────────

        [Fact]
        public void Decode_EncodedSingleRole_ReturnsSameRole()
        {
            var encoded = _mask.Encode(new[] { Roles.Admin });
            Assert.True(encoded.TryGetValue(out var bitmask));

            var decoded = _mask.Decode(bitmask!);
            Assert.True(decoded.Successful);
            Assert.True(decoded.TryGetValue(out var roles));
            Assert.Contains(Roles.Admin, roles!);
            Assert.Single(roles!);
        }

        [Fact]
        public void Decode_EncodedMultipleRoles_ReturnsSameRoles()
        {
            var input = new[] { Roles.Admin, Roles.Staff };
            var encoded = _mask.Encode(input);
            Assert.True(encoded.TryGetValue(out var bitmask));

            var decoded = _mask.Decode(bitmask!);
            Assert.True(decoded.Successful);
            Assert.True(decoded.TryGetValue(out var roles));
            Assert.Equal(input.OrderBy(x => x), roles!.OrderBy(x => x));
        }

        [Fact]
        public void Decode_AllRoles_ReturnsAllRoles()
        {
            var encoded = _mask.Encode(AllRoles);
            Assert.True(encoded.TryGetValue(out var bitmask));

            var decoded = _mask.Decode(bitmask!);
            Assert.True(decoded.Successful);
            Assert.True(decoded.TryGetValue(out var roles));
            Assert.Equal(AllRoles.OrderBy(x => x), roles!.OrderBy(x => x));
        }

        [Fact]
        public void Decode_EmptyBitmask_ReturnsFailure()
        {
            var result = _mask.Decode(new BitArray(0));

            Assert.False(result.Successful);
        }

        // ── Serialize ─────────────────────────────────────────────────────────

        [Fact]
        public void Serialize_ValidBitmask_ReturnsSuccessfulResult()
        {
            var encoded = _mask.Encode(new[] { Roles.Admin });
            Assert.True(encoded.TryGetValue(out var bitmask));

            var serialized = RolesBitMask.Serialize(bitmask!);

            Assert.True(serialized.Successful);
        }

        [Fact]
        public void Serialize_ContainsVersionAndData()
        {
            var encoded = _mask.Encode(new[] { Roles.Admin });
            Assert.True(encoded.TryGetValue(out var bitmask));

            var serialized = RolesBitMask.Serialize(bitmask!);
            Assert.True(serialized.TryGetValue(out var value));

            // Формат: {base64version}${base64data}
            var parts = value!.Split('$', 2);
            Assert.Equal(2, parts.Length);
            Assert.NotEmpty(parts[0]); // version
            Assert.NotEmpty(parts[1]); // data
        }

        [Fact]
        public void Serialize_EmptyBitmask_ReturnsFailure()
        {
            var result = RolesBitMask.Serialize(new BitArray(0));

            Assert.False(result.Successful);
        }

        // ── Deserialize ───────────────────────────────────────────────────────

        [Fact]
        public void Deserialize_ValidSerialized_ReturnsSuccessfulResult()
        {
            var encoded = _mask.Encode(new[] { Roles.Admin });
            Assert.True(encoded.TryGetValue(out var bitmask));

            var serialized = RolesBitMask.Serialize(bitmask!);
            Assert.True(serialized.TryGetValue(out var value));

            var deserialized = RolesBitMask.Deserialize(value!);

            Assert.True(deserialized.Successful);
        }

        [Fact]
        public void Deserialize_EmptyString_ReturnsFailure()
        {
            var result = RolesBitMask.Deserialize(string.Empty);

            Assert.False(result.Successful);
        }

        [Fact]
        public void Deserialize_WhitespaceString_ReturnsFailure()
        {
            var result = RolesBitMask.Deserialize("   ");

            Assert.False(result.Successful);
        }

        [Fact]
        public void Deserialize_InvalidFormat_ReturnsFailure()
        {
            var result = RolesBitMask.Deserialize("notvalidformat");

            Assert.False(result.Successful);
        }

        [Fact]
        public void Deserialize_WrongVersion_ReturnsFailure()
        {
            // Подделываем версию
            var fakeVersion = Convert.ToBase64String(new byte[] { 0x00, 0x01, 0x02 });
            var result = RolesBitMask.Deserialize($"{fakeVersion}$AAAA");

            Assert.False(result.Successful);
        }

        // ── Roundtrip ─────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_SingleRole_PreservesRole()
        {
            var input = new[] { Roles.Admin };

            var encoded    = _mask.Encode(input);
            Assert.True(encoded.TryGetValue(out var bitmask));

            var serialized = RolesBitMask.Serialize(bitmask!);
            Assert.True(serialized.TryGetValue(out var value));

            var deserialized = RolesBitMask.Deserialize(value!);
            Assert.True(deserialized.TryGetValue(out var restoredBitmask));

            var decoded = _mask.Decode(restoredBitmask!);
            Assert.True(decoded.TryGetValue(out var roles));

            Assert.Equal(input, roles!.OrderBy(x => x).ToArray());
        }

        [Fact]
        public void Roundtrip_AllRoles_PreservesAllRoles()
        {
            var encoded = _mask.Encode(AllRoles);
            Assert.True(encoded.TryGetValue(out var bitmask));

            var serialized = RolesBitMask.Serialize(bitmask!);
            Assert.True(serialized.TryGetValue(out var value));

            var deserialized = RolesBitMask.Deserialize(value!);
            Assert.True(deserialized.TryGetValue(out var restoredBitmask));

            var decoded = _mask.Decode(restoredBitmask!);
            Assert.True(decoded.TryGetValue(out var roles));

            Assert.Equal(AllRoles.OrderBy(x => x), roles!.OrderBy(x => x));
        }

        [Fact]
        public void Roundtrip_MultipleRoles_PreservesRoles()
        {
            var input = new[] { Roles.Staff, Roles.Dev };

            var encoded = _mask.Encode(input);
            Assert.True(encoded.TryGetValue(out var bitmask));

            var serialized = RolesBitMask.Serialize(bitmask!);
            Assert.True(serialized.TryGetValue(out var value));

            var deserialized = RolesBitMask.Deserialize(value!);
            Assert.True(deserialized.TryGetValue(out var restoredBitmask));

            var decoded = _mask.Decode(restoredBitmask!);
            Assert.True(decoded.TryGetValue(out var roles));

            Assert.Equal(
                input.OrderBy(x => x),
                roles!.OrderBy(x => x));
        }

        // ── Version ───────────────────────────────────────────────────────────

        [Fact]
        public void Version_IsConsistentAcrossInstances()
        {
            var v1 = RolesBitMask.Version.ToArray();
            var v2 = RolesBitMask.Version.ToArray();

            Assert.Equal(v1, v2);
        }

        [Fact]
        public void Version_IsNotEmpty()
        {
            Assert.NotEmpty(RolesBitMask.Version.ToArray());
        }

        [Fact]
        public void Serialize_ContainsCorrectVersion()
        {
            var encoded = _mask.Encode(new[] { Roles.Admin });
            Assert.True(encoded.TryGetValue(out var bitmask));

            var serialized = RolesBitMask.Serialize(bitmask!);
            Assert.True(serialized.TryGetValue(out var value));

            var parts = value!.Split('$', 2);
            var versionInToken = Convert.FromBase64String(parts[0]);

            Assert.True(versionInToken.AsSpan().SequenceEqual(RolesBitMask.Version));
        }
    }
}
