using System;
using UnityEngine;

namespace System.Runtime.CompilerServices{
  internal static class IsExternalInit{
  }
}

#nullable enable
namespace Mirror.Components.Experimental{
  public struct NetworkPlayerInputs{
    /// <summary> Initializes a new instance of the <see cref="NetworkPlayerInputs"/> struct. Optionally takes defaults to initialize the instance. </summary>
    /// <param name="defaults">Default inputs to copy values from, or null for default initialization.</param>
    public NetworkPlayerInputs(NetworkPlayerInputs? defaults = null) {
      _tickNumber = defaults?._tickNumber;
      _serverTickOffset = defaults?._serverTickOffset;
      _movementVector = defaults?._movementVector;
      _joystickVector = defaults?._joystickVector;
      _mouseVectorX = defaults?._mouseVectorX;
      _mouseVectorY = defaults?._mouseVectorY;
      _additionalInputs = defaults?._additionalInputs;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkPlayerInputs"/> struct with specific values.
    /// This private constructor allows internal use for creating instances with selected fields.
    /// </summary>
    /// <param name="tickNumber">The tick number to set.</param>
    /// <param name="serverTickOffset">The server tick offset, or null if unset.</param>
    /// <param name="movementVector">The movement vector, or null if unset.</param>
    /// <param name="joystickVector">The joystick vector, or null if unset.</param>
    /// <param name="mouseVectorX">The X component of the mouse vector, or null if unset.</param>
    /// <param name="mouseVectorY">The Y component of the mouse vector, or null if unset.</param>
    /// <param name="additionalInputs">The additional inputs, or null if unset.</param>
    private NetworkPlayerInputs(int? tickNumber, byte? serverTickOffset, ushort? movementVector, ushort? joystickVector, ushort? mouseVectorX,
      ushort? mouseVectorY, ReadOnlyMemory<byte>? additionalInputs) {
      _tickNumber = tickNumber;
      _serverTickOffset = serverTickOffset;
      _movementVector = movementVector;
      _joystickVector = joystickVector;
      _mouseVectorX = mouseVectorX;
      _mouseVectorY = mouseVectorY;
      _additionalInputs = additionalInputs;
    }

    /// <summary>
    /// Compares the current NetworkPlayerInputs with another and returns the differences as a new instance.
    /// Only fields that differ will be set in the returned instance; others will remain null.
    /// </summary>
    /// <param name="inputs">The other NetworkPlayerInputs to compare with.</param>
    /// <returns>A new NetworkPlayerInputs instance with only differing values, or null if there are no differences. </returns>
    public NetworkPlayerInputs? GetChangedInputsComparedTo(NetworkPlayerInputs inputs) {
      // Compare each field and set values that differ, leaving others as null
      byte? serverTickOffset = _serverTickOffset != inputs._serverTickOffset ? _serverTickOffset : null;
      ushort? movementVector = _movementVector != inputs._movementVector ? _movementVector : null;
      ushort? joystickVector = _joystickVector != inputs._joystickVector ? _joystickVector : null;
      ReadOnlyMemory<byte>? additionalInputs = !_additionalInputs.Equals(inputs._additionalInputs) ? _additionalInputs : null;

      // If any of these are different we need to send both
      bool mouseVectorDiff = _mouseVectorX != inputs._mouseVectorX || _mouseVectorY != inputs._mouseVectorY;
      ushort? mouseVectorX = mouseVectorDiff ? _mouseVectorX : null;
      ushort? mouseVectorY = mouseVectorDiff ? _mouseVectorY : null;

      // If no differences exist, return null
      return serverTickOffset is null && movementVector is null && joystickVector is null &&
             mouseVectorX is null && mouseVectorY is null && additionalInputs is null
        ? null
        : new NetworkPlayerInputs(
          tickNumber: _tickNumber,
          serverTickOffset: serverTickOffset,
          movementVector: movementVector,
          joystickVector: joystickVector,
          mouseVectorX: mouseVectorX,
          mouseVectorY: mouseVectorY,
          additionalInputs: additionalInputs
        );
    }

    /* Tick Number Handling */

    #region Tick Number Handling

    // Stores the validated tick number for the player's inputs.
    private int? _tickNumber;

    /// <summary> Represents the tick number for the player's inputs, validated to be within the range [0, 2047]. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown if the tick number is outside the range [0, 2047]. </exception>
    public int? TickNumber {
      get => _tickNumber;
      set => _tickNumber = value is null or < 0 or > 2047
        ? throw new ArgumentOutOfRangeException(nameof(value), $"Invalid TickNumber: {value}. It must be between 0 and 2047.")
        : value % 2048;
    }

    #endregion

    /* Server Tick Offset handling */

    #region Server Tick Offset handling

    // Stores the server tick offset as a byte, representing a value between 0 and 255.
    private readonly byte? _serverTickOffset;

    /// <summary> Represents the server tick offset, ensuring it is within the range of 0 to 255. Throws an exception if the value is out of range. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown if the X or Y components are outside the range [-1, 1]. </exception>
    public readonly int? ServerTickOffset {
      get => _serverTickOffset;
      init => _serverTickOffset = value is null
        ? _serverTickOffset
        : value is < 0 or > 255
          ? throw new ArgumentOutOfRangeException(nameof(value), $"Invalid ServerTickOffset: {value}. It must be between 0 and 255.")
          : (byte)value;
    }

    #endregion

    /* Movement Vector2 Normals handling */

    #region Movement Vector2 Normals handling

    // Stores the movement input as a serialized Vector2.
    private readonly ushort? _movementVector;

    /// <summary> Represents the movement input as a Vector2 with components clamped to the range [-1, 1]. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown if the X or Y components are outside the range [-1, 1]. </exception>
    public readonly Vector2? MovementVector {
      get => _movementVector is not null
        ? DecompressUshortNormalsToVector2(_movementVector.Value)
        : null;
      init => _movementVector = value is null
        ? _movementVector
        : value.Value.x is < -1 or > 1 || value.Value.y is < -1 or > 1
          ? throw new ArgumentOutOfRangeException(nameof(value), $"Invalid MovementVector: {value}. Components must be between -1 and 1.")
          : CompressVector2NormalsToUshort(value.Value);
    }

    #endregion

    /* Joystick Vector2 Normals handling */

    #region Joystick Vector2 Normals handling

    // Stores the joystick vector as a normalized Vector2.
    private readonly ushort? _joystickVector;

    /// <summary> Represents the joystick input as a normalized Vector2 with components in the range [-1, 1]. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown if the vector components are outside the range [-1, 1]. </exception>
    public readonly Vector2? JoystickVector {
      get => _joystickVector is not null
        ? DecompressUshortNormalsToVector2(_joystickVector.Value)
        : null;
      init => _joystickVector = value is null
        ? _joystickVector
        : value.Value.x is < -1 or > 1 || value.Value.y is < -1 or > 1
          ? throw new ArgumentOutOfRangeException(nameof(value), $"Invalid JoystickVector: {value}. Components must be between -1 and 1.")
          : CompressVector2NormalsToUshort(value.Value);
    }

    #endregion

    /* Mouse Vector2 handling */

    #region Mouse Vector2 handling

    // Stores the mouse vector components as half-precision floats for efficient storage.
    private readonly ushort? _mouseVectorX;
    private readonly ushort? _mouseVectorY;

    /// <summary>Represents the mouse input as a Vector2, with components stored as half-precision floats.</summary>
    public readonly Vector2? MouseVector {
      get => _mouseVectorX.HasValue && _mouseVectorY.HasValue
        ? new Vector2(Mathf.HalfToFloat(_mouseVectorX.Value), Mathf.HalfToFloat(_mouseVectorY.Value))
        : null;
      init {
        _mouseVectorX = value is null ? _mouseVectorX : Mathf.FloatToHalf(value.Value.x);
        _mouseVectorY = value is null ? _mouseVectorY : Mathf.FloatToHalf(value.Value.y);
      }
    }

    #endregion

    /* Additional inputs handling */

    #region Additional inputs handling

    //Stores additional inputs defined by the developer, allowing custom byte data.
    private ReadOnlyMemory<byte>? _additionalInputs;

    /// <summary> Stores additional inputs defined by the developer, allowing custom byte data. Empty byte arrays are not allowed. </summary>
    /// <exception cref="ArgumentException">Thrown if the byte array is empty.</exception>
    public ReadOnlyMemory<byte>? AdditionalInputs {
      get => _additionalInputs;
      init => _additionalInputs = value is null
        ? _additionalInputs
        : value is { Length: 0 }
          ? throw new ArgumentException("AdditionalInputs cannot be an empty byte array.", nameof(value))
          : value;
    }

    #endregion

    /* Serialization and Deserialization */

    #region Serialization and DeserializatioMyRegion

    /// <summary> Serializes the NetworkPlayerInputs by encoding presence of inputs in a 16-bit header and writes relevant fields conditionally. </summary>
    /// <param name="writer">The NetworkWriter to write to.</param>
    /// <param name="inputs">The NetworkPlayerInputs to serialize.</param>
    public static void WriteNetworkPlayerInputs(NetworkWriter writer, NetworkPlayerInputs inputs) {
      // Ensure tick number is set; otherwise, we may send the wrong tick number here
      if (inputs._tickNumber is null)
        throw new InvalidOperationException("TickNumber must be set before serialization.");

      // Create header of 16 bits ( 5 bits for payload and 11 bits for the tick number )
      ushort header = 0;
      // First 5 bits represent the presence (null or non-null) of specific inputs
      if (inputs._serverTickOffset is not null) header |= (1 << 0); // Bit 0: ServerTickOffset presence
      if (inputs._movementVector is not null) header |= (1 << 1); // Bit 1: MovementVector presence
      if (inputs._joystickVector is not null) header |= (1 << 2); // Bit 2: JoystickVector presence
      if (inputs._mouseVectorX is not null && inputs._mouseVectorY is not null) header |= (1 << 3); // Bit 3: MouseVector presence
      if (inputs._additionalInputs is not null) header |= (1 << 4); // Bit 4: AdditionalInputs non-empty

      // Next 11 bits represent the tick number (masking to ensure only lower 11 bits are used)
      header |= (ushort)((inputs._tickNumber & 0x7FF) << 5);

      //Write header first
      writer.WriteUShort(header);

      // Write server tick offset if its not null
      if (inputs._serverTickOffset is not null)
        writer.WriteByte(inputs._serverTickOffset.Value);

      // Write compressed movement vector if its not null
      if (inputs._movementVector is not null)
        writer.WriteUShort(inputs._movementVector.Value);

      // Write compressed joystick vector if its not null
      if (inputs._joystickVector is not null)
        writer.WriteUShort(inputs._joystickVector.Value);

      // Write Half (fp16) mouse vector if its not null
      if (inputs._mouseVectorX is not null && inputs._mouseVectorY is not null) {
        writer.WriteUShort(inputs._mouseVectorX.Value);
        writer.WriteUShort(inputs._mouseVectorY.Value);
      }

      // Write additional inputs bytes
      if (inputs._additionalInputs is not null)
        writer.WriteBytesAndSize(inputs._additionalInputs.Value.ToArray(), 0, inputs._additionalInputs.Value.Length);
    }


    /// <summary> Deserializes NetworkPlayerInputs by reading a 16-bit header to determine which fields are present and reads them conditionally. </summary>
    /// <param name="reader">The NetworkReader to read from.</param>
    /// <returns>A deserialized instance of NetworkPlayerInputs.</returns>
    public static NetworkPlayerInputs ReadNetworkPlayerInputs(NetworkReader reader) {
      // Read the header first
      ushort header = reader.ReadUShort();

      // Extract presence bits from the header
      bool hasServerTickOffset = (header & (1 << 0)) != 0; // Bit 0: ServerTickOffset presence
      bool hasMovementVector = (header & (1 << 1)) != 0; // Bit 1: MovementVector presence
      bool hasJoystickVector = (header & (1 << 2)) != 0; // Bit 2: JoystickVector presence
      bool hasMouseVector = (header & (1 << 3)) != 0; // Bit 3: MouseVector presence
      bool hasAdditionalInputs = (header & (1 << 4)) != 0; // Bit 4: AdditionalInputs non-empty

      // Extract the tick number from the header (last 11 bits)
      int tickNumber = (header >> 5) & 0x7FF;

      // Initialize fields
      byte? serverTickOffset = hasServerTickOffset ? reader.ReadByte() : null;
      ushort? movementVector = hasMovementVector ? reader.ReadUShort() : null;
      ushort? joystickVector = hasJoystickVector ? reader.ReadUShort() : null;
      ushort? mouseVectorX = hasMouseVector ? reader.ReadUShort() : null;
      ushort? mouseVectorY = hasMouseVector ? reader.ReadUShort() : null;

      // Compiler nonsense requires me to use explicit if-else to avoid getting byte[0] instead of null
      ReadOnlyMemory<byte>? additionalInputs;
      if (hasAdditionalInputs)
        additionalInputs = new ReadOnlyMemory<byte>(reader.ReadBytesAndSize());
      else
        additionalInputs = null;

      // Construct and return the NetworkPlayerInputs object
      return new NetworkPlayerInputs(
        tickNumber: tickNumber,
        serverTickOffset: serverTickOffset,
        movementVector: movementVector,
        joystickVector: joystickVector,
        mouseVectorX: mouseVectorX,
        mouseVectorY: mouseVectorY,
        additionalInputs: additionalInputs
      );
    }

    #endregion


    /* Utility functions */

    #region Utility functions

    /// <summary> Decompresses a <see cref="ushort"/> back into normalized Vector2 values (X and Y). </summary>
    /// <param name="compressedValue">The compressed <see cref="ushort"/> value.</param>
    /// <returns>A tuple containing the normalized Vector2 values in the range [-1, 1].</returns>
    public static Vector2 DecompressUshortNormalsToVector2(ushort compressedValue) {
      // Extract byteX and byteY from the compressed ushort
      byte byteX = (byte)(compressedValue >> 8);
      byte byteY = (byte)(compressedValue & 0xFF);
      // Convert byte values back to normalized Vector2 in the range [-1, 1]
      return new Vector2() { x = (byteX / 127f) - 1f, y = (byteY / 127f) - 1f };
    }


    /// <summary> Compresses normalized Vector2 values (X and Y) into a single <see cref="ushort"/>. </summary>
    /// <param name="vector">The normalized X and Y axis values in the range [-1, 1].</param>
    /// <returns>A <see cref="ushort"/> representing the compressed Vector2 values.</returns>
    public static ushort CompressVector2NormalsToUshort(Vector2 vector) {
      // Scale and shift values from [-1, 1] to [0, 254]
      byte byteX = (byte)((Mathf.Clamp(vector.x, -1f, 1f) + 1f) * 127f);
      byte byteY = (byte)((Mathf.Clamp(vector.y, -1f, 1f) + 1f) * 127f);
      // Combine byteX and byteY into a single ushort
      return (ushort)((byteX << 8) | byteY);
    }

    #endregion
  }

  public static class GeneratedNetworkCode{
    public static void WriteNetworkPlayerInputs(this NetworkWriter writer, NetworkPlayerInputs value) {
      NetworkPlayerInputs.WriteNetworkPlayerInputs(writer, value);
    }

    public static NetworkPlayerInputs ReadNetworkPlayerInputs(this NetworkReader reader) {
      return NetworkPlayerInputs.ReadNetworkPlayerInputs(reader);
    }
  }
}
