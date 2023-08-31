// Input Accumulator for prediction.
//
// everything goes over reliable channel for now: make it work, then make it fast!
//
// in the future we want to send client inputs to the server immediately over
// unreliable. some will get lost, so we always want to send the last N inputs
// at once.
//
// based on Overwatch GDC talk: https://www.youtube.com/watch?v=zrIY0eIyqmI
//
// usage:
// - inherit and customize this for your player
// - add the component to the player prefab
// - channel all inputs through this component
//   for example, when firing call GetComponent<InputAccumulator>().Fire()
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public abstract class InputAccumulator : NetworkBehaviour
    {
        uint nextInputId = 1;

        // history limit as byte to enforce max 255 (which saves bandwidth)
        [Tooltip("Keep this many inputs to sync to server in a batch, and to apply reconciliation.\nDon't change this at runtime!")]
        public byte historyLimit = 64;

        // UNRELIABLE:
        // [Tooltip("How many times the client will attempt to (unreliably) send an input before giving up.")]
        // public int attemptLimit = 16;

        // input history with both acknowledged and unacknowledged inputs.
        // => unacknowledged inputs are still being resent
        // => acknowledged are kept for later in case of reconciliation
        internal readonly Queue<ClientInput> history = new Queue<ClientInput>();

        double lastSendTime;

        // record input by name, i.e. "Fire".
        // make sure to use const strings like "Fire" to avoid allocations.
        // "Fire{i}" would allocate.

        // returns true if there was space in history, false otherwise.
        // if it returns false, it's best not to apply the player input to the world.
        protected bool RecordInput(string inputName, NetworkWriter parameters)
        {
            // keep history limit
            if (history.Count >= historyLimit)
            {
                // the oldest entry is only safe to dequeue if the server acked it.
                // otherwise the server wouldn't never receive & apply it.
                // if (!history.Peek().acked)
                // {
                //     // best to warn and drop it.
                //     Debug.LogWarning($"Input {inputName} on {name} with netId={netId} will be dropped because history is full and the oldest input hasn't been acknowledged yet.");
                //     return false;
                // }

                history.Dequeue();
            }

            // record it with a new input id
            ClientInput input = new ClientInput(nextInputId, inputName, parameters, NetworkTime.time);
            nextInputId += 1;
            history.Enqueue(input);

            // send it to the server over reliable for now.
            // in the future, N inputs will be squashed into one unreliable message.
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.Write(input);
                CmdSendInput(writer);
            }

            return true;
        }

        // called on server after receiving a new client input.
        // TODO pass batch remoteTime? server knows this but may be easier to pass here too.
        protected abstract void ApplyInputOnServer(string inputName);

        [Command]
        void CmdSendInput(ArraySegment<byte> serializedInput)
        {
            // deserialize input
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(serializedInput))
            {
                ClientInput input = reader.ReadClientInput();

                // UNRELIABLE
                // send ack message to client.
                // at the moment this is sent reliabily.
                // TODO keep history of acked so we don't send twice?
                // client may still send it a few times before it gets ack.
                // TargetSendInputAck(input.inputId);

                // process the input on server
                ApplyInputOnServer(input.input);
            }
        }

        // UNRELIABLE
        /*
        // squash all history inputs and send them to the server in one unreliable message
        protected void Flush()
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // write each client input that the server hasn't acknowledged yet.
                // we are also flagging each input's send attempts.
                // which means that we need to iterate with dequeue/enqueue again.
                // foreach can't modify while iterating.

                //   foreach (ClientInput input in history)
                //   {
                //       if (!input.acked)
                //           writer.WriteClientInput(input);
                //   }

                int count = history.Count;
                for (int i = 0; i < count; ++i)
                {
                    ClientInput input = history.Dequeue();
                    if (!input.acked)
                    {
                        writer.WriteClientInput(input);
                        input.sendAttempts += 1;

                        // give up after too many attempts
                        if (input.sendAttempts > attemptLimit)
                        {
                            // TODO maybe this should disconnect?
                            Debug.LogWarning($"Input {input.input} on {name} with netId={netId} will be dropped because it was sent {input.sendAttempts} times and never acknowledged by the server.");
                            continue; // continue to the next, don't Enqueue
                        }
                    }
                    history.Enqueue(input);
                }

                CmdSendInputBatch(writer.ToArraySegment());
            }
        }

        bool IsNewInputId(uint inputId)
        {
            // TODO faster
            foreach (ClientInput input in history)
            {
                if (input.inputId == inputId)
                    return false;
            }
            return true;
        }

        // prediction should send input immediately over unreliable.
        // latency is key.
        // we batch together the last N inputs to make up for unreliable loss.
        // [Command(channel = Channels.Unreliable)]
        void CmdSendInputBatch(ArraySegment<byte> inputBatch)
        {
            // deserialize inputs
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(inputBatch))
            {
                // read each client input
                while (reader.Remaining > 0)
                {
                    ClientInput input = reader.ReadClientInput();

                    // UDP messages may arrive twice.
                    // only process and apply the same input once though!
                    if (IsNewInputId(input.inputId))
                    {
                        // TODO for unreliable, we need to ensure inputs are applied in same order!

                        // send ack message to client.
                        // at the moment this is sent reliabily.
                        // TODO keep history of acked so we don't send twice?
                        // client may still send it a few times before it gets ack.
                        TargetSendInputAck(input.inputId);

                        // process the input on server
                        ApplyInputOnServer(input.input);
                    }
                }
            }
        }

        // acknowledge an inputId, which flags it as acked on the client.
        // client will then stop sending it to the server.
        // standalone function (not rpc) for easier testing.
        // TODO batch & optimize to minimize bandwidth later
        internal void AcknowledgeInput(uint inputId)
        {
            // we can't modify Queue elements while iterating.
            // we'll have to deqeueue + enqueue each of them once for now.
            // TODO faster lookup?
            int count = history.Count;
            for (int i = 0; i < count; ++i)
            {
                ClientInput input = history.Dequeue();
                if (input.inputId == inputId)
                {
                    // flag as acked, but keep in history for reconciliation later
                    input.acked = true;
                }
                history.Enqueue(input);
            }
        }

        // server sends acknowledgements for received inputs to client
        // TODO unreliable? this isn't latency sensitive though.
        //      with reliable, at least we can guarantee it's gonna be delivered
        [TargetRpc]
        void TargetSendInputAck(uint inputId) => AcknowledgeInput(inputId);

        void Update()
        {
            if (isLocalPlayer)
            {
                // UNRELIABLE
                // TODO we don't have OnSerializeUnreliable yet.
                // send manually for now
                // if (NetworkTime.time >= lastSendTime + syncInterval)
                // {
                //     Flush();
                //     lastSendTime = NetworkTime.time;
                // }
            }
        }
        */
    }
}
