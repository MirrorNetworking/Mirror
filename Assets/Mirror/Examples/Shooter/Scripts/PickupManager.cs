using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;

public class PickupManager : NetworkBehaviour
{
    // we're using a manager and sync list to handle non-networked pickups, rather than network spawning each individual pickup

    [Serializable]
    public struct PickupStruct
    {
        public bool isActive;
        public GameObject objectLocation;
    }

    // we dont want to sync spawn location, we only want to sync active status, and link this to index number
    [Serializable]
    public struct PickupStructSync
    {
        public bool isActive;
    }

    public int minPickupsActive = 1; // amount to trigger a re-calculation of which pickups are active
    public int maxPickupsActive = 3; // should be less or same as total pickup list count
    public List<PickupStruct> pickupList;
    readonly SyncList<PickupStructSync> pickupSyncList = new SyncList<PickupStructSync>();
    private int totalActivePickups = 0;

    public override void OnStartServer()
    {
        base.OnStartServer();

        CalculateActivePickups();
    }

    public void Start()
    {
        if (isClientOnly)
        {
            pickupSyncList.Callback += OnSyncListUpdated;

            //print("pickupSyncList.Count: " + pickupSyncList.Count);

            // clients setup local list active status, according to servers sent sync list
            for (int i = 0; i < pickupSyncList.Count; i++)
            {
                UpdateLocalList(i);
            }
            SetupActivePickups();
        }
    }

    void CalculateActivePickups()
    {
        // server rolls some RNG pickups according to your placements and maxPickupsActive variable
        //print("CalculateActivePickups");
        int pickupCounter = 0;
        int currentPickup = 0;

        while (pickupCounter < maxPickupsActive)
        {
            pickupCounter += 1;
            currentPickup = UnityEngine.Random.Range(0, pickupList.Count);
            // we may get same pickup being activated whilst still increasing counter
            PickupStruct pickupStruct = pickupList[currentPickup];
            pickupStruct.isActive = true;
            pickupList[currentPickup] = pickupStruct;
        }

        pickupSyncList.Clear();

        for (int i = 0; i < pickupList.Count; i++)
        {
            PickupStructSync pickupStructSync;
            pickupStructSync.isActive = pickupList[i].isActive;
            pickupSyncList.Add(pickupStructSync);
        }

        //print("pickupSyncList.Count: " + pickupSyncList.Count);

        SetupActivePickups();
    }

    void SetupActivePickups()
    {
        // client and server run this, updates the pickups in map according to list data
        totalActivePickups = 0;
        for (int i = 0; i < pickupList.Count; i++)
        {
            pickupList[i].objectLocation.SetActive(pickupList[i].isActive);
            if (pickupList[i].isActive)
            {
                totalActivePickups += 1;
            }
        }
        //print("SetupActivePickups: " + totalActivePickups);
    }

    void OnSyncListUpdated(SyncList<PickupStructSync>.Operation op, int index, PickupStructSync oldItem, PickupStructSync newItem)
    {
        //print("OnSyncListUpdated: " + op);

        switch (op)
        {
            case SyncList<PickupStructSync>.Operation.OP_ADD:
                // index is where it was added into the list
                // newItem is the new item

                UpdateLocalList(index);

                break;
            case SyncList<PickupStructSync>.Operation.OP_INSERT:
                // index is where it was inserted into the list
                // newItem is the new item
                break;
            case SyncList<PickupStructSync>.Operation.OP_REMOVEAT:
                // index is where it was removed from the list
                // oldItem is the item that was removed
                break;
            case SyncList<PickupStructSync>.Operation.OP_SET:
                // index is of the item that was changed
                // oldItem is the previous value for the item at the index
                // newItem is the new value for the item at the index

                UpdateLocalList(index);

                break;
            case SyncList<PickupStructSync>.Operation.OP_CLEAR:
                // list got cleared
                break;
        }

        SetupActivePickups();
    }

    public void DisablePickup(GameObject _gameObject)
    {
        //print("DisablePickup");

        for (int i = 0; i < pickupList.Count; i++)
        {
            if (_gameObject == pickupList[i].objectLocation)
            {
                //print("Pickup Matches");
                // generate new pickups if minimum amount in map has been reached.
                totalActivePickups -= 1;
                if (totalActivePickups <= minPickupsActive)
                {
                    CalculateActivePickups();
                }
                else
                {
                    // disable matching pickup on local list, then update sync list
                    pickupList[i].objectLocation.SetActive(false);

                    PickupStruct pickupStruct = pickupList[i];
                    pickupStruct.isActive = false;
                    pickupList[i] = pickupStruct;

                    UpdateSyncList(i);
                }  

                break;
            }
        }
    }

    void UpdateLocalList(int index)
    {
        PickupStruct pickupStruct = pickupList[index];
        pickupStruct.isActive = pickupSyncList[index].isActive;
        pickupList[index] = pickupStruct;
    }

    void UpdateSyncList(int index)
    {
        PickupStructSync pickupStructSync = pickupSyncList[index];
        pickupStructSync.isActive = pickupList[index].isActive;
        pickupSyncList[index] = pickupStructSync;
    }
}
