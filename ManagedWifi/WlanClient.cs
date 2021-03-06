﻿////////////////////////////////////////////////////////////////
//
// Copyright (c) 2007-2010 MetaGeek, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
//	http://www.apache.org/licenses/LICENSE-2.0 
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License. 
//
////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ManagedWifi
{
    public class WlanClient
    {
        private readonly IntPtr _clientHandle;
        private readonly Dictionary<Guid, WlanInterface> _ifaces = new Dictionary<Guid, WlanInterface>();
        private uint _negotiatedVersion;
        private readonly Wlan.WlanNotificationCallbackDelegate _wlanNotificationCallback;

        public event EventHandler<InterfaceNotificationEventsArgs> InterfaceArrivedEvent;

        public event EventHandler<InterfaceNotificationEventsArgs> InterfaceRemovedEvent;

        public WlanClient()
        {
            Wlan.ThrowIfError(Wlan.WlanOpenHandle(1, IntPtr.Zero, out _negotiatedVersion, out _clientHandle));
            try
            {
                Wlan.WlanNotificationSource source;
                _wlanNotificationCallback = new Wlan.WlanNotificationCallbackDelegate(OnWlanNotification);
                Wlan.ThrowIfError(Wlan.WlanRegisterNotification(_clientHandle, Wlan.WlanNotificationSource.All, false, _wlanNotificationCallback, IntPtr.Zero, IntPtr.Zero, out source));
            }
            catch
            {
                Wlan.WlanCloseHandle(_clientHandle, IntPtr.Zero);
                throw;
            }
        }

        ~WlanClient()
        {
            Wlan.WlanCloseHandle(_clientHandle, IntPtr.Zero);
        }

        public string GetStringForReasonCode(Wlan.WlanReasonCode reasonCode)
        {
            StringBuilder stringBuffer = new StringBuilder(0x400);
            Wlan.ThrowIfError(Wlan.WlanReasonCodeToString(reasonCode, stringBuffer.Capacity, stringBuffer, IntPtr.Zero));
            return stringBuffer.ToString();
        }

        protected virtual void InvokeInterfaceArrivedEvent(InterfaceNotificationEventsArgs e)
        {
            if (InterfaceArrivedEvent != null)
            {
                InterfaceArrivedEvent(this, e);
            }
        }

        protected virtual void InvokeInterfaceRemovedEvent(InterfaceNotificationEventsArgs e)
        {
            if (InterfaceRemovedEvent != null)
            {
                InterfaceRemovedEvent(this, e);
            }
        }

        private void OnWlanNotification(ref Wlan.WlanNotificationData notifyData, IntPtr context)
        {
            WlanInterface interface2 = _ifaces.ContainsKey(notifyData.interfaceGuid) ? _ifaces[notifyData.interfaceGuid] : null;
            switch (notifyData.notificationSource)
            {
                case Wlan.WlanNotificationSource.Acm:
                    switch (notifyData.notificationCode)
                    {
                        case 8:
                            if (notifyData.dataSize >= Marshal.SizeOf(0))
                            {
                                Wlan.WlanReasonCode reasonCode = (Wlan.WlanReasonCode) Marshal.ReadInt32(notifyData.dataPtr);
                                if (interface2 != null)
                                {
                                    interface2.OnWlanReason(notifyData, reasonCode);
                                }
                            }
                            goto Label_0194;

                        case 9:
                        case 10:
                        case 11:
                        case 20:
                        case 0x15:
                        {
                            Wlan.WlanConnectionNotificationData? nullable = ParseWlanConnectionNotification(ref notifyData);
                            if (nullable.HasValue && (interface2 != null))
                            {
                                interface2.OnWlanConnection(notifyData, nullable.Value);
                            }
                            goto Label_0194;
                        }
                        case 12:
                        case 15:
                        case 0x10:
                        case 0x11:
                        case 0x12:
                        case 0x13:
                            goto Label_0194;

                        case 13:
                            InvokeInterfaceArrivedEvent(new InterfaceNotificationEventsArgs(notifyData.interfaceGuid));
                            goto Label_0194;

                        case 14:
                            InvokeInterfaceRemovedEvent(new InterfaceNotificationEventsArgs(notifyData.interfaceGuid));
                            goto Label_0194;
                    }
                    break;

                case Wlan.WlanNotificationSource.Msm:
                    switch (notifyData.notificationCode)
                    {
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 9:
                        case 10:
                        case 11:
                        case 12:
                        case 13:
                        {
                            Wlan.WlanConnectionNotificationData? nullable2 = ParseWlanConnectionNotification(ref notifyData);
                            if (nullable2.HasValue && (interface2 != null))
                            {
                                interface2.OnWlanConnection(notifyData, nullable2.Value);
                            }
                            goto Label_0194;
                        }
                        case 7:
                        case 8:
                            goto Label_0194;
                    }
                    goto Label_0194;
            }
        Label_0194:
            if (interface2 != null)
            {
                interface2.OnWlanNotification(notifyData);
            }
        }

        private Wlan.WlanConnectionNotificationData? ParseWlanConnectionNotification(ref Wlan.WlanNotificationData notifyData)
        {
            int num = Marshal.SizeOf(typeof(Wlan.WlanConnectionNotificationData));
            if (notifyData.dataSize < num)
            {
                return null;
            }
            Wlan.WlanConnectionNotificationData data = (Wlan.WlanConnectionNotificationData) Marshal.PtrToStructure(notifyData.dataPtr, typeof(Wlan.WlanConnectionNotificationData));
            if (data.wlanReasonCode == Wlan.WlanReasonCode.Success)
            {
                IntPtr ptr = new IntPtr(notifyData.dataPtr.ToInt64() + Marshal.OffsetOf(typeof(Wlan.WlanConnectionNotificationData), "profileXml").ToInt64());
                data.profileXml = Marshal.PtrToStringUni(ptr);
            }
            return data;
        }

        public WlanInterface[] Interfaces
        {
            get
            {
                IntPtr ptr;
                WlanInterface[] interfaceArray2;
                Wlan.ThrowIfError(Wlan.WlanEnumInterfaces(_clientHandle, IntPtr.Zero, out ptr));
                try
                {
                    Wlan.WlanInterfaceInfoListHeader structure = (Wlan.WlanInterfaceInfoListHeader) Marshal.PtrToStructure(ptr, typeof(Wlan.WlanInterfaceInfoListHeader));
                    long num = ptr.ToInt64() + Marshal.SizeOf(structure);
                    WlanInterface[] interfaceArray = new WlanInterface[structure.numberOfItems];
                    List<Guid> list = new List<Guid>();
                    for (int i = 0; i < structure.numberOfItems; i++)
                    {
                        Wlan.WlanInterfaceInfo info = (Wlan.WlanInterfaceInfo) Marshal.PtrToStructure(new IntPtr(num), typeof(Wlan.WlanInterfaceInfo));
                        num += Marshal.SizeOf(info);
                        list.Add(info.interfaceGuid);
                        WlanInterface interface2 = _ifaces.ContainsKey(info.interfaceGuid) ? _ifaces[info.interfaceGuid] : new WlanInterface(this, info);
                        interfaceArray[i] = interface2;
                        _ifaces[info.interfaceGuid] = interface2;
                    }
                    Queue<Guid> queue = new Queue<Guid>();
                    foreach (Guid guid in _ifaces.Keys)
                    {
                        if (!list.Contains(guid))
                        {
                            queue.Enqueue(guid);
                        }
                    }
                    while (queue.Count != 0)
                    {
                        Guid key = queue.Dequeue();
                        _ifaces.Remove(key);
                    }
                    interfaceArray2 = interfaceArray;
                }
                finally
                {
                    Wlan.WlanFreeMemory(ptr);
                }
                return interfaceArray2;
            }
        }

        public class WlanInterface
        {
            private readonly WlanClient _client;
            private readonly Queue<object> _eventQueue = new Queue<object>();
            private readonly AutoResetEvent _eventQueueFilled = new AutoResetEvent(false);
            private Wlan.WlanInterfaceInfo _info;
            private bool _queueEvents;

            public event WlanConnectionNotificationEventHandler WlanConnectionNotification;

            public event WlanNotificationEventHandler WlanNotification;

            public event WlanReasonNotificationEventHandler WlanReasonNotification;

            internal WlanInterface(WlanClient client, Wlan.WlanInterfaceInfo info)
            {
                _client = client;
                _info = info;
            }

            private void Connect(Wlan.WlanConnectionParameters connectionParams)
            {
                Wlan.ThrowIfError(Wlan.WlanConnect(_client._clientHandle, _info.interfaceGuid, ref connectionParams, IntPtr.Zero));
            }

            private void Connect(Wlan.WlanConnectionMode connectionMode, Wlan.Dot11BssType bssType, string profile)
            {
                Wlan.WlanConnectionParameters parameters2 = new Wlan.WlanConnectionParameters {
                    wlanConnectionMode = connectionMode,
                    profile = profile,
                    dot11BssType = bssType,
                    flags = 0
                };
                Wlan.WlanConnectionParameters connectionParams = parameters2;
                Connect(connectionParams);
            }

            public void Connect(Wlan.WlanConnectionMode connectionMode, Wlan.Dot11BssType bssType, Wlan.Dot11Ssid ssid, Wlan.WlanConnectionFlags flags)
            {
                Wlan.WlanConnectionParameters parameters2 = new Wlan.WlanConnectionParameters {
                    wlanConnectionMode = connectionMode,
                    dot11SsidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(ssid)),
                    dot11BssType = bssType,
                    flags = flags
                };
                Wlan.WlanConnectionParameters connectionParams = parameters2;
                this.Connect(connectionParams);
                Marshal.StructureToPtr(ssid, connectionParams.dot11SsidPtr, false);
                Marshal.DestroyStructure(connectionParams.dot11SsidPtr, ssid.GetType());
                Marshal.FreeHGlobal(connectionParams.dot11SsidPtr);
            }

            public bool ConnectSynchronously(Wlan.WlanConnectionMode connectionMode, Wlan.Dot11BssType bssType, string profile, int connectTimeout)
            {
                _queueEvents = true;
                try
                {
                    Connect(connectionMode, bssType, profile);
                    while (_queueEvents && _eventQueueFilled.WaitOne(connectTimeout, true))
                    {
                        lock (_eventQueue)
                        {
                            while (_eventQueue.Count != 0)
                            {
                                object obj2 = _eventQueue.Dequeue();
                                if (obj2 is WlanConnectionNotificationEventData)
                                {
                                    WlanConnectionNotificationEventData data = (WlanConnectionNotificationEventData) obj2;
                                    if (((data.NotifyData.notificationSource != Wlan.WlanNotificationSource.Acm) || (data.NotifyData.notificationCode != 10)) || data.ConnNotifyData.profileName != profile)
                                    {
                                        break;
                                    }
                                    return true;
                                }
                            }
                            continue;
                        }
                    }
                }
                finally
                {
                    _queueEvents = false;
                    _eventQueue.Clear();
                }
                return false;
            }

            private Wlan.WlanAvailableNetwork[] ConvertAvailableNetworkListPtr(IntPtr availNetListPtr)
            {
                Wlan.WlanAvailableNetworkListHeader header = (Wlan.WlanAvailableNetworkListHeader) Marshal.PtrToStructure(availNetListPtr, typeof(Wlan.WlanAvailableNetworkListHeader));
                long num = availNetListPtr.ToInt64() + Marshal.SizeOf(typeof(Wlan.WlanAvailableNetworkListHeader));
                Wlan.WlanAvailableNetwork[] networkArray = new Wlan.WlanAvailableNetwork[header.numberOfItems];
                for (int i = 0; i < header.numberOfItems; i++)
                {
                    networkArray[i] = (Wlan.WlanAvailableNetwork) Marshal.PtrToStructure(new IntPtr(num), typeof(Wlan.WlanAvailableNetwork));
                    num += Marshal.SizeOf(typeof(Wlan.WlanAvailableNetwork));
                }
                return networkArray;
            }

            private Wlan.WlanBssEntryN[] ConvertBssListPtr(IntPtr bssListPtr)
            {
                Wlan.WlanBssListHeader header = (Wlan.WlanBssListHeader) Marshal.PtrToStructure(bssListPtr, typeof(Wlan.WlanBssListHeader));
                long num = bssListPtr.ToInt64() + Marshal.SizeOf(typeof(Wlan.WlanBssListHeader));
                Wlan.WlanBssEntryN[] entryArray = new Wlan.WlanBssEntryN[header.numberOfItems];
                for (int i = 0; i < header.numberOfItems; i++)
                {
                    entryArray[i] = new Wlan.WlanBssEntryN((Wlan.WlanBssEntry)Marshal.PtrToStructure(new IntPtr(num), typeof(Wlan.WlanBssEntry)));

                    int size = (int)entryArray[i].BaseEntry.ieSize;
                    byte[] IEs = new byte[size];

                    Marshal.Copy(new IntPtr(num + entryArray[i].BaseEntry.ieOffset), IEs, 0, size);

                    //Parse 802.11n IEs if avalible
                    entryArray[i].NSettings = IeParser.Parse(IEs);

                    //===DEBUGGING===
                    //string ssid = Encoding.ASCII.GetString(entryArray[i].entry.dot11Ssid.SSID);
                    //System.IO.File.WriteAllBytes("data" + ssid.Trim("\0".ToCharArray())  + ".dat", IEs);

                    //Console.WriteLine(IEs.Length);

                    //Test t = (Test)Marshal.PtrToStructure(new IntPtr(num), typeof(Test));
                    //===END DEBUGGING===
                    num += Marshal.SizeOf(typeof(Wlan.WlanBssEntry));
                }
                return entryArray;
            }

            public void DeleteProfile(string profileName)
            {
                Wlan.ThrowIfError(Wlan.WlanDeleteProfile(_client._clientHandle, _info.interfaceGuid, profileName, IntPtr.Zero));
            }

            private void EnqueueEvent(object queuedEvent)
            {
                lock (_eventQueue)
                {
                    _eventQueue.Enqueue(queuedEvent);
                }
                _eventQueueFilled.Set();
            }

            public IEnumerable<Wlan.WlanAvailableNetwork> GetAvailableNetworkList(Wlan.WlanGetAvailableNetworkFlags flags)
            {
                IntPtr ptr;
                Wlan.WlanAvailableNetwork[] networkArray;
                Wlan.ThrowIfError(Wlan.WlanGetAvailableNetworkList(_client._clientHandle, _info.interfaceGuid, flags, IntPtr.Zero, out ptr));
                try
                {
                    networkArray = ConvertAvailableNetworkListPtr(ptr);
                }
                finally
                {
                    Wlan.WlanFreeMemory(ptr);
                }
                return networkArray;
            }

            private int GetInterfaceInt(Wlan.WlanIntfOpcode opCode)
            {
                IntPtr ptr;
                int num;
                Wlan.WlanOpcodeValueType type;
                int num2;
                Wlan.ThrowIfError(Wlan.WlanQueryInterface(_client._clientHandle, _info.interfaceGuid, opCode, IntPtr.Zero, out num, out ptr, out type));
                try
                {
                    num2 = Marshal.ReadInt32(ptr);
                }
                finally
                {
                    Wlan.WlanFreeMemory(ptr);
                }
                return num2;
            }

            public IEnumerable<Wlan.WlanBssEntryN> GetNetworkBssList()
            {
                IntPtr ptr;
                Wlan.WlanBssEntryN[] entryArray;
                Wlan.ThrowIfError(Wlan.WlanGetNetworkBssList(_client._clientHandle, _info.interfaceGuid, IntPtr.Zero, Wlan.Dot11BssType.Any, false, IntPtr.Zero, out ptr));
                try
                {
                    entryArray = ConvertBssListPtr(ptr);
                }
                finally
                {
                    Wlan.WlanFreeMemory(ptr);
                }
                return entryArray;
            }

            public Wlan.WlanBssEntryN[] GetNetworkBssList(Wlan.Dot11Ssid ssid, Wlan.Dot11BssType bssType, bool securityEnabled)
            {
                Wlan.WlanBssEntryN[] entryArray;
                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(ssid));
                Marshal.StructureToPtr(ssid, ptr, false);
                try
                {
                    IntPtr ptr2;
                    Wlan.ThrowIfError(Wlan.WlanGetNetworkBssList(_client._clientHandle, _info.interfaceGuid, ptr, bssType, securityEnabled, IntPtr.Zero, out ptr2));
                    try
                    {
                        entryArray = ConvertBssListPtr(ptr2);
                    }
                    finally
                    {
                        Wlan.WlanFreeMemory(ptr2);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
                return entryArray;
            }

            public Wlan.WlanProfileInfo[] GetProfiles()
            {
                IntPtr ptr;
                Wlan.WlanProfileInfo[] infoArray2;
                Wlan.ThrowIfError(Wlan.WlanGetProfileList(_client._clientHandle, _info.interfaceGuid, IntPtr.Zero, out ptr));
                try
                {
                    Wlan.WlanProfileInfoListHeader structure = (Wlan.WlanProfileInfoListHeader) Marshal.PtrToStructure(ptr, typeof(Wlan.WlanProfileInfoListHeader));
                    Wlan.WlanProfileInfo[] infoArray = new Wlan.WlanProfileInfo[structure.numberOfItems];
                    long num = ptr.ToInt64() + Marshal.SizeOf(structure);
                    for (int i = 0; i < structure.numberOfItems; i++)
                    {
                        Wlan.WlanProfileInfo info = (Wlan.WlanProfileInfo) Marshal.PtrToStructure(new IntPtr(num), typeof(Wlan.WlanProfileInfo));
                        infoArray[i] = info;
                        num += Marshal.SizeOf(info);
                    }
                    infoArray2 = infoArray;
                }
                finally
                {
                    Wlan.WlanFreeMemory(ptr);
                }
                return infoArray2;
            }

            public string GetProfileXml(string profileName)
            {
                IntPtr ptr;
                Wlan.WlanProfileFlags flags;
                Wlan.WlanAccess access;
                string str;
                Wlan.ThrowIfError(Wlan.WlanGetProfile(_client._clientHandle, _info.interfaceGuid, profileName, IntPtr.Zero, out ptr, out flags, out access));
                try
                {
                    str = Marshal.PtrToStringUni(ptr);
                }
                finally
                {
                    Wlan.WlanFreeMemory(ptr);
                }
                return str;
            }

            internal void OnWlanConnection(Wlan.WlanNotificationData notifyData, Wlan.WlanConnectionNotificationData connNotifyData)
            {
                if (WlanConnectionNotification != null)
                {
                    WlanConnectionNotification(notifyData, connNotifyData);
                }
                if (_queueEvents)
                {
                    WlanConnectionNotificationEventData data2 = new WlanConnectionNotificationEventData {
                        NotifyData = notifyData,
                        ConnNotifyData = connNotifyData
                    };
                    WlanConnectionNotificationEventData queuedEvent = data2;
                    EnqueueEvent(queuedEvent);
                }
            }

            internal void OnWlanNotification(Wlan.WlanNotificationData notifyData)
            {
                if (WlanNotification != null)
                {
                    WlanNotification(notifyData);
                }
            }

            internal void OnWlanReason(Wlan.WlanNotificationData notifyData, Wlan.WlanReasonCode reasonCode)
            {
                if (WlanReasonNotification != null)
                {
                    WlanReasonNotification(notifyData, reasonCode);
                }
                if (_queueEvents)
                {
                    WlanReasonNotificationData data2 = new WlanReasonNotificationData {
                        NotifyData = notifyData,
                        ReasonCode = reasonCode
                    };
                    WlanReasonNotificationData queuedEvent = data2;
                    EnqueueEvent(queuedEvent);
                }
            }

            public void Scan()
            {
                Wlan.ThrowIfError(Wlan.WlanScan(_client._clientHandle, _info.interfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
            }

            private void SetInterfaceInt(Wlan.WlanIntfOpcode opCode, int value)
            {
                IntPtr ptr = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(ptr, value);
                try
                {
                    Wlan.ThrowIfError(Wlan.WlanSetInterface(_client._clientHandle, _info.interfaceGuid, opCode, 4, ptr, IntPtr.Zero));
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

            public Wlan.WlanReasonCode SetProfile(Wlan.WlanProfileFlags flags, string profileXml, bool overwrite)
            {
                Wlan.WlanReasonCode code;
                Wlan.ThrowIfError(Wlan.WlanSetProfile(_client._clientHandle, _info.interfaceGuid, flags, profileXml, null, overwrite, IntPtr.Zero, out code));
                return code;
            }

            public bool Autoconf
            {
                get
                {
                    return (GetInterfaceInt(Wlan.WlanIntfOpcode.AutoconfEnabled) != 0);
                }
                set
                {
                    SetInterfaceInt(Wlan.WlanIntfOpcode.AutoconfEnabled, value ? 1 : 0);
                }
            }

            public Wlan.Dot11BssType BssType
            {
                get
                {
                    return (Wlan.Dot11BssType) GetInterfaceInt(Wlan.WlanIntfOpcode.BssType);
                }
                set
                {
                    SetInterfaceInt(Wlan.WlanIntfOpcode.BssType, (int) value);
                }
            }

            public int Channel
            {
                get
                {
                    return GetInterfaceInt(Wlan.WlanIntfOpcode.ChannelNumber);
                }
            }

            public Wlan.WlanConnectionAttributes CurrentConnection
            {
                get
                {
                    int num;
                    IntPtr ptr;
                    Wlan.WlanOpcodeValueType type;
                    Wlan.WlanConnectionAttributes attributes;
                    Wlan.ThrowIfError(Wlan.WlanQueryInterface(_client._clientHandle, _info.interfaceGuid, Wlan.WlanIntfOpcode.CurrentConnection, IntPtr.Zero, out num, out ptr, out type));
                    try
                    {
                        attributes = (Wlan.WlanConnectionAttributes) Marshal.PtrToStructure(ptr, typeof(Wlan.WlanConnectionAttributes));
                    }
                    finally
                    {
                        Wlan.WlanFreeMemory(ptr);
                    }
                    return attributes;
                }
            }

            public Wlan.Dot11OperationMode CurrentOperationMode
            {
                get
                {
                    return (Wlan.Dot11OperationMode) GetInterfaceInt(Wlan.WlanIntfOpcode.CurrentOperationMode);
                }
            }

            public string InterfaceDescription
            {
                get
                {
                    return _info.interfaceDescription;
                }
            }

            public Guid InterfaceGuid
            {
                get
                {
                    return _info.interfaceGuid;
                }
            }

            public string InterfaceName
            {
                get
                {
                    return NetworkInterface.Name;
                }
            }

            public Wlan.WlanInterfaceState InterfaceState
            {
                get
                {
                    return (Wlan.WlanInterfaceState) GetInterfaceInt(Wlan.WlanIntfOpcode.InterfaceState);
                }
            }

            public NetworkInterface NetworkInterface
            {
                get
                {
                    foreach (
                        NetworkInterface interface2 in
                            NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (interface2.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        {
                            Guid guid = new Guid(interface2.Id);
                            if (guid.Equals(_info.interfaceGuid))
                            {
                                return interface2;
                            }
                        }
                    }
                    return null;
                }
            }

            public int Rssi
            {
                get
                {
                    return GetInterfaceInt(Wlan.WlanIntfOpcode.Rssi);
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct WlanConnectionNotificationEventData
            {
                public Wlan.WlanNotificationData NotifyData;
                public Wlan.WlanConnectionNotificationData ConnNotifyData;
            }

            public delegate void WlanConnectionNotificationEventHandler(Wlan.WlanNotificationData notifyData, Wlan.WlanConnectionNotificationData connNotifyData);

            public delegate void WlanNotificationEventHandler(Wlan.WlanNotificationData notifyData);

            [StructLayout(LayoutKind.Sequential)]
            private struct WlanReasonNotificationData
            {
                public Wlan.WlanNotificationData NotifyData;
                public Wlan.WlanReasonCode ReasonCode;
            }

            public delegate void WlanReasonNotificationEventHandler(Wlan.WlanNotificationData notifyData, Wlan.WlanReasonCode reasonCode);
        }
    }
}

