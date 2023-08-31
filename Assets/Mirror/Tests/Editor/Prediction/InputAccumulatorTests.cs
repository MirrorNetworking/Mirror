using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Prediction
{
    class MockInputAccumulator : InputAccumulator
    {
        public List<string> serverInputs = new List<string>();

        // expose protected functions for testing
        public new bool RecordInput(string inputName, NetworkWriter parameters)
            => base.RecordInput(inputName, parameters);

        // public new void Flush()
        //     => base.Flush();

        protected override void ApplyInputOnServer(string inputName)
            => serverInputs.Add(inputName);
    }

    public class InputAccumulatorTests : MirrorTest
    {
        MockInputAccumulator serverComp;
        MockInputAccumulator clientComp;
        const int Limit = 4;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient);

            CreateNetworkedAndSpawnPlayer(
                out _, out _, out serverComp,
                out _, out _, out clientComp,
                connectionToClient);
            serverComp.historyLimit = Limit;
            clientComp.historyLimit = Limit;
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void InputAccumulatorSerialization_WithoutParameters()
        {
            // write an input without any parameters
            NetworkWriter parameters = new NetworkWriter();
            NetworkWriter writer = new NetworkWriter();
            ClientInput input = new ClientInput(42, "Fire", parameters, 2.0);
            writer.WriteClientInput(input);

            // read
            NetworkReader reader = new NetworkReader(writer);
            ClientInput result = reader.ReadClientInput();

            // check
            Assert.That(result.inputId, Is.EqualTo(input.inputId));
            Assert.That(result.input, Is.EqualTo(input.input));
            Assert.That(result.parameters.Position, Is.EqualTo(0));
            Assert.That(result.timestamp, Is.EqualTo(input.timestamp));
        }

        [Test]
        public void InputAccumulatorSerialization_WithParameters()
        {
            // write an input with a few parameters
            NetworkWriter parameters = new NetworkWriter();
            parameters.WriteVector3(new Vector3(1, 2, 3));

            NetworkWriter writer = new NetworkWriter();
            ClientInput input = new ClientInput(42, "Fire", parameters, 2.0);
            writer.WriteClientInput(input);

            // read
            NetworkReader reader = new NetworkReader(writer);
            ClientInput result = reader.ReadClientInput();

            // check
            Assert.That(result.inputId, Is.EqualTo(input.inputId));
            Assert.That(result.input, Is.EqualTo(input.input));
            NetworkReader parametersReader = new NetworkReader(input.parameters);
                Assert.That(parametersReader.Remaining, Is.EqualTo(4 * 3)); // sizeof(Vector3)
                Assert.That(parametersReader.ReadVector3(), Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(result.timestamp, Is.EqualTo(input.timestamp));
        }

        [Test]
        public void Record()
        {
            // record a few
            Assert.That(clientComp.RecordInput("Fire", new NetworkWriter()), Is.True);
            Assert.That(clientComp.RecordInput("Jump", new NetworkWriter()), Is.True);
            ClientInput[] history = clientComp.history.ToArray();
            Assert.That(history.Length, Is.EqualTo(2));
            Assert.That(history[0].input, Is.EqualTo("Fire"));
            Assert.That(history[0].inputId, Is.EqualTo(1));
            Assert.That(history[1].input, Is.EqualTo("Jump"));
            Assert.That(history[1].inputId, Is.EqualTo(2));
        }

        /*
        // recording more inputs than 'limit' should drop input if not acked.
        [Test]
        public void RecordOverLimit_Unacked()
        {
            // fill to the limit
            for (int i = 0; i < Limit; ++i)
                Assert.That(clientComp.RecordInput($"Fire{i}", new NetworkWriter()), Is.True);

            // try to record another while the oldest is still unacknowledged.
            // input should be dropped, not inserted.
            Assert.That(clientComp.RecordInput("Extra", new NetworkWriter()), Is.False);
            ClientInput[] history = clientComp.history.ToArray();
            Assert.That(history.Length, Is.EqualTo(Limit));
            Assert.That(history[0].input, Is.EqualTo("Fire0"));
        }

        // recording more inputs than 'limit' should drop the oldest (if acked).
        [Test]
        public void RecordOverLimit_Acked()
        {
            // fill to the limit
            for (int i = 0; i < Limit; ++i)
                Assert.That(clientComp.RecordInput($"Fire{i}", new NetworkWriter()), Is.True);

            // acknowledge the oldest
            uint oldestId = clientComp.history.Peek().inputId;
            clientComp.AcknowledgeInput(oldestId);

            // try to record another while the oldest is acknowledged.
            // input should be inserted, and oldest dropped.
            Assert.That(clientComp.RecordInput("Extra", new NetworkWriter()), Is.True);
            ClientInput[] history = clientComp.history.ToArray();
            Assert.That(history.Length, Is.EqualTo(Limit));
            Assert.That(history[0].input, Is.EqualTo("Fire1")); // first one is gone
        }

        [Test]
        public void MaxResendAttempts()
        {
            // insert a few
            Assert.That(clientComp.RecordInput("Fire", new NetworkWriter()), Is.True);
            Assert.That(clientComp.RecordInput("Jump", new NetworkWriter()), Is.True);
            ClientInput[] history = clientComp.history.ToArray();
            Assert.That(history.Length, Is.EqualTo(2));

            // set one of them to max resends
            // can't access queue [i] directly, use dequeue+enqueue instead
            ClientInput oldest = clientComp.history.Dequeue();
            oldest.sendAttempts = clientComp.attemptLimit;
            clientComp.history.Enqueue(oldest);

            // flush should remove the one with too many attempts
            clientComp.Flush();
            history = clientComp.history.ToArray();
            Assert.That(history.Length, Is.EqualTo(1));
            Assert.That(history[0].input, Is.EqualTo("Jump"));
        }
        */

        [Test]
        public void ClientInputGetSyncedToServer()
        {
            // insert a few
            Assert.That(clientComp.RecordInput("Fire", new NetworkWriter()), Is.True);
            Assert.That(clientComp.RecordInput("Jump", new NetworkWriter()), Is.True);

            // flush to server
            // clientComp.Flush();
            ProcessMessages();

            // server should've received the inputs
            // note there's a small chance for unreliable messages to
            // get dropped or arrive out of order, even on localhost.
            Assert.That(serverComp.serverInputs.Count, Is.EqualTo(2));
            Assert.That(serverComp.serverInputs.Contains("Fire")); // UDP order isn't guaranteed
            Assert.That(serverComp.serverInputs.Contains("Jump")); // UDP order isn't guaranteed
        }
    }
}
