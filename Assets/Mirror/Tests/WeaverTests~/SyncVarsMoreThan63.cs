using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        [SyncVar] int var2;
        [SyncVar] int var3;
        [SyncVar] int var4;
        [SyncVar] int var5;
        [SyncVar] int var6;
        [SyncVar] int var7;
        [SyncVar] int var8;
        [SyncVar] int var9;
        [SyncVar] int var10;
        [SyncVar] int var11;
        [SyncVar] int var12;
        [SyncVar] int var13;
        [SyncVar] int var14;
        [SyncVar] int var15;
        [SyncVar] int var16;
        [SyncVar] int var17;
        [SyncVar] int var18;
        [SyncVar] int var19;
        [SyncVar] int var20;
        [SyncVar] int var21;
        [SyncVar] int var22;
        [SyncVar] int var23;
        [SyncVar] int var24;
        [SyncVar] int var25;
        [SyncVar] int var26;
        [SyncVar] int var27;
        [SyncVar] int var28;
        [SyncVar] int var29;
        [SyncVar] int var30;
        [SyncVar] int var31;
        [SyncVar] int var32;
        [SyncVar] int var33;
        [SyncVar] int var34;
        [SyncVar] int var35;
        [SyncVar] int var36;
        [SyncVar] int var37;
        [SyncVar] int var38;
        [SyncVar] int var39;
        [SyncVar] int var40;
        [SyncVar] int var41;
        [SyncVar] int var42;
        [SyncVar] int var43;
        [SyncVar] int var44;
        [SyncVar] int var45;
        [SyncVar] int var46;
        [SyncVar] int var47;
        [SyncVar] int var48;
        [SyncVar] int var49;
        [SyncVar] int var50;
        [SyncVar] int var51;
        [SyncVar] int var52;
        [SyncVar] int var53;
        [SyncVar] int var54;
        [SyncVar] int var55;
        [SyncVar] int var56;
        [SyncVar] int var57;
        [SyncVar] int var58;
        [SyncVar] int var59;
        [SyncVar] int var60;
        [SyncVar] int var61;
        [SyncVar] int var62;
        [SyncVar] int var63;
        [SyncVar] int var64;

        public void TakeDamage(int amount)
        {
            if (!isServer)
                return;

            health -= amount;
        }

        void OnChangeHealth(int health)
        {
            // do things with your health bar
        }
    }
}
