using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror.Authenticators;

namespace Mirror.Examples.Chat
{
    [AddComponentMenu("")]
    [Obsolete("Use UniqueNameAuthenticator instead")]
    public class ChatAuthenticator : UniqueNameAuthenticator { }
}
