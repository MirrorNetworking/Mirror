using System;
using System.IO;

namespace FossilDeltaX
{
	public class Delta
	{
		// from docs: "The value of this parameter has to be a power of two."
		public const ushort HASH_SIZE = 16;

		// insert: <<command, size, bytes>>
		static void WriteInsert(Stream writer, ArraySegmentX<byte> segment, int offset, int count)
		{
			// varint for minium bandwidth
			writer.WriteByte(Command.INSERT);
			writer.WriteVarInt((uint)count);
			// write ArraySegment .Array but consider .Offset
			writer.Write(segment.Array, segment.Offset + offset, count);
		}

		// copy: <<command, count, offset>>
		static void WriteCopy(Stream writer, int count, int offset)
		{
			// varint for minium bandwidth
			writer.WriteByte(Command.COPY);
			writer.WriteVarInt((uint)count);
			writer.WriteVarInt((uint)offset);
		}

		// Compute the hash table used to locate matching sections in the source.
		// -> RollingHash is a struct to avoid allocations. pass as ref!
		// -> collide[] & landmark[] are reusable & resized if necessary.
		internal static void ComputeHashTable(ArraySegmentX<byte> A, ref RollingHash hash, out int nHash, ref int[] collide, ref int[] landmark)
		{
			nHash = A.Count / HASH_SIZE;

			// instead of allocating, resize the reusable array only if we need
			// a larger one. next time we won't resize & allocate then.
			// NOTE that 'stackalloc' would limit us to around 1 MB A[].
			//      we want this to work with any size without StackOverflow.
			// NOTE that passing a List<int> would be 30% slower.
			if (collide.Length < nHash) Array.Resize(ref collide, nHash);
			if (landmark.Length < nHash) Array.Resize(ref landmark, nHash);

			// initialize arrays.
			// note that their size might be larger than nHash from last time.
			// we only care about the size until 'nhash'.
			for (int i = 0; i < nHash; i++)
			{
				collide[i] = -1;
				landmark[i] = -1;
			}

			// compute hashes for every HASH_SIZEd chunk.
			// note that this does nothing if A.Length is exactly HASH_SIZE
			// because 'i < ...'.
			// that's what the original code does, it's not obvious why.
			for (int i = 0; i < A.Count - HASH_SIZE; i += HASH_SIZE)
			{
				hash.Init(A, i);
				int hv = (int)(hash.Value() % nHash);
				collide[i/HASH_SIZE] = landmark[hv];
				landmark[hv] = i/HASH_SIZE;
			}
		}

		// match result for the search algorithm
		struct Match
		{
			public int cnt;
			public int offset;
			public int litsz;

			public Match(int cnt, int offset, int litsz)
			{
				this.cnt = cnt;
				this.offset = offset;
				this.litsz = litsz;
			}
		}

		// magic search function
		// -> collide[] & landmark[] are reusable & resized if necessary.
		//    even though they might be larger, only use them from [0..nhash]!
		static bool Search(ArraySegmentX<byte> A, ArraySegmentX<byte> B, uint hashValue, int nHash, int[] collide, int[] landmark,
			int _base, int i,
			out Match match)
		{
			bool anyFound = false;
			match = new Match(0, 0, 0);

			int limit = 250;
			int hv = (int)(hashValue % nHash);
			int blockIndex = landmark[hv];
			while (blockIndex >= 0 && (limit--) > 0)
			{
				//
				// The hash window has identified a potential match against
				// landmark block iBlock. But we need to investigate further.
				//
				// Look for a region in zOut that matches zSrc. Anchor the search
				// at zSrc[iSrc] and zOut[_base+i]. Do not include anything prior to
				// zOut[_base] or after zOut[outLen] nor anything after zSrc[srcLen].
				//
				// Set cnt equal to the length of the match and set offset so that
				// zSrc[offset] is the first element of the match. litsz is the number
				// of characters between zOut[_base] and the beginning of the match.
				// sz will be the overhead (in bytes) needed to encode the copy
				// command. Only generate copy command if the overhead of the
				// copy command is less than the amount of literal text to be copied.
				//
				int j, k, x, y;

				// Beginning at iSrc, match forwards as far as we can.
				// j counts the number of characters that match.
				int iSrc = blockIndex * HASH_SIZE;
				for (j = 0, x = iSrc, y = _base+i; x < A.Count && y < B.Count; j++, x++, y++)
				{
					if (A.Array[A.Offset + x] != B.Array[B.Offset + y]) break;
				}
				j--;

				// Beginning at iSrc-1, match backwards as far as we can.
				// k counts the number of characters that match.
				for (k = 1; k < iSrc && k <= i; k++)
				{
					if (A.Array[A.Offset + iSrc-k] != B.Array[B.Offset + _base+i-k]) break;
				}
				k--;

				// Compute the offset and size of the matching region.
				int offset = iSrc - k;
				int cnt = j + k + 1;
				// Number of bytes of literal text before the copy
				int litsz = i - k;
				// sz will hold the number of bytes needed to encode the "insert"
				// command and the copy command, not counting the "insert" text.
				// -> we use varint, so we need to calculate byte sizes here
				// TODO make sure i-k is always >0 otherwise varint is too big
				int sz = VarInt.PredictSize((ulong)(i-k)) + VarInt.PredictSize((ulong)cnt) + VarInt.PredictSize((ulong)offset) + 3;
				if (cnt >= sz && cnt > match.cnt)
				{
					// Remember this match only if it is the best so far and it
					// does not increase the file size.
					match = new Match(cnt, iSrc-k, litsz);
					anyFound = true;
				}

				// Check the next matching block
				blockIndex = collide[blockIndex];
			}

			return anyFound;
		}

		// creates diff between A and B, returns result
		// (byte[] version for ease of use)
		public static byte[] Create(byte[] A, byte[] B)
		{
			MemoryStream stream = new MemoryStream();
			int[] collide = new int[0];
			int[] landmark = new int[0];
			Create(new ArraySegmentX<byte>(A), new ArraySegmentX<byte>(B), ref collide, ref landmark, stream);
			return stream.ToArray();
		}

		// creates diff between A and B, writes diff into 'result' stream
		// (stream version avoids allocations)
		// => MemoryStream / Mirror's NetworkWriter etc. can be passed here!
		// => ArraySegment so inputs can come from Streams / NetworkReader etc.
		//    without having to copy the _exact_ part into an array.
		// => ArraySegmentX is 3.76x faster than ArraySegment here.
		// -> collide[] & landmark[] are reusable & resized if necessary.
		public static void Create(ArraySegmentX<byte> A, ArraySegmentX<byte> B, ref int[] collide, ref int[] landmark, Stream result)
		{
			int lastRead = -1;

			// If the source is very small, it means that we have no
			// chance of ever doing a copy command. Just output a single
			// literal segment for the entire target and exit.
			if (A.Count <= HASH_SIZE)
			{
				WriteInsert(result, B, 0, B.Count);
				return;
			}

			// Compute the hash table used to locate matching sections in the source.
			RollingHash hash = new RollingHash();
			ComputeHashTable(A, ref hash, out int nHash, ref collide, ref landmark);

			// _base seems to be the offset of current chunk
			int _base = 0;
			while (_base + HASH_SIZE < B.Count)
			{
				hash.Init(B, _base);
				int i = 0;
				while (true)
				{
					// search best match (if any)
					uint hashValue = hash.Value();
					bool found = Search(A, B, hashValue, nHash, collide, landmark, _base, i, out Match match);

					// We have a copy command that does not cause the delta to be larger
					// than a literal insert. So add the copy command to the delta.
					if (found)
					{
						if (match.litsz > 0)
						{
							// Add an insert command before the copy.
							WriteInsert(result, B, _base, match.litsz);
							_base += match.litsz;
						}
						_base += match.cnt;

						// COPY command
						WriteCopy(result, match.cnt, match.offset);
						if (match.offset + match.cnt -1 > lastRead)
						{
							lastRead = match.offset + match.cnt - 1;
						}
						break;
					}

					// If we reach this point, it means no match is found so far

					// reached the end? and not found any matches?
					if (_base + i + HASH_SIZE >= B.Count)
					{
						// do an "insert" for everything that does not match
						WriteInsert(result, B, _base, B.Count - _base);
						_base = B.Count;
						break;
					}

					// no match found, but not at the end yet.
					// Advance the hash by one character. Keep looking for a match.
					hash.Next(B.Array[B.Offset + _base + i + HASH_SIZE]);
					i++;
				}
			}

			// Output a final "insert" record to get all the text at the end of
			// the file that does not match anything in the source.
			if (_base < B.Count)
			{
				WriteInsert(result, B, _base, B.Count - _base);
			}
		}

		// Reader is a struct to avoid allocations. pass as 'ref'.
		static void ProcessCopyCommand(ArraySegmentX<byte> A, ref Reader reader, Stream writer, ref uint total)
		{
			uint count = (uint)reader.ReadVarInt();
			uint offset = (uint)reader.ReadVarInt();

			total += count;
			// 'limit' header was removed to reduce bandwidth
			//if (total > limit)
			//	throw new Exception("copy exceeds output file size");
			if (offset + count > A.Count)
				throw new Exception("copy extends past end of input");

			// write A at offset with count.
			// need to include ArraySegment.Offset when accessing the .Array.
			writer.Write(A.Array, A.Offset + (int)offset, (int)count);
		}

		// Reader is a struct to avoid allocations. pass as 'ref'.
		static void ProcessInsertCommand(ArraySegmentX<byte> delta, ref Reader reader, Stream writer, ref uint total)
		{
			uint count = (uint)reader.ReadVarInt();

			total += count;

			// 'limit' header was removed to reduce bandwidth
			//if (total > limit)
			//	throw new Exception("insert command gives an output larger than predicted");
			if (count > delta.Count)
				throw new Exception("insert count exceeds size of delta");

			// write buffer at offset with count.
			// need to include ArraySegment.Offset when accessing the .Array.
			writer.Write(reader.buffer.Array, reader.buffer.Offset + (int)reader.Position, (int)count);
			reader.Position += count;
		}

		// applies delta to A and returns the result.
		// (byte[] version for ease of use)
		public static byte[] Apply(byte[] A, byte[] delta)
		{
			MemoryStream stream = new MemoryStream();
			Apply(new ArraySegmentX<byte>(A), new ArraySegmentX<byte>(delta), stream);
			return stream.ToArray();
		}

		// applies delta to A and writes it into result stream
		// (stream version avoids allocations)
		// => MemoryStream / Mirror's NetworkWriter etc. can be passed here!
		// => ArraySegment so inputs can come from Streams / NetworkReader etc.
		//    without having to copy the _exact_ part into an array.
		// => ArraySegmentX is 3.76x faster than ArraySegment here.
		public static void Apply(ArraySegmentX<byte> A, ArraySegmentX<byte> delta, Stream result)
		{
			uint total = 0;
			Reader deltaReader = new Reader(delta);

			while (deltaReader.HaveBytes())
			{
				int command = deltaReader.ReadByte();
				switch (command)
				{
					case Command.COPY:
					{
						ProcessCopyCommand(A, ref deltaReader, result, ref total);
						break;
					}
					case Command.INSERT:
					{
						ProcessInsertCommand(delta, ref deltaReader, result, ref total);
						break;
					}
					default:
					{
						throw new Exception($"unknown delta operator: 0x{command:X2}");
					}
				}
			}
		}
	}
}