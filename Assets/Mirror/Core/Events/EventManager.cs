using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace Mirror.Core.Events
{
    public static class EventManager
    {
        #region Messages

		/// <summary>
		///     A network message sent to clients from the server.
		/// </summary>
		[Serializable]
		public struct ClientNetworkEventMessage : NetworkMessage
        {

			public NetworkEvent netEvent;

		}

		/// <summary>
		///     A network message sent to the server from clients.
		/// </summary>
		[Serializable]
		public struct ServerNetworkEventMessage : NetworkMessage
        {

			public NetworkEvent netEvent;

			/// <summary>
			///     Should this event be relayed to other clients?
			/// </summary>
			public bool relay;

		}

		#endregion

		#region Server

		/// <summary>
		///     This should be called whenever the server starts.
		/// </summary>
		[Server]
		public static void Server_Register()
        {
			NetworkServer.RegisterHandler<ServerNetworkEventMessage>(Server_OnNetworkEventReceived);
		}

		/// <summary>
		///     This should be called whenever the server stops.
		/// </summary>
		[Server]
		public static void Server_Unregister()
        {
			NetworkServer.UnregisterHandler<ServerNetworkEventMessage>();
		}

		[Server]
		private static void Server_OnNetworkEventReceived(NetworkConnectionToClient connectionToClient, ServerNetworkEventMessage eventMessage)
        {
			InvokeEvent(eventMessage.netEvent);
			if (eventMessage.relay)
				Server_InvokeRpcEvent(eventMessage.netEvent);
		}

		/// <summary>
		///     Invokes an event on the server, then on all clients.
		/// </summary>
		/// <param name="networkEvent"></param>
		[Server]
		public static void Server_InvokeNetworkedEvent(NetworkEvent networkEvent)
        {
			InvokeEvent(networkEvent);
			Server_InvokeRpcEvent(networkEvent, false);
		}

		/// <summary>
		///     Invokes an event on all clients.
		/// </summary>
		/// <param name="networkEvent"></param>
		/// <param name="includeHost">Whether the server host should receive the event. This will NOT invoke the event on dedicated servers.</param>
		[Server]
		public static void Server_InvokeRpcEvent(NetworkEvent networkEvent, bool includeHost = true)
        {
			if (includeHost)
            {
				NetworkServer.SendToAll(new ClientNetworkEventMessage { netEvent = networkEvent });
			}
            else
            { // Send the event to all clients that aren't the host.
				foreach (NetworkConnectionToClient connectionToClient in NetworkServer.connections.Values)
                {
					if (connectionToClient is LocalConnectionToClient)
						continue;

					connectionToClient.Send(new ClientNetworkEventMessage { netEvent = networkEvent });
				}
			}
		}

		/// <summary>
		///     Sends an event from the server to a particular client.
		/// </summary>
		/// <param name="connectionToClient"></param>
		/// <param name="networkEvent"></param>
		[Server]
		public static void Server_InvokeTargetEvent(NetworkConnectionToClient connectionToClient, NetworkEvent networkEvent)
        {
			connectionToClient.Send(new ClientNetworkEventMessage { netEvent = networkEvent });
		}

		/// <summary>
		///     Invokes an event ONLY on the server.
		/// </summary>
		/// <param name="networkEvent"></param>
		[Server]
		public static void Server_InvokeLocalEvent(NetworkEvent networkEvent)
        {
			InvokeEvent(networkEvent);
		}

		#endregion

		#region Client

		/// <summary>
		///     This should be called whenever the client starts.
		/// </summary>
		[Client]
		public static void Client_Register()
        {
			NetworkClient.RegisterHandler<ClientNetworkEventMessage>(Client_OnNetworkEventReceived);
		}

		/// <summary>
		///     This should be called whenever the client stops.
		/// </summary>
		[Client]
		public static void Client_Unregister()
        {
			NetworkClient.UnregisterHandler<ClientNetworkEventMessage>();
		}

		[Client]
		private static void Client_OnNetworkEventReceived(ClientNetworkEventMessage eventMessage)
        {
			InvokeEvent(eventMessage.netEvent);
		}

		/// <summary>
		///     Invokes an event on the server, and optionally, relay it to other clients.
		/// </summary>
		/// <param name="networkEvent"></param>
		/// <param name="relay">When true the event should be relayed to other clients as well.</param>
		[Client]
		public static void Client_InvokeNetworkedEvent(NetworkEvent networkEvent, bool relay = true)
        {
			if (networkEvent.isNetworked)
				NetworkClient.connection.Send(new ServerNetworkEventMessage { netEvent = networkEvent, relay = relay });

			if (!networkEvent.isNetworked || !relay)
				InvokeEvent(networkEvent);
		}

		/// <summary>
		///     Invokes an event ONLY on the local client.
		/// </summary>
		/// <param name="networkEvent"></param>
		[Client]
		public static void Client_InvokeLocalEvent(NetworkEvent networkEvent)
        {
			InvokeEvent(networkEvent);
		}

		#endregion

		#region Shared

		/// <summary>
		/// Key: Event Type<br />
		/// Value:<br />
		///     Key: Object with registered event handlers<br />
		///     Value: Methods registered as event handlers for the event type<br />
		/// </summary>
		private static readonly Dictionary<Type, Dictionary<object, List<MethodInfo>>> listeners = new();

		/// <summary>
		///     Registers all listeners on the provided object.
		/// </summary>
		/// <param name="o"></param>
		public static void RegisterListeners(object o)
        {
			foreach (MethodInfo methodInfo in o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
				List<Attribute> customAttributes = new(methodInfo.GetCustomAttributes());
				if (customAttributes.Count == 0)
					continue;

				if (!NetworkServer.active && customAttributes.Any(attribute => attribute is ServerAttribute))
					continue;
				if (!NetworkClient.active && customAttributes.Any(attribute => attribute is ClientAttribute))
					continue;

				int netEventHandlers = customAttributes.Count(attribute => attribute is NetworkEventHandler);

				//            No switches?
				// ⠀⣞⢽⢪⢣⢣⢣⢫⡺⡵⣝⡮⣗⢷⢽⢽⢽⣮⡷⡽⣜⣜⢮⢺⣜⢷⢽⢝⡽⣝
				// ⠸⡸⠜⠕⠕⠁⢁⢇⢏⢽⢺⣪⡳⡝⣎⣏⢯⢞⡿⣟⣷⣳⢯⡷⣽⢽⢯⣳⣫⠇
				// ⠀⠀⢀⢀⢄⢬⢪⡪⡎⣆⡈⠚⠜⠕⠇⠗⠝⢕⢯⢫⣞⣯⣿⣻⡽⣏⢗⣗⠏⠀
				// ⠀⠪⡪⡪⣪⢪⢺⢸⢢⢓⢆⢤⢀⠀⠀⠀⠀⠈⢊⢞⡾⣿⡯⣏⢮⠷⠁⠀⠀
				// ⠀⠀⠀⠈⠊⠆⡃⠕⢕⢇⢇⢇⢇⢇⢏⢎⢎⢆⢄⠀⢑⣽⣿⢝⠲⠉⠀⠀⠀⠀
				// ⠀⠀⠀⠀⠀⡿⠂⠠⠀⡇⢇⠕⢈⣀⠀⠁⠡⠣⡣⡫⣂⣿⠯⢪⠰⠂⠀⠀⠀⠀
				// ⠀⠀⠀⠀⡦⡙⡂⢀⢤⢣⠣⡈⣾⡃⠠⠄⠀⡄⢱⣌⣶⢏⢊⠂⠀⠀⠀⠀⠀⠀
				// ⠀⠀⠀⠀⢝⡲⣜⡮⡏⢎⢌⢂⠙⠢⠐⢀⢘⢵⣽⣿⡿⠁⠁⠀⠀⠀⠀⠀⠀⠀
				// ⠀⠀⠀⠀⠨⣺⡺⡕⡕⡱⡑⡆⡕⡅⡕⡜⡼⢽⡻⠏⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
				// ⠀⠀⠀⠀⣼⣳⣫⣾⣵⣗⡵⡱⡡⢣⢑⢕⢜⢕⡝⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
				// ⠀⠀⠀⣴⣿⣾⣿⣿⣿⡿⡽⡑⢌⠪⡢⡣⣣⡟⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
				// ⠀⠀⠀⡟⡾⣿⢿⢿⢵⣽⣾⣼⣘⢸⢸⣞⡟⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
				// ⠀⠀⠀⠀⠁⠇⠡⠩⡫⢿⣝⡻⡮⣒⢽⠋⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀

				if (netEventHandlers == 0)
					continue;

				if (netEventHandlers > 1)
                {
#if UNITY_EDITOR
					Debug.LogError($"Tried to register an event that has more than one NetworkEventHandler attribute! Method: {o.GetType().Name}#{methodInfo.Name}");
#endif
					continue;
				}

				if (methodInfo.GetParameters().Length != 1)
                {
#if UNITY_EDITOR
					Debug.LogError($"Tried to register a NetworkEventHandler that has an incorrect amount of parameters! Method: {o.GetType().Name}#{methodInfo.Name}, Parameters: {methodInfo.GetParameters().Select(x => x.ParameterType.Name).Stringify()}");
#endif
					continue;
				}

				Type type = methodInfo.GetParameters().First().ParameterType;

				if (!type.Extends(typeof(NetworkEvent)))
                {
#if UNITY_EDITOR
					Debug.LogError($"Tried to register a NetworkEventHandler that has a non-NetworkEvent as a parameter! Method: {o.GetType().Name}#{methodInfo.Name}, Parameter: {type.Name}");
#endif
					continue;
				}

				if (methodInfo.ReturnType != typeof(void))
                {
#if UNITY_EDITOR
					Debug.LogError($"Tried to register a NetworkEventHandler that doesn't return void! Method: {o.GetType().Name}#{methodInfo.Name}");
#endif
					continue;
				}

				Dictionary<object, List<MethodInfo>> registeredObjectMap = listeners.GetValueOrDefault(type, new Dictionary<object, List<MethodInfo>>());
				List<MethodInfo> registeredMethods = registeredObjectMap.GetValueOrDefault(o, new List<MethodInfo>());
				registeredMethods.Add(methodInfo);

				if (registeredObjectMap.ContainsKey(o))
					registeredObjectMap[o] = registeredMethods;
				else
					registeredObjectMap.Add(o, registeredMethods);

				if (listeners.ContainsKey(type))
					listeners[type] = registeredObjectMap;
				else
					listeners.Add(type, registeredObjectMap);
			}
		}

		/// <summary>
		///     Unregisters all listeners on the provided object.
		/// </summary>
		/// <param name="o"></param>
		public static void UnregisterListeners(object o)
        {
			foreach ((Type _, Dictionary<object, List<MethodInfo>> objects) in listeners)
            {
				if (!objects.ContainsKey(o))
					continue;

				objects.Remove(o);
			}
		}

		private static void InvokeEvent(NetworkEvent networkEvent)
        {
			Dictionary<object, List<MethodInfo>> methods;

			if (!listeners.TryGetValue(networkEvent.GetType(), out methods))
				return;

			MethodInfo curr = null;
#if UNITY_EDITOR
			try
            {
#endif
				foreach ((object key, List<MethodInfo> value) in methods)
                {
					foreach (MethodInfo methodInfo in value)
                    {
						curr = methodInfo;
						methodInfo.Invoke(key, new object[] { networkEvent });
					}
				}
#if UNITY_EDITOR
			}
            catch (InvalidOperationException)
            {
				if (curr != null && curr.DeclaringType != null)
                {
					Debug.LogError($"Events cannot be registered or unregistered in event handlers! Method: {curr.DeclaringType.Name}#{curr.Name}");
				}
				throw;
			}
            catch (TargetInvocationException e)
            {
				if (curr != null && curr.DeclaringType != null)
                {
					Debug.LogError($"An exception occured in an event handler! Method: {curr.DeclaringType.Name}#{curr.Name}");
				}

				if (e.InnerException != null)
					ExceptionDispatchInfo.Capture(e.InnerException).Throw();
			}
#endif
		}

		#endregion

		#region Serialization

		public static void WriteNetworkEvent(this NetworkWriter writer, NetworkEvent networkEvent)
        {
			networkEvent.Write(writer);
		}

		public static NetworkEvent ReadNetworkEvent(this NetworkReader reader)
        {
			Type type = Type.GetType(reader.ReadString());
			if (type == null)
            {
				Debug.LogError("Type cannot be null!");
				type = typeof(NetworkEvent);
			}

			NetworkEvent networkEvent = (NetworkEvent) Activator.CreateInstance(type);
			networkEvent.Read(reader);

			return networkEvent;
		}

		#endregion

    }
}
