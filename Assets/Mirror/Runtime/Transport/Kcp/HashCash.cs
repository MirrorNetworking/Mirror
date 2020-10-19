
using System;
using System.Security.Cryptography;

namespace Mirror.KCP
{

    /// <summary>
    /// minimalistic hashcash-like token
    /// a simplification of the hashcash header described here:
    /// https://github.com/cliftonm/hashcash
    /// </summary>
    /// <remarks> to make it light weight in the server I adjusted the field to plain numbers</remarks>
    /// <remarks> When hashing this structure with sha1
    /// for a token to validate it has to:
    /// 1) be recent,  so dt must be in the near past in utc time
    /// 2) the resource must match the expected resource in the server
    /// 3) the token has not been seen in the server yet
    /// 4) the sha1 hash of the token must start with zeroes. The more zeroes,  the more difficulty
    /// </remarks>
    public readonly struct HashCash : IEquatable<HashCash>
    {
        /// <summary>
        /// Date and time when the token was generated
        /// </summary>
        public readonly DateTime dt;
        /// <summary>
        /// a number that represents the resource this hashcash is for.
        /// In the original they have a string,  so just use a string hash here
        /// </summary>
        public readonly int resource;

        /// <summary>
        /// the random number.  In the original they have
        /// a byte[],  but we can limit it to 64 bits
        /// </summary>
        public readonly ulong salt;
        /// <summary>
        /// counter used for making the token valid
        /// </summary>
        public readonly ulong counter;

        public HashCash(DateTime dt, int resource, ulong salt, ulong counter)
        {
            this.dt = dt;
            this.resource = resource;
            this.salt = salt;
            this.counter = counter;
        }

        public HashCash(DateTime dt, string resource, ulong salt, ulong counter)
        {
            this.dt = dt;
            this.resource = resource.GetStableHashCode();
            this.salt = salt;
            this.counter = counter;
        }

        public bool Equals(HashCash other)
        {
            return dt == other.dt &&
                resource == other.resource &&
                salt == other.salt &&
                counter == other.counter;
        }

        public override bool Equals(object obj)
        {
            return (obj is HashCash hashCash) && Equals(hashCash);
        }

        public override int GetHashCode()
        {
            int hashCode = -858276690;
            hashCode = hashCode * -1521134295 + dt.GetHashCode();
            hashCode = hashCode * -1521134295 + resource.GetHashCode();
            hashCode = hashCode * -1521134295 + salt.GetHashCode();
            hashCode = hashCode * -1521134295 + counter.GetHashCode();
            return hashCode;
        }

        #region mining

        /// <summary>
        /// Mines a hashcash token for a given resource
        /// </summary>
        /// <param name="resource">The resource for which we are mining the token
        /// the resource can be any number, but should be unique to your game
        /// for example,  use Application.productName.GetStableHashCode()</param>
        /// <returns>A valid HashCash for the resource</returns>
        public static HashCash Mine(string resource, int bits = 18)
        {

            var random = new Random();

            long newSalt = random.Next();
            newSalt = (newSalt << 32) | (long)random.Next();

            var token = new HashCash(DateTime.UtcNow, resource, (ulong)newSalt, 0);

            // note we create these here so Mine does not depend
            // on static state.  This way Mine is thread safe
            HashAlgorithm hashAlg = SHA256.Create();
            byte[] buf = new byte[HashCashEncoding.SIZE];

            // calculate hash after hash until
            // we find one that validaes
            while (true)
            {
                byte[] hash = token.Hash(hashAlg, buf);

                if (Validate(hash, bits))
                    return token;

                token = new HashCash(token.dt, token.resource, token.salt, token.counter + 1);
            }
        }


        #endregion


        #region Validation
        private static readonly HashAlgorithm hashAlgorithm = SHA256.Create();

        private static readonly byte[] buffer = new byte[HashCashEncoding.SIZE];

        // not thread safe
        internal byte[] Hash() => Hash(hashAlgorithm, buffer);

        // calculate the hash of a hashcash token
        private byte[] Hash(HashAlgorithm hashAlgorithm, byte[] buffer)
        {
            int length = HashCashEncoding.Encode(buffer, 0, this);

            return hashAlgorithm.ComputeHash(buffer, 0, length);
        }

        // validate that the first n bits in a hash are zero
        internal static bool Validate(byte[] hash, int bits = 18)
        {
            int bytesToCheck = bits >> 3 ;
            int remainderBitsToCheck = bits & 0b111;
            byte remainderMask = (byte)(0xFF << (8 - remainderBitsToCheck));

            if (bytesToCheck >= hash.Length)
                return false;

            for (int i=0; i< bytesToCheck; i++)
            {
                if (hash[i] != 0)
                    return false;
            }

            return (hash[bytesToCheck] & remainderMask) == 0;
        }


        internal bool ValidateHash(int bits = 20)
        {
            return Validate(Hash(), bits);
        }

        public bool Validate(string resource, int bits = 18)
            => Validate(resource.GetStableHashCode(), bits);

        private bool Validate(int resource, int bits = 18)
        {
            // this token is for some other resource
            if (this.resource != resource)
                return false;

            // tokens are valid for 10 minutes
            if (dt < DateTime.UtcNow.AddMinutes(-10f))
                return false;

            // tokens cannot come from the future
            if (dt > DateTime.UtcNow.AddMinutes(5f))
                return false;

            return ValidateHash(bits);
        }

        #endregion
    }

    public static class HashCashEncoding
    {
        // takes 20 bytes to serialize a hash cash token
        public const int SIZE = 28;

        /// <summary>
        /// Encode a hashcash token into a buffer
        /// </summary>
        /// <param name="buffer">the buffer where to store the hashcash</param>
        /// <param name="index">the index in the buffer where to put it</param>
        /// <param name="hashCash">the token to be encoded</param>
        /// <returns>the length of the written data</returns>
        public static int Encode(byte[] buffer, int index, HashCash hashCash)
        {
            var encoder = new Encoder(buffer, index);

            encoder.Encode64U((ulong)hashCash.dt.Ticks);
            encoder.Encode32U((uint)hashCash.resource);
            encoder.Encode64U(hashCash.salt);
            encoder.Encode64U(hashCash.counter);

            return encoder.Position - index ;
        }

        /// <summary>
        /// Encode a hashcash token into a buffer
        /// </summary>
        /// <param name="buffer">the buffer where to store the hashcash</param>
        /// <param name="index">the index in the buffer where to put it</param>
        /// <param name="hashCash">the token to be encoded</param>
        /// <returns>the length of the written data</returns>
        public static HashCash Decode(byte[] buffer, int index)
        {
            var decoder = new Decoder(buffer, index);
            long ticks = (long)decoder.Decode64U();
            int resource = (int)decoder.Decode32U();
            ulong salt = decoder.Decode64U();
            ulong counter = decoder.Decode64U();

            var token = new HashCash (
                new DateTime(ticks),
                resource,
                salt,
                counter
            );

            return token;
        }
    }
}