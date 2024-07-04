// CharacterController2k is based on Unity's OpenCharacterController, modified by vis2k, all rights reserved.
//
// -------------------------------------------------------------------------------------------------------------
// Original License from: https://github.com/Unity-Technologies/Standard-Assets-Characters:
// Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.
//
using UnityEngine;

namespace Controller2k
{
	public static class Extensions
	{
		// Is floatA equal to zero? Takes floating point inaccuracy into account, by using Epsilon.
		public static bool IsEqualToZero(this float floatA)
		{
			return Mathf.Abs(floatA) < Mathf.Epsilon;
		}

		// Is floatA not equal to zero? Takes floating point inaccuracy into account, by using Epsilon.
		public static bool NotEqualToZero(this float floatA)
		{
			return Mathf.Abs(floatA) > Mathf.Epsilon;
		}
	}
}