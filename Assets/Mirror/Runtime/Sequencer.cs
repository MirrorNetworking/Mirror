/*
The MIT License (MIT)

Copyright (c) 2020 Fredrik Holmstrom
Copyright (c) 2020 Paul Pacheco

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

namespace Mirror
{
    public struct Sequencer
    {
        readonly int shift;
        readonly int bits;
        readonly ulong mask;
        ulong sequence;

        public int Bits => bits;

        public Sequencer(int bits)
        {
            // 1 byte
            // (1 << 8) = 256
            // - 1      = 255
            //          = 1111 1111

            this.bits = bits;
            sequence = 0;
            mask = (1UL << bits) - 1UL;
            shift = sizeof(ulong) * 8 - bits;
        }

        public ulong Next()
        {
            return sequence = NextAfter(sequence);
        }

        public ulong NextAfter(ulong sequence)
        {
            return (sequence + 1UL) & mask;
        }

        public long Distance(ulong from, ulong to)
        {
            to <<= shift;
            from <<= shift;
            return ((long)(from - to)) >> shift;
        }

        // 0 1 2 3 4 5 6 7 8 9 ... 255
        // wraps around back to 0

    }
}