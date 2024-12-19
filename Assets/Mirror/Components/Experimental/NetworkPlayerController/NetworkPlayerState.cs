using System.Runtime.CompilerServices; // do not remove, required to add init support for net4.9 or lower
using System;
using System.Linq;
using UnityEngine;

#nullable enable
namespace Mirror.Components.Experimental{
  public struct NetworkPlayerState{
    /// <summary> Initializes a new instance of the <see cref="NetworkPlayerState"/> struct. Optionally takes defaults to initialize the instance. </summary>
    /// <param name="defaults">Default <see cref="NetworkPlayerState"/> to copy values from, or null for default initialization.</param>
    public NetworkPlayerState(NetworkPlayerState? defaults = null) {
      _tickNumber = defaults?._tickNumber;
      _parent = defaults?._parent;
      _parentId = defaults?._parentId;
      _additionalState = defaults?._additionalState;
      Position = defaults?.Position;
      BaseVelocity = defaults?.BaseVelocity;
      Rotation = defaults?.Rotation;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkPlayerState"/> struct with the specified values.
    /// This private constructor allows internal use for creating instances with selected fields.
    /// </summary>
    /// <param name="tickNumber">The validated tick number for the player's state, or null if unset.</param>
    /// <param name="parent">The parent <see cref="NetworkIdentity"/> instance associated with this state, or null if none.</param>
    /// <param name="parentId">The unique identifier of the parent object, or null if none.</param>
    /// <param name="position">The player's position, or null if unset.</param>
    /// <param name="baseVelocity">The player's base velocity, or null if unset.</param>
    /// <param name="rotation">The player's rotation, or null if unset.</param>
    /// <param name="additionalState">Additional custom state data, or null if unset.</param>
    private NetworkPlayerState(int? tickNumber, NetworkIdentity? parent, uint? parentId, Vector3? position, Vector3? baseVelocity, Quaternion? rotation,
      ReadOnlyMemory<byte>? additionalState) {
      _tickNumber = tickNumber;
      _parent = parent;
      _parentId = parentId;
      Position = position;
      BaseVelocity = baseVelocity;
      Rotation = rotation;
      _additionalState = additionalState;
    }

    /* State Compare and Extend methods */

    #region State Compare and Extend methods

    /// <summary>
    /// Creates a new state instance containing only the fields that have changed compared to the given state.
    /// If no fields differ, returns null.
    /// </summary>
    /// <param name="state">The other NetworkPlayerState to compare with.</param>
    /// <returns>A new NetworkPlayerState instance with only differing values, or null if there are no differences. </returns>
    public NetworkPlayerState? GetChangedStateComparedTo(NetworkPlayerState state) {
      // Compare items that can be sent via network
      uint? parentId = _parentId != state._parentId ? _parentId : null;
      Vector3? position = Position != state.Position ? Position : null;
      Vector3? baseVelocity = BaseVelocity != state.BaseVelocity ? BaseVelocity : null;
      Quaternion? rotation = Rotation != state.Rotation ? Rotation : null;
      ReadOnlyMemory<byte>? additionalState = !ByteArraysEqual(_additionalState, state._additionalState) ? _additionalState : null;

      // Ensure to align the parent NetworkIdentity instance with the changed parent state
      NetworkIdentity? parent = parentId is not null ? _parent : null;

      // If no changes in the network data we return null otherwise we create a new state with the changes
      return parentId is null && position is null && baseVelocity is null && rotation is null && additionalState is null
        ? null
        : new NetworkPlayerState(
          tickNumber: _tickNumber,
          parent: parent,
          parentId: parentId,
          position: position,
          baseVelocity: baseVelocity,
          rotation: rotation,
          additionalState: additionalState
        );
    }

    /// <summary>
    /// Creates a new NetworkPlayerState instance by overriding non-null fields from the given `overrides` instance.
    /// Fields that are null in `overrides` remain unchanged from the current instance.
    /// </summary>
    /// <param name="overrides">The NetworkPlayerState instance providing the overriding values.</param>
    /// <returns>A new NetworkPlayerState instance with fields overridden by non-null values from `overrides`.</returns>
    public NetworkPlayerState OverrideWith(NetworkPlayerState overrides) => new(
      tickNumber: overrides._tickNumber ?? _tickNumber,
      parent: overrides._parentId is not null ? overrides._parent : _parent,
      parentId: overrides._parentId ?? _parentId,
      position: overrides.Position ?? Position,
      baseVelocity: overrides.BaseVelocity ?? BaseVelocity,
      rotation: overrides.Rotation ?? Rotation,
      additionalState: overrides._additionalState ?? _additionalState
    );

    #endregion

    /* Tick Number Handling */

    #region Tick Number Handling

    // Stores the validated tick number for the player's state.
    private int? _tickNumber;

    /// <summary> Represents the tick number for the player's state, validated to be within the range [0, 2047]. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown if the tick number is outside the range [0, 2047]. </exception>
    public int? TickNumber {
        get => _tickNumber;
        set => _tickNumber = value is null or < 0 or > 2047
            ? throw new ArgumentOutOfRangeException(nameof(value), $"Invalid TickNumber: {value}. It must be between 0 and 2047.")
            : value % 2048;
    }

    #endregion

    /* Physics state handling */

    #region Physics state handling

    /// <summary> Represents the player's position in the world. This value does not undergo compression and is directly used as-is. </summary>
    public readonly Vector3? Position { get; init; }

    /// <summary> Represents the player's base velocity in the world. This value does not undergo compression and is directly used as-is. </summary>
    public readonly Vector3? BaseVelocity { get; init; }

    /// <summary> Represents the player's rotation in the world. This value does not undergo compression and is directly used as-is. </summary>
    public readonly Quaternion? Rotation { get; init; }

    #endregion

    /* Additional state handling */

    #region Additional state handling

    //Stores additional state defined by the developer, allowing custom byte data.
    private readonly ReadOnlyMemory<byte>? _additionalState;

    /// <summary> Stores additional state defined by the developer, allowing custom byte data. Empty byte arrays are not allowed. </summary>
    /// <exception cref="ArgumentException">Thrown if the byte array is empty.</exception>
    public readonly ReadOnlyMemory<byte>? AdditionalState {
      get => _additionalState;
      init => _additionalState = value is null
        ? _additionalState
        : value is { Length: 0 }
          ? throw new ArgumentException("AdditionalState cannot be an empty byte array.", nameof(value))
          : value;
    }

    #endregion

    /* Parent state handling */

    #region Parent state handling

    // This is here to ensure we dont do expensive search every time we want to return the NetworkIdentity parent instance
    private readonly NetworkIdentity? _parent;

    // Actual parentId to send over the network, null means no change and 0 means no-parent.
    private readonly uint? _parentId;

    /// <summary>Indicates whether a parent is set. Returns true if the parent is either assigned or unassigned; otherwise, false. </summary>
    public readonly bool IsParentSet => _parentId is not null;

    /// <summary>Gets or sets the parent <see cref="NetworkIdentity"/>. When set, the associated parent ID is also cached. </summary>
    public readonly NetworkIdentity? Parent {
      get => _parent;
      init => (_parent, _parentId) = (value, value?.netId ?? 0);
    }

    #endregion

    /* Serialization and Deserialization */

    #region Serialization and DeserializatioMyRegion

    /// <summary> Serializes the NetworkPlayerState by encoding presence of state in a 16-bit header and writes relevant fields conditionally. </summary>
    /// <param name="writer">The NetworkWriter to write to.</param>
    /// <param name="state">The NetworkPlayerState to serialize.</param>
    public static void WriteNetworkPlayerState(NetworkWriter writer, NetworkPlayerState state) {
      // Ensure tick number is set; otherwise, we may send the wrong tick number here
      if (state._tickNumber is null)
        throw new InvalidOperationException("TickNumber must be set before serialization.");

      // Create header of 16 bits ( 5 bits for payload and 11 bits for the tick number )
      ushort header = 0;
      // First 5 bits represent the presence (null or non-null) of specific state
      if (state._parentId is not null) header |= (1 << 0); // Bit 0: Parent presence
      if (state.Position is not null) header |= (1 << 1); // Bit 1: Position presence
      if (state.BaseVelocity is not null) header |= (1 << 2); // Bit 2: BaseVelocity presence
      if (state.Rotation is not null) header |= (1 << 3); // Bit 3: Rotation presence
      if (state._additionalState is not null) header |= (1 << 4); // Bit 4: AdditionalState non-empty

      // Next 11 bits represent the tick number (masking to ensure only lower 11 bits are used)
      header |= (ushort)((state._tickNumber & 0x7FF) << 5);

      //Write header first
      writer.WriteUShort(header);

      // Write parent id if it's not null
      if (state._parentId is not null)
        writer.WriteUInt(state._parentId.Value);

      // Write position vector if it's not null
      if (state.Position is not null)
        writer.WriteVector3(state.Position.Value);

      // Write base velocity vector if it's not null
      if (state.BaseVelocity is not null)
        writer.WriteVector3(state.BaseVelocity.Value);

      // Write rotation quaternion if it's not null
      if (state.Rotation is not null)
        writer.WriteQuaternion(state.Rotation.Value);

      // Write additional state bytes
      if (state._additionalState is not null)
        writer.WriteBytesAndSize(state._additionalState.Value.ToArray(), 0, state._additionalState.Value.Length);
    }

    /// <summary> Deserializes NetworkPlayerState by reading a 16-bit header to determine which fields are present and reads them conditionally. </summary>
    /// <param name="reader">The NetworkReader to read from.</param>
    /// <returns>A deserialized instance of NetworkPlayerState.</returns>
    public static NetworkPlayerState ReadNetworkPlayerState(NetworkReader reader) {
      // Read the header first
      ushort header = reader.ReadUShort();
      // Extract presence bits from the header
      bool hasParentId = (header & (1 << 0)) != 0; // Bit 0: ServerTickOffset presence
      bool hasPosition = (header & (1 << 1)) != 0; // Bit 1: MovementVector presence
      bool hasBaseVelocity = (header & (1 << 2)) != 0; // Bit 2: JoystickVector presence
      bool hasRotation = (header & (1 << 3)) != 0; // Bit 3: MouseVector presence
      bool hasAdditionalState = (header & (1 << 4)) != 0; // Bit 4: AdditionalState non-empty
      // Extract the tick number from the header (last 11 bits)
      int tickNumber = (header >> 5) & 0x7FF;
      // extract the set data
      uint? parentId = hasParentId ? reader.ReadUInt() : null;
      Vector3? position = hasPosition ? reader.ReadVector3() : null;
      Vector3? baseVelocity = hasBaseVelocity ? reader.ReadVector3() : null;
      Quaternion? rotation = hasRotation ? reader.ReadQuaternion() : null;
      // Compiler nonsense requires me to use explicit if-else to avoid getting byte[0] instead of null
      ReadOnlyMemory<byte>? additionalState;
      if (hasAdditionalState)
        additionalState = new ReadOnlyMemory<byte>(reader.ReadBytesAndSize());
      else
        additionalState = null;
      // We want to fetch the parent here to prevent expensive get by id method later
      NetworkIdentity? parent = parentId is not null && parentId != 0 ? Utils.GetSpawnedInServerOrClient(parentId.Value) : null;
      return new NetworkPlayerState(
        tickNumber: tickNumber,
        parent: parent,
        parentId: parentId,
        position: position,
        baseVelocity: baseVelocity,
        rotation: rotation,
        additionalState: additionalState
      );
    }

    #endregion

    /*** Utility Functions ***/

    #region Utility Functions

    /// <summary> Compares two byte arrays for equality, including handling null values. </summary>
    /// <param name="byteArray1">The first ReadOnlyMemory<byte>? to compare, can be null. </param>
    /// <param name="byteArray2">The second ReadOnlyMemory<byte>? to compare, can be null. </param>
    /// <returns> Comparison by value </returns>
    static bool ByteArraysEqual(ReadOnlyMemory<byte>? byteArray1, ReadOnlyMemory<byte>? byteArray2) {
      // If both are null, they are equal
      if (byteArray1 == null && byteArray2 == null) return true;

      // If one is null but not the other, they are not equal
      if (byteArray1 == null || byteArray2 == null) return false;

      return byteArray1.Value.Span.SequenceEqual(byteArray2.Value.Span);
    }

    #endregion
  }

  public static class NetworkPlayerStateSerializer{
    public static void WriteNetworkPlayerState(this NetworkWriter writer, NetworkPlayerState value) {
      NetworkPlayerState.WriteNetworkPlayerState(writer, value);
    }

    public static NetworkPlayerState ReadNetworkPlayerState(this NetworkReader reader) {
      return NetworkPlayerState.ReadNetworkPlayerState(reader);
    }
  }
}
