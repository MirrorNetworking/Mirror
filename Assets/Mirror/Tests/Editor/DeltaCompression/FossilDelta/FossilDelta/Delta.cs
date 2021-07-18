using System;
using System.IO;

namespace Fossil
{
	public class Delta
	{
		public static UInt16 NHASH = 16;

		public static byte[] Create(byte[] origin, byte[] target) {
			var zDelta = new Writer();
			int lenOut = target.Length;
			int lenSrc = origin.Length;
			int i, lastRead = -1;

			zDelta.PutInt((uint) lenOut);
			zDelta.PutChar('\n');

			// If the source is very small, it means that we have no
			// chance of ever doing a copy command.  Just output a single
			// literal segment for the entire target and exit.
			if (lenSrc <= NHASH) {
				zDelta.PutInt((uint) lenOut);
				zDelta.PutChar(':');
				zDelta.PutArray(target, 0, lenOut);
				zDelta.PutInt(Checksum(target));
				zDelta.PutChar(';');
				return zDelta.ToArray();
			}

			// Compute the hash table used to locate matching sections in the source.
			int nHash = (int) lenSrc / NHASH;
			int[] collide =  new int[nHash];
			int[] landmark = new int[nHash];
			for (i = 0; i < collide.Length; i++) collide[i] = -1;
			for (i = 0; i < landmark.Length; i++) landmark[i] = -1;
			int hv;
			RollingHash h = new RollingHash();
			for (i = 0; i < lenSrc-NHASH; i += NHASH) {
				h.Init(origin, i);
				hv = (int) (h.Value() % nHash);
				collide[i/NHASH] = landmark[hv];
				landmark[hv] = i/NHASH;
			}

			int _base = 0;
			int iSrc, iBlock;
			int bestCnt, bestOfst=0, bestLitsz=0;
			while (_base+NHASH<lenOut) {
				bestOfst=0;
				bestLitsz=0;
				h.Init(target, _base);
				i = 0; // Trying to match a landmark against zOut[_base+i]
				bestCnt = 0;
				while (true) {
					int limit = 250;
					hv = (int) (h.Value() % nHash);
					iBlock = landmark[hv];
					while (iBlock >= 0 && (limit--)>0 ) {
						//
						// The hash window has identified a potential match against
						// landmark block iBlock.  But we need to investigate further.
						//
						// Look for a region in zOut that matches zSrc. Anchor the search
						// at zSrc[iSrc] and zOut[_base+i].  Do not include anything prior to
						// zOut[_base] or after zOut[outLen] nor anything after zSrc[srcLen].
						//
						// Set cnt equal to the length of the match and set ofst so that
						// zSrc[ofst] is the first element of the match.  litsz is the number
						// of characters between zOut[_base] and the beginning of the match.
						// sz will be the overhead (in bytes) needed to encode the copy
						// command.  Only generate copy command if the overhead of the
						// copy command is less than the amount of literal text to be copied.
						//
						int cnt, ofst, litsz;
						int j, k, x, y;
						int sz;

						// Beginning at iSrc, match forwards as far as we can.
						// j counts the number of characters that match.
						iSrc = iBlock*NHASH;
						for (j = 0, x = iSrc, y = _base+i; x < lenSrc && y < lenOut; j++, x++, y++) {
							if (origin[x] != target[y]) break;
						}
						j--;

						// Beginning at iSrc-1, match backwards as far as we can.
						// k counts the number of characters that match.
						for (k = 1; k < iSrc && k <= i; k++) {
							if (origin[iSrc-k] != target[_base+i-k]) break;
						}
						k--;

						// Compute the offset and size of the matching region.
						ofst = iSrc-k;
						cnt = j+k+1;
						litsz = i-k;  // Number of bytes of literal text before the copy
						// sz will hold the number of bytes needed to encode the "insert"
						// command and the copy command, not counting the "insert" text.
						sz = DigitCount(i-k)+DigitCount(cnt)+DigitCount(ofst)+3;
						if (cnt >= sz && cnt > bestCnt) {
							// Remember this match only if it is the best so far and it
							// does not increase the file size.
							bestCnt = cnt;
							bestOfst = iSrc-k;
							bestLitsz = litsz;
						}

						// Check the next matching block
						iBlock = collide[iBlock];
					}

					// We have a copy command that does not cause the delta to be larger
					// than a literal insert.  So add the copy command to the delta.
					if (bestCnt > 0) {
						if (bestLitsz > 0) {
							// Add an insert command before the copy.
							zDelta.PutInt((uint) bestLitsz);
							zDelta.PutChar(':');
							zDelta.PutArray(target, _base, _base+bestLitsz);
							_base += bestLitsz;
						}
						_base += bestCnt;
						zDelta.PutInt((uint) bestCnt);
						zDelta.PutChar('@');
						zDelta.PutInt((uint) bestOfst);
						zDelta.PutChar(',');
						if (bestOfst + bestCnt -1 > lastRead) {
							lastRead = bestOfst + bestCnt - 1;
						}
						bestCnt = 0;
						break;
					}

					// If we reach this point, it means no match is found so far
					if (_base+i+NHASH >= lenOut){
						// We have reached the end and have not found any
						// matches.  Do an "insert" for everything that does not match
						zDelta.PutInt((uint) (lenOut-_base));
						zDelta.PutChar(':');
						zDelta.PutArray(target, _base, _base+lenOut-_base);
						_base = lenOut;
						break;
					}

					// Advance the hash by one character. Keep looking for a match.
					h.Next(target[_base+i+NHASH]);
					i++;
				}
			}
			// Output a final "insert" record to get all the text at the end of
			// the file that does not match anything in the source.
			if(_base < lenOut) {
				zDelta.PutInt((uint) (lenOut-_base));
				zDelta.PutChar(':');
				zDelta.PutArray(target, _base, _base+lenOut-_base);
			}
			// Output the final checksum record.
			zDelta.PutInt(Checksum(target));
			zDelta.PutChar(';');
			return zDelta.ToArray();
		}

		public static byte[] Apply(byte[] origin, byte[] delta) {
			uint limit, total = 0;
			uint lenSrc = (uint) origin.Length;
			uint lenDelta = (uint) delta.Length;
			Reader zDelta = new Reader(delta);

			limit = zDelta.GetInt();
			if (zDelta.GetChar() != '\n')
				throw new Exception("size integer not terminated by \'\\n\'");

			Writer zOut = new Writer();
			while(zDelta.HaveBytes()) {
				uint cnt, ofst;
				cnt = zDelta.GetInt();

				switch (zDelta.GetChar()) {
				case '@':
					ofst = zDelta.GetInt();
					if (zDelta.HaveBytes() && zDelta.GetChar() != ',')
						throw new Exception("copy command not terminated by \',\'");
					total += cnt;
					if (total > limit)
						throw new Exception("copy exceeds output file size");
					if (ofst+cnt > lenSrc)
						throw new Exception("copy extends past end of input");
					zOut.PutArray(origin, (int) ofst, (int) (ofst+cnt));
					break;

				case ':':
					total += cnt;
					if (total > limit)
						throw new Exception("insert command gives an output larger than predicted");
					if (cnt > lenDelta)
						throw new Exception("insert count exceeds size of delta");
					zOut.PutArray(zDelta.a, (int) zDelta.pos, (int) (zDelta.pos+cnt));
					zDelta.pos += cnt;
					break;

				case ';':
					byte[] output = zOut.ToArray();
					if (cnt != Checksum(output))
						throw new Exception("bad checksum");
					if (total != limit)
						throw new Exception("generated size does not match predicted size");
					return output;

				default:
					throw new Exception("unknown delta operator");
				}
			}
			throw new Exception("unterminated delta");
		}

		public static void Apply(Stream origin, byte[] delta, Stream target) {
			uint limit, total = 0;
			uint lenSrc = (uint) origin.Length;
			uint lenDelta = (uint) delta.Length;
			Reader zDelta = new Reader(delta);

			limit = zDelta.GetInt();
			if (zDelta.GetChar() != '\n')
				throw new Exception("size integer not terminated by \'\\n\'");

			uint checksum = 0;
			const int BufferSize = 64 * 1024;
			// We need additional 4 for bytes that might remain unprocessed from previous loop traversal
			var buffer = new byte[BufferSize + 4];
			int remainingChecksumBytes = 0;
			while(zDelta.HaveBytes()) {
				uint cnt, ofst;
				cnt = zDelta.GetInt();

				switch (zDelta.GetChar()) {
					case '@':
						ofst = zDelta.GetInt();
						if (zDelta.HaveBytes() && zDelta.GetChar() != ',')
							throw new Exception("copy command not terminated by \',\'");
						total += cnt;
						if (total > limit)
							throw new Exception("copy exceeds output file size");
						if (ofst+cnt > lenSrc)
							throw new Exception("copy extends past end of input");

						origin.Position = ofst;
						int remainingBytes = (int) cnt;
						while (remainingBytes > 0)
						{
							int totalRead = origin.Read(buffer, remainingChecksumBytes, Math.Min(remainingBytes, BufferSize));
							remainingBytes -= totalRead;
							target.Write(buffer, remainingChecksumBytes, totalRead);

							totalRead += remainingChecksumBytes;

							remainingChecksumBytes = totalRead % 4;
							int checksumBytes = totalRead - remainingChecksumBytes;
							checksum = Checksum(buffer, checksumBytes, checksum);
							for (int i = 0; i < remainingChecksumBytes; i++)
							{
								buffer[i] = buffer[i + checksumBytes];
							}
						}
						break;

					case ':':
						total += cnt;
						if (total > limit)
							throw new Exception("insert command gives an output larger than predicted");
						if (cnt > lenDelta)
							throw new Exception("insert count exceeds size of delta");
						remainingBytes = (int) cnt;
						int pos = (int) zDelta.pos;
						while (remainingBytes > 0)
						{
							var totalCopied = Math.Min(remainingBytes, BufferSize);
							Array.Copy(zDelta.a, pos, buffer, remainingChecksumBytes, totalCopied);
							remainingBytes -= totalCopied;

							totalCopied += remainingChecksumBytes;

							remainingChecksumBytes = totalCopied % 4;
							int checksumBytes = totalCopied - remainingChecksumBytes;
							checksum = Checksum(buffer, checksumBytes, checksum);
							for (int i = 0; i < remainingChecksumBytes; i++)
							{
								buffer[i] = buffer[i + checksumBytes];
							}
						}

						target.Write(zDelta.a, (int) zDelta.pos, (int) cnt);
						zDelta.pos += cnt;
						break;

					case ';':
						if (remainingChecksumBytes > 0) checksum = Checksum(buffer, remainingChecksumBytes, checksum);
						if (cnt != checksum)
							throw new Exception("bad checksum");
						if (total != limit)
							throw new Exception("generated size does not match predicted size");
						return;

					default:
						throw new Exception("unknown delta operator");
				}
			}
			throw new Exception("unterminated delta");
		}

		public static uint OutputSize(byte[] delta) {
			Reader zDelta = new Reader(delta);
			uint size = zDelta.GetInt();
			if (zDelta.GetChar() != '\n')
				throw new Exception("size integer not terminated by \'\\n\'");
			return size;
		}

		static int DigitCount(int v){
			int i, x;
			for(i=1, x=64; v>=x; i++, x <<= 6){}
			return i;
		}

		// Return a 32-bit checksum of the array.
		static uint Checksum(byte[] arr, int count = 0, uint sum = 0) {
			uint sum0 = 0, sum1 = 0, sum2 = 0,
			z = 0, N = (uint) (count == 0 ? arr.Length : count);

			while(N >= 16){
				sum0 += (uint) arr[z+0] + arr[z+4] + arr[z+8]  + arr[z+12];
				sum1 += (uint) arr[z+1] + arr[z+5] + arr[z+9]  + arr[z+13];
				sum2 += (uint) arr[z+2] + arr[z+6] + arr[z+10] + arr[z+14];
				sum  += (uint) arr[z+3] + arr[z+7] + arr[z+11] + arr[z+15];
				z += 16;
				N -= 16;
			}
			while(N >= 4){
				sum0 += arr[z+0];
				sum1 += arr[z+1];
				sum2 += arr[z+2];
				sum  += arr[z+3];
				z += 4;
				N -= 4;
			}

			sum += (sum2 << 8) + (sum1 << 16) + (sum0 << 24);
			switch (N&3) {
			case 3:
				sum += (uint) (arr [z + 2] << 8);
				sum += (uint) (arr [z + 1] << 16);
				sum += (uint) (arr [z + 0] << 24);
				break;
			case 2:
				sum += (uint) (arr [z + 1] << 16);
				sum += (uint) (arr [z + 0] << 24);
				break;
			case 1:
				sum += (uint) (arr [z + 0] << 24);
				break;
			}
			return sum;
		}

	}
}