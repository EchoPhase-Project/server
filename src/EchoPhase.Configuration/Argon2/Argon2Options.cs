// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Argon2
{
    /// <summary>
    /// Argon2id cost for new password hashes. Defaults are RFC 9106's second
    /// recommended option (64 MiB, 3 passes, 4 lanes): about 0.6 s per hash on
    /// one core, two hashes at once on two. Memory is held for the whole hash (and the managed
    /// implementation's buffers outlive it until the next GC), so
    /// MaxConcurrentHashes bounds it; size the pod's memory limit for it.
    /// Old hashes keep verifying with the parameters stored in them and are
    /// re-hashed on the next sign-in.
    /// </summary>
    public class Argon2Options : IValidatable
    {
        public const string SectionName = "Argon2";

        /// <summary>Key-vault name of the pepper mixed into every hash.</summary>
        public string Key { get; set; } = "argon2";

        /// <summary>Memory per hash in KiB.</summary>
        public int MemoryKiB { get; set; } = 64 * 1024;

        /// <summary>Passes over the memory.</summary>
        public int Iterations { get; set; } = 3;

        /// <summary>
        /// Lanes: part of the hash's parameters (and cost), computed on one thread;
        /// concurrent sign-ins use the cores instead.
        /// </summary>
        public int Parallelism { get; set; } = 4;

        /// <summary>
        /// Hashes computed at once; more wait. Hashing is CPU-bound, so going past
        /// the pod's cores adds no throughput, only memory (MemoryKiB each).
        /// Keep it at the CPU limit: a burst of sign-ins then queues instead of
        /// exhausting the pod's memory.
        /// </summary>
        public int MaxConcurrentHashes { get; set; } = 2;

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Key), "Key cannot be empty."));

            // Below these the hash stops protecting weak passwords (OWASP floor: 7 MiB x 5 passes).
            if (MemoryKiB < 7 * 1024 || MemoryKiB > 1024 * 1024)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(MemoryKiB), "MemoryKiB must be between 7168 (7 MiB) and 1048576 (1 GiB)."));

            if (Iterations < 1 || Iterations > 10)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Iterations), "Iterations must be between 1 and 10."));

            if (Parallelism < 1 || Parallelism > 16)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Parallelism), "Parallelism must be between 1 and 16."));

            if (MaxConcurrentHashes < 1 || MaxConcurrentHashes > 64)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(MaxConcurrentHashes), "MaxConcurrentHashes must be between 1 and 64."));

            return ValidationResult.Success();
        }
    }
}
