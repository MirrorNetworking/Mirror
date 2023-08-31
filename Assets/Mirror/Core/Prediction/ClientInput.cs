using System;

namespace Mirror
{
    public struct ClientInput
    {
        // inputs need a unique ID for acknowledgement from the server.
        // TODO this can be optimized later, for now let's stay with uint.
        public uint inputId;

        // TODO this definitely needs to be optimized later. maybe with hash?
        public string input;

        // serialized parameters for the input, i.e. position.
        // TODO NONALLOC/POOLED
        public NetworkWriter parameters;

        public double timestamp;

        // UNRELIABLE:
        // keep track of how many times we attempted to send this input unreliably
        // this is useful to detect issues.
        // public int sendAttempts;

        // UNRELIABLE:
        // inputs are sent over unreliable.
        // server will tell us when it received an input with inputId.
        // in that case, set acked and don't retransmit.
        // public bool acked;

        public ClientInput(uint inputId, string input, NetworkWriter parameters, double timestamp)
        {
            this.inputId      = inputId;
            this.input        = input;
            this.parameters   = parameters;
            this.timestamp    = timestamp;
            // UNRELIABLE:
            // this.sendAttempts = 0;
            // this.acked        = false;
        }
    }

    // add NetworkReader/Writer extensions for ClientInput type
    public static class InputAccumulatorSerialization
    {
        public static void WriteClientInput(this NetworkWriter writer, ClientInput input)
        {
            writer.WriteUInt(input.inputId);
            writer.WriteString(input.input);
            writer.WriteArraySegmentAndSize(input.parameters);
            writer.WriteDouble(input.timestamp);
        }

        public static ClientInput ReadClientInput(this NetworkReader reader)
        {
            uint inputId                  = reader.ReadUInt();
            string input                  = reader.ReadString();
            ArraySegment<byte> parameters = reader.ReadArraySegmentAndSize();
            double timestamp              = reader.ReadDouble();

            // wrap parameter bytes in a writer.
            // if there were no parameters, 'parameters' is default/null.
            // in that case, don't copy anything otherwise we get a nullref.
            NetworkWriter writer = new NetworkWriter();
            if (parameters.Array != null)
            {
                writer.WriteBytes(parameters.Array, parameters.Offset, parameters.Count);
            }
            return new ClientInput(inputId, input, writer, timestamp);
        }
    }
}
